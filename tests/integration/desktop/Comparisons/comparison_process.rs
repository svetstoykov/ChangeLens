use changelens_desktop_lib::comparisons::{ComparisonService, ComparisonTargetKind};
use changelens_desktop_lib::engine_protocol::{ActionErrorKind, EngineClient, OperationErrorType};
use std::path::{Path, PathBuf};
use std::process::Command;
use std::sync::OnceLock;
use std::time::{Duration, Instant};

const REPOSITORY_PATH: &str = "/projects/change_lens";
const TARGET: &str = "refs/remotes/origin/main";
const FRESHNESS_TOKEN: &str = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

#[test]
fn reuses_one_process_for_list_prepare_and_freshness() {
    let client = client_for_mode("comparison-success");

    let targets = client
        .list_targets(REPOSITORY_PATH, Some("main"), None, None)
        .expect("the target page must parse");
    let process_id = client
        .process_id_for_testing()
        .expect("the first action must start the fixture");
    let prepared = client
        .prepare(REPOSITORY_PATH, TARGET)
        .expect("the prepared comparison must parse");
    let freshness = client
        .check_freshness(REPOSITORY_PATH, TARGET, FRESHNESS_TOKEN)
        .expect("the freshness result must parse");

    assert_eq!(
        targets.targets[0].kind,
        ComparisonTargetKind::RemoteTracking
    );
    assert_eq!(prepared.target.full_name, TARGET);
    assert!(matches!(
        freshness,
        changelens_desktop_lib::comparisons::ComparisonFreshness::Current
    ));
    assert_eq!(client.process_id_for_testing(), Some(process_id));
}

#[test]
fn preserves_ordered_operation_errors_without_restarting() {
    let client = client_for_mode("comparison-ordered-error-once");

    let error = client
        .prepare(REPOSITORY_PATH, TARGET)
        .expect_err("the fixture must fail the first comparison action");
    let process_id = client
        .process_id_for_testing()
        .expect("operation errors must preserve the process");
    client
        .prepare(REPOSITORY_PATH, TARGET)
        .expect("the later explicit action must use the same process");

    assert_eq!(error.kind, ActionErrorKind::Operation);
    assert_eq!(error.request_id.as_deref(), Some("desktop-1"));
    assert_eq!(error.errors.len(), 2);
    assert_eq!(error.errors[0].error_type, OperationErrorType::Validation);
    assert_eq!(error.errors[0].code, "fixture.first");
    assert_eq!(
        error.errors[0].message,
        "The first fixture value is invalid."
    );
    assert_eq!(error.errors[1].error_type, OperationErrorType::Conflict);
    assert_eq!(error.errors[1].code, "fixture.second");
    assert_eq!(client.process_id_for_testing(), Some(process_id));
}

#[test]
fn invalid_comparison_result_invalidates_the_process() {
    let client = client_for_mode("comparison-invalid-result-once");

    let first = client
        .list_targets(REPOSITORY_PATH, None, None, None)
        .expect_err("the malformed target result must fail");
    assert_eq!(client.process_id_for_testing(), None);
    let second = client
        .list_targets(REPOSITORY_PATH, None, None, None)
        .expect_err("a later action must restart the malformed fixture");

    for (error, request_id) in [(first, "desktop-1"), (second, "desktop-2")] {
        assert_eq!(error.kind, ActionErrorKind::Protocol);
        assert_eq!(error.request_id.as_deref(), Some(request_id));
        assert_eq!(error.errors[0].code, "protocol.invalidResponse");
    }
}

#[test]
fn comparison_timeout_is_not_replayed_and_later_explicit_action_restarts() {
    let client = client_for_mode("comparison-delay-first");
    let started_at = Instant::now();

    let error = client
        .list_targets_with_timeout_for_testing(
            REPOSITORY_PATH,
            None,
            None,
            None,
            Duration::from_millis(50),
        )
        .expect_err("the delayed target response must exceed the test deadline");

    assert!(started_at.elapsed() < Duration::from_secs(2));
    assert_eq!(error.kind, ActionErrorKind::Transport);
    assert_eq!(error.request_id.as_deref(), Some("desktop-1"));
    assert_eq!(error.errors[0].code, "engine.responseTimedOut");
    assert_eq!(client.process_id_for_testing(), None);
    client
        .list_targets(REPOSITORY_PATH, None, None, None)
        .expect("a later explicit action must restart the engine process");
}

#[test]
fn every_comparison_capability_exposes_an_independent_timeout_override() {
    let preparation_client = client_for_mode("comparison-delay-first");
    let preparation = preparation_client
        .prepare_with_timeout_for_testing(REPOSITORY_PATH, TARGET, Duration::from_millis(50))
        .expect_err("the delayed preparation response must exceed its test deadline");
    assert_eq!(preparation.errors[0].code, "engine.responseTimedOut");
    assert_eq!(preparation_client.process_id_for_testing(), None);

    let freshness_client = client_for_mode("comparison-delay-first");
    let freshness = freshness_client
        .check_freshness_with_timeout_for_testing(
            REPOSITORY_PATH,
            TARGET,
            FRESHNESS_TOKEN,
            Duration::from_millis(50),
        )
        .expect_err("the delayed freshness response must exceed its test deadline");
    assert_eq!(freshness.errors[0].code, "engine.responseTimedOut");
    assert_eq!(freshness_client.process_id_for_testing(), None);
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
