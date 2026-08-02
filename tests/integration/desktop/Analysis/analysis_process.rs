use changelens_desktop_lib::analysis::{AnalysisRunState, AnalysisService};
use changelens_desktop_lib::engine_protocol::{ActionErrorKind, EngineClient};
use std::path::{Path, PathBuf};
use std::process::Command;
use std::sync::OnceLock;
use std::time::{Duration, Instant};

const REPOSITORY_PATH: &str = "/projects/change_lens";
const TARGET: &str = "refs/remotes/origin/main";
const FRESHNESS_TOKEN: &str = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
const RUN_ID: &str = "0198a1b2-3c4d-4e5f-8a9b-0123456789ab";

#[test]
fn start_poll_and_cancel_reuse_one_process() {
    let client = client_for_mode("analysis-success");

    let start = client
        .start(REPOSITORY_PATH, TARGET, FRESHNESS_TOKEN, None)
        .expect("the start result must parse");
    let process_id = client
        .process_id_for_testing()
        .expect("the start action must start the fixture");
    let active = client
        .get_active(REPOSITORY_PATH)
        .expect("the active-lookup result must parse");
    let poll = client.poll_run(RUN_ID).expect("the poll result must parse");
    client.cancel(RUN_ID).expect("the cancel result must parse");

    assert!(matches!(
        start,
        changelens_desktop_lib::analysis::AnalysisStartResult::Accepted { .. }
    ));
    assert!(matches!(
        active,
        changelens_desktop_lib::analysis::AnalysisGetActiveResult::Active { .. }
    ));
    assert_eq!(poll.state, AnalysisRunState::PendingCapture);
    assert_eq!(client.process_id_for_testing(), Some(process_id));
}

#[test]
fn preserves_ordered_operation_errors_without_restarting() {
    let client = client_for_mode("analysis-ordered-error-once");

    let error = client
        .poll_run(RUN_ID)
        .expect_err("the fixture must fail the first analysis action");
    let process_id = client
        .process_id_for_testing()
        .expect("operation errors must preserve the process");
    client
        .poll_run(RUN_ID)
        .expect("the later explicit action must use the same process");

    assert_eq!(error.kind, ActionErrorKind::Operation);
    assert_eq!(error.errors[0].code, "analysis.unknownRun");
    assert_eq!(client.process_id_for_testing(), Some(process_id));
}

#[test]
fn invalid_analysis_result_invalidates_the_process() {
    let client = client_for_mode("analysis-invalid-result-once");

    let first = client
        .poll_run(RUN_ID)
        .expect_err("the malformed run summary must fail");
    assert_eq!(client.process_id_for_testing(), None);
    let second = client
        .poll_run(RUN_ID)
        .expect_err("a later action must restart the malformed fixture");

    for error in [first, second] {
        assert_eq!(error.kind, ActionErrorKind::Protocol);
        assert_eq!(error.errors[0].code, "protocol.invalidResponse");
    }
}

#[test]
fn analysis_start_timeout_is_not_replayed_and_later_explicit_action_restarts() {
    let client = client_for_mode("analysis-delay-first");
    let started_at = Instant::now();

    let error = client
        .start_with_timeout_for_testing(
            REPOSITORY_PATH,
            TARGET,
            FRESHNESS_TOKEN,
            None,
            Duration::from_millis(50),
        )
        .expect_err("the delayed start response must exceed the test deadline");

    assert!(started_at.elapsed() < Duration::from_secs(2));
    assert_eq!(error.kind, ActionErrorKind::Transport);
    assert_eq!(error.errors[0].code, "engine.responseTimedOut");
    assert_eq!(client.process_id_for_testing(), None);
    client
        .start(REPOSITORY_PATH, TARGET, FRESHNESS_TOKEN, None)
        .expect("a later explicit action must restart the engine process");
}

fn client_for_mode(mode: &str) -> EngineClient {
    static FIXTURE_BUILD: OnceLock<()> = OnceLock::new();
    FIXTURE_BUILD.get_or_init(|| build_dotnet_project(&fixture_project_path()));
    EngineClient::with_engine_path_and_arguments(fixture_dll_path(), vec![mode.to_owned()])
}

fn fixture_dll_path() -> PathBuf {
    fixture_project_path()
        .parent()
        .expect("the fixture project must have a parent directory")
        .join("bin/Debug/net10.0/ChangeLens.EngineProtocolFixture.dll")
}

fn fixture_project_path() -> PathBuf {
    Path::new(env!("CARGO_MANIFEST_DIR")).join(
        "../../../tests/integration/desktop/EngineStatus/Fixtures/ChangeLens.EngineProtocolFixture/ChangeLens.EngineProtocolFixture.csproj",
    )
}

fn build_dotnet_project(project: &Path) {
    let status = Command::new("dotnet")
        .arg("build")
        .arg(project)
        .arg("--nologo")
        .status()
        .expect("the dotnet CLI should build the fixture project");

    assert!(status.success(), "the fixture project build should pass");
}
