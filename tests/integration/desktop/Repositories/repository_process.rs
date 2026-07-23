use changelens_desktop_lib::engine_protocol::{ActionErrorKind, EngineClient, OperationErrorType};
use changelens_desktop_lib::repositories::RepositoryService;
use std::fs;
use std::path::{Path, PathBuf};
use std::process::Command;
use std::sync::OnceLock;
use std::time::{Duration, Instant, SystemTime};

const REPOSITORY_PATH: &str = "/projects/change_lens";
const BRANCH_RESULT_FIXTURE: &str = include_str!(concat!(
    env!("CARGO_MANIFEST_DIR"),
    "/../../../contracts/engine-protocol/v1/fixtures/repositories-open.branch.result.json"
));
const DETACHED_RESULT_FIXTURE: &str = include_str!(concat!(
    env!("CARGO_MANIFEST_DIR"),
    "/../../../contracts/engine-protocol/v1/fixtures/repositories-open.detached.result.json"
));

#[test]
fn sends_the_exact_request_and_parses_the_branch_fixture() {
    let client = client_for_mode("repository-branch");

    let repository = client
        .open_repository(REPOSITORY_PATH)
        .expect("the canonical branch result must parse");

    assert_repository_matches_fixture(repository, BRANCH_RESULT_FIXTURE);
}

#[test]
fn parses_the_detached_fixture() {
    let client = client_for_mode("repository-detached");

    let repository = client
        .open_repository(REPOSITORY_PATH)
        .expect("the canonical detached result must parse");

    assert_repository_matches_fixture(repository, DETACHED_RESULT_FIXTURE);
}

#[test]
fn preserves_ordered_engine_errors_and_reuses_the_process() {
    let client = client_for_mode("repository-ordered-error-once");

    let first = client
        .open_repository(REPOSITORY_PATH)
        .expect_err("the fixture must return ordered errors first");
    let process_id = client
        .process_id_for_testing()
        .expect("an operation error must leave the fixture process available");
    client
        .open_repository(REPOSITORY_PATH)
        .expect("the same process must handle a later repository action");

    assert_eq!(client.process_id_for_testing(), Some(process_id));
    assert_eq!(first.kind, ActionErrorKind::Operation);
    assert_eq!(first.request_id.as_deref(), Some("desktop-1"));
    assert_eq!(first.errors.len(), 2);
    assert_eq!(first.errors[0].error_type, OperationErrorType::Validation);
    assert_eq!(first.errors[0].code, "fixture.first");
    assert_eq!(
        first.errors[0].message,
        "The first fixture value is invalid."
    );
    assert_eq!(first.errors[1].error_type, OperationErrorType::Conflict);
    assert_eq!(first.errors[1].code, "fixture.second");
    assert_eq!(
        first.errors[1].message,
        "The second fixture value conflicts with current state."
    );
}

#[test]
fn invalid_typed_results_invalidate_the_process() {
    for mode in [
        "repository-wrong-kind-once",
        "repository-detached-name-once",
        "repository-branch-missing-name-once",
        "repository-blank-name-once",
        "repository-blank-path-once",
        "repository-blank-branch-once",
        "repository-uppercase-revision-once",
        "repository-short-revision-once",
        "repository-nonhex-revision-once",
    ] {
        let client = client_for_mode(mode);

        let first = client
            .open_repository(REPOSITORY_PATH)
            .expect_err("the malformed success result must fail");
        assert_eq!(client.process_id_for_testing(), None);
        let second = client
            .open_repository(REPOSITORY_PATH)
            .expect_err("the later action must start a new fixture process and fail again");

        assert_invalid_response(&first, "desktop-1");
        assert_invalid_response(&second, "desktop-2");
        assert_eq!(client.process_id_for_testing(), None);
    }
}

#[test]
fn repository_timeout_is_not_replayed_and_a_later_action_restarts() {
    let request_log = RequestLog::new();
    let client = client_for_mode_with_arguments(
        "repository-delay-first",
        vec![
            request_log
                .path
                .to_str()
                .expect("the fixture log path must be Unicode")
                .to_owned(),
        ],
    );
    let started_at = Instant::now();

    let first = client
        .open_repository_with_timeout_for_testing(REPOSITORY_PATH, Duration::from_millis(50))
        .expect_err("the delayed repository response must exceed the test deadline");

    assert!(started_at.elapsed() < Duration::from_secs(2));
    assert_eq!(request_log.request_count(), 1);
    assert_eq!(first.kind, ActionErrorKind::Transport);
    assert_eq!(first.request_id.as_deref(), Some("desktop-1"));
    assert_eq!(first.errors[0].error_type, OperationErrorType::Timeout);
    assert_eq!(first.errors[0].code, "engine.responseTimedOut");
    assert_eq!(client.process_id_for_testing(), None);

    client
        .open_repository(REPOSITORY_PATH)
        .expect("a later explicit action must start a fresh process");
    assert_eq!(request_log.request_count(), 2);
}

fn assert_invalid_response(
    error: &changelens_desktop_lib::engine_protocol::EngineActionError,
    request_id: &str,
) {
    assert_eq!(error.kind, ActionErrorKind::Protocol);
    assert_eq!(error.request_id.as_deref(), Some(request_id));
    assert_eq!(error.errors.len(), 1);
    assert_eq!(error.errors[0].code, "protocol.invalidResponse");
}

fn assert_repository_matches_fixture(
    repository: changelens_desktop_lib::repositories::RepositoryDescriptor,
    fixture: &str,
) {
    let actual = serde_json::to_value(repository).expect("the repository must serialize");
    let expected: serde_json::Value =
        serde_json::from_str(fixture).expect("the shared result fixture must contain JSON");

    assert_eq!(actual, expected["result"]["repository"]);
}

fn client_for_mode(mode: &str) -> EngineClient {
    client_for_mode_with_arguments(mode, Vec::new())
}

fn client_for_mode_with_arguments(mode: &str, fixture_arguments: Vec<String>) -> EngineClient {
    static FIXTURE_BUILD: OnceLock<()> = OnceLock::new();
    FIXTURE_BUILD.get_or_init(|| build_dotnet_project(&fixture_project_path()));
    let mut arguments = vec![mode.to_owned()];
    arguments.extend(fixture_arguments);
    EngineClient::with_engine_path_and_arguments(fixture_dll_path(), arguments)
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
    let build_status = Command::new("dotnet")
        .arg("build")
        .arg(project)
        .arg("--nologo")
        .status()
        .expect("the dotnet CLI should build the fixture project");

    assert!(
        build_status.success(),
        "the fixture project build should pass"
    );
}

struct RequestLog {
    path: PathBuf,
}

impl RequestLog {
    fn new() -> Self {
        let unique_value = SystemTime::now()
            .duration_since(SystemTime::UNIX_EPOCH)
            .expect("the system clock must be after the Unix epoch")
            .as_nanos();
        let path = std::env::temp_dir().join(format!(
            "changelens-repository-requests-{}-{unique_value}.log",
            std::process::id()
        ));

        Self { path }
    }

    fn request_count(&self) -> usize {
        fs::read_to_string(&self.path)
            .expect("the fixture must create the repository request log")
            .lines()
            .count()
    }
}

impl Drop for RequestLog {
    fn drop(&mut self) {
        let _ = fs::remove_file(&self.path);
    }
}
