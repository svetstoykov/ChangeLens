use changelens_desktop_lib::engine_protocol::{ActionErrorKind, EngineClient, OperationErrorType};
use changelens_desktop_lib::engine_status::EngineStatusService;
use std::fs;
use std::path::{Path, PathBuf};
use std::process::Command;
use std::sync::OnceLock;
use std::time::{Duration, Instant, SystemTime, UNIX_EPOCH};

#[test]
fn checks_status_with_the_real_dotnet_engine() {
    build_dotnet_project(&engine_project_path());
    let engine_client = EngineClient::new();

    engine_client
        .check_status()
        .expect("the controlled .NET engine should report ready status");
}

#[test]
fn reports_engine_start_failure() {
    let client = EngineClient::with_process_configuration(
        fixture_dll_path(),
        Vec::new(),
        PathBuf::from("missing-dotnet-fixture-command"),
    );

    let error = client
        .check_status()
        .expect_err("a missing runtime must prevent process startup");

    assert_eq!(error.kind, ActionErrorKind::Transport);
    assert_eq!(error.errors[0].code, "engine.startFailed");
}

#[test]
fn restarts_only_after_a_later_explicit_action() {
    let client = client_for_mode("timeout-once");

    let first = client
        .check_status()
        .expect_err("the first request must time out without replay");
    client
        .check_status()
        .expect("the later explicit action must start a fresh process");

    assert_eq!(first.kind, ActionErrorKind::Transport);
    assert_eq!(first.request_id.as_deref(), Some("desktop-1"));
    assert_eq!(first.errors[0].code, "engine.responseTimedOut");
}

#[test]
fn valid_engine_error_does_not_invalidate_process() {
    let client = client_for_mode("ordered-error-once");

    let first = client
        .check_status()
        .expect_err("the fixture must return its ordered errors first");
    client
        .check_status()
        .expect("the same fixture process must handle the second request");

    assert_eq!(first.kind, ActionErrorKind::Operation);
    assert_eq!(first.errors[0].code, "fixture.first");
    assert_eq!(first.errors[1].code, "fixture.second");
}

#[test]
fn uncorrelated_engine_error_does_not_invalidate_process() {
    let client = client_for_mode("uncorrelated-error-once");

    let first = client
        .check_status()
        .expect_err("the fixture must return an uncorrelated error first");
    client
        .check_status()
        .expect("the same fixture process must handle the second request");

    assert_eq!(first.kind, ActionErrorKind::Operation);
    assert_eq!(first.request_id, None);
    assert_eq!(first.errors.len(), 1);
    assert_eq!(first.errors[0].code, "protocol.invalidRequest");
}

#[test]
fn invalidates_process_for_unsafe_protocol_and_transport_failures() {
    for (mode, kind, code) in [
        ("exit", ActionErrorKind::Transport, "engine.exited"),
        (
            "invalid-json",
            ActionErrorKind::Protocol,
            "protocol.invalidResponse",
        ),
        (
            "invalid-utf8",
            ActionErrorKind::Protocol,
            "protocol.invalidResponse",
        ),
        (
            "oversized",
            ActionErrorKind::Transport,
            "engine.responseTooLarge",
        ),
        (
            "correlation",
            ActionErrorKind::Protocol,
            "protocol.correlationMismatch",
        ),
    ] {
        let client = client_for_mode(mode);
        let first = client
            .check_status()
            .expect_err("the unsafe fixture response must fail");
        let second = client
            .check_status()
            .expect_err("a later action must use a fresh fixture process and fail again");

        assert_eq!(first.kind, kind);
        assert_eq!(first.errors[0].code, code);
        assert_eq!(first.request_id.as_deref(), Some("desktop-1"));
        assert_eq!(second.request_id.as_deref(), Some("desktop-2"));
    }
}

#[test]
fn shutdown_closes_input_and_allows_cooperative_exit() {
    let client = client_for_mode("exit-on-eof");
    client
        .check_status()
        .expect("the fixture process must start successfully");
    let process_id = client
        .process_id_for_testing()
        .expect("the client must retain the running fixture process");

    let started_at = Instant::now();
    client.shutdown();

    assert!(started_at.elapsed() < Duration::from_secs(2));
    assert!(!process_exists(process_id));
    assert_eq!(client.process_id_for_testing(), None);
}

#[test]
fn shutdown_closes_input_before_cooperative_process_termination() {
    let marker_path = unique_fixture_path("shutdown-eof");
    let client = client_for_mode_with_arguments(
        "record-eof",
        vec![marker_path.to_string_lossy().into_owned()],
    );
    client
        .check_status()
        .expect("the fixture process must start successfully");

    client.shutdown();

    assert_eq!(
        fs::read_to_string(&marker_path).expect("the fixture must record observed EOF"),
        "eof"
    );
    remove_fixture_file(&marker_path);
}

#[test]
fn shutdown_force_terminates_and_reaps_uncooperative_process() {
    let client = client_for_mode("ignore-eof");
    client
        .check_status()
        .expect("the fixture process must start successfully");
    let process_id = client
        .process_id_for_testing()
        .expect("the client must retain the running fixture process");

    let started_at = Instant::now();
    client.shutdown();
    let elapsed = started_at.elapsed();

    assert!(elapsed >= Duration::from_millis(1_800));
    assert!(elapsed < Duration::from_secs(5));
    assert!(!process_exists(process_id));
    assert_eq!(client.process_id_for_testing(), None);
}

#[test]
fn shutdown_joins_response_reader_in_cooperative_and_forced_paths() {
    for mode in ["exit-on-eof", "ignore-eof"] {
        let client = client_for_mode(mode);
        client
            .check_status()
            .expect("the fixture process must start successfully");
        let process_id = client
            .process_id_for_testing()
            .expect("the client must retain the running fixture process");

        client.shutdown();

        assert!(!process_exists(process_id));
        assert_eq!(client.process_id_for_testing(), None);
    }
}

#[test]
fn repeated_shutdown_is_safe_and_bounded() {
    let client = client_for_mode("exit-on-eof");
    client
        .check_status()
        .expect("the fixture process must start successfully");

    client.shutdown();
    let repeated_shutdown_started_at = Instant::now();
    client.shutdown();

    assert!(repeated_shutdown_started_at.elapsed() < Duration::from_millis(100));
    assert_eq!(client.process_id_for_testing(), None);
}

#[test]
fn action_after_shutdown_is_rejected_without_starting_process() {
    let client = client_for_mode("exit-on-eof");

    client.shutdown();
    let error = client
        .check_status()
        .expect_err("an action after shutdown must be rejected");

    assert_eq!(error.kind, ActionErrorKind::Operation);
    assert_eq!(error.request_id, None);
    assert_eq!(
        error.errors[0].error_type,
        OperationErrorType::InvalidOperation
    );
    assert_eq!(error.errors[0].code, "engine.shuttingDown");
    assert_eq!(
        error.errors[0].message,
        "The desktop is shutting down the Engine."
    );
    assert_eq!(client.process_id_for_testing(), None);
}

#[test]
fn dropping_client_uses_graceful_shutdown_path() {
    let marker_path = unique_fixture_path("drop-eof");
    let client = client_for_mode_with_arguments(
        "record-eof",
        vec![marker_path.to_string_lossy().into_owned()],
    );
    client
        .check_status()
        .expect("the fixture process must start successfully");
    let process_id = client
        .process_id_for_testing()
        .expect("the client must retain the running fixture process");

    drop(client);

    assert!(!process_exists(process_id));
    assert_eq!(
        fs::read_to_string(&marker_path).expect("drop must allow the fixture to observe EOF"),
        "eof"
    );
    remove_fixture_file(&marker_path);
}

#[test]
fn shutdown_does_not_start_replacement_for_invalidated_process() {
    let client = client_for_mode("exit");

    client
        .check_status()
        .expect_err("the fixture must invalidate the process by exiting");
    assert_eq!(client.process_id_for_testing(), None);

    client.shutdown();

    assert_eq!(client.process_id_for_testing(), None);
}

fn client_for_mode(mode: &str) -> EngineClient {
    client_for_mode_with_arguments(mode, Vec::new())
}

fn client_for_mode_with_arguments(mode: &str, arguments: Vec<String>) -> EngineClient {
    static FIXTURE_BUILD: OnceLock<()> = OnceLock::new();
    FIXTURE_BUILD.get_or_init(|| build_dotnet_project(&fixture_project_path()));
    EngineClient::with_engine_path_and_arguments(
        fixture_dll_path(),
        std::iter::once(mode.to_owned()).chain(arguments).collect(),
    )
}

fn fixture_dll_path() -> PathBuf {
    fixture_project_path()
        .parent()
        .expect("the fixture project must have a parent directory")
        .join("bin/Debug/net10.0/ChangeLens.EngineProtocolFixture.dll")
}

fn engine_project_path() -> PathBuf {
    Path::new(env!("CARGO_MANIFEST_DIR"))
        .join("../../engine/ChangeLens.Engine/ChangeLens.Engine.csproj")
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
        .expect("the dotnet CLI should build the requested project");

    assert!(
        build_status.success(),
        "the dotnet project build should pass"
    );
}

fn unique_fixture_path(name: &str) -> PathBuf {
    let unique_value = SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .expect("the system clock must be after the Unix epoch")
        .as_nanos();

    std::env::temp_dir().join(format!(
        "changelens-{name}-{}-{unique_value}.txt",
        std::process::id()
    ))
}

fn remove_fixture_file(path: &Path) {
    fs::remove_file(path).expect("the fixture file must be removable");
}

#[cfg(unix)]
fn process_exists(process_id: u32) -> bool {
    Command::new("kill")
        .args(["-0", &process_id.to_string()])
        .status()
        .is_ok_and(|status| status.success())
}

#[cfg(windows)]
fn process_exists(process_id: u32) -> bool {
    Command::new("tasklist")
        .args(["/FI", &format!("PID eq {process_id}")])
        .output()
        .is_ok_and(|output| {
            String::from_utf8_lossy(&output.stdout).contains(&process_id.to_string())
        })
}
