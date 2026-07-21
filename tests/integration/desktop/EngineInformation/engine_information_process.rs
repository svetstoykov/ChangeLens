use changelens_desktop_lib::engine_information::EngineClient;
use changelens_desktop_lib::engine_protocol::ActionErrorKind;
use std::path::{Path, PathBuf};
use std::process::Command;
use std::sync::OnceLock;

#[test]
fn gets_information_from_the_real_dotnet_engine() {
    build_dotnet_project(&engine_project_path());
    let engine_client = EngineClient::new();

    let information = engine_client
        .get_information()
        .expect("the controlled .NET engine should return its information");

    assert_eq!(information.name, "ChangeLens.Engine");
    assert_eq!(information.version, "0.1.0");
    assert_eq!(information.protocol_version, 1);
}

#[test]
fn reports_engine_start_failure() {
    let client = EngineClient::with_process_configuration(
        fixture_dll_path(),
        Vec::new(),
        PathBuf::from("missing-dotnet-fixture-command"),
    );

    let error = client
        .get_information()
        .expect_err("a missing runtime must prevent process startup");

    assert_eq!(error.kind, ActionErrorKind::Transport);
    assert_eq!(error.errors[0].code, "engine.startFailed");
}

#[test]
fn restarts_only_after_a_later_explicit_action() {
    let client = client_for_mode("timeout-once");

    let first = client
        .get_information()
        .expect_err("the first request must time out without replay");
    let second = client
        .get_information()
        .expect("the later explicit action must start a fresh process");

    assert_eq!(first.kind, ActionErrorKind::Transport);
    assert_eq!(first.request_id.as_deref(), Some("desktop-1"));
    assert_eq!(first.errors[0].code, "engine.responseTimedOut");
    assert_eq!(second.name, "ChangeLens.Engine");
}

#[test]
fn valid_engine_error_does_not_invalidate_process() {
    let client = client_for_mode("ordered-error-once");

    let first = client
        .get_information()
        .expect_err("the fixture must return its ordered errors first");
    let second = client
        .get_information()
        .expect("the same fixture process must handle the second request");

    assert_eq!(first.kind, ActionErrorKind::Operation);
    assert_eq!(first.errors[0].code, "fixture.first");
    assert_eq!(first.errors[1].code, "fixture.second");
    assert_eq!(second.protocol_version, 1);
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
            .get_information()
            .expect_err("the unsafe fixture response must fail");
        let second = client
            .get_information()
            .expect_err("a later action must use a fresh fixture process and fail again");

        assert_eq!(first.kind, kind);
        assert_eq!(first.errors[0].code, code);
        assert_eq!(first.request_id.as_deref(), Some("desktop-1"));
        assert_eq!(second.request_id.as_deref(), Some("desktop-2"));
    }
}

#[test]
fn dropping_client_cleans_up_child_process() {
    let client = client_for_mode("success");
    client
        .get_information()
        .expect("the fixture process must start successfully");
    let process_id = client
        .process_id_for_testing()
        .expect("the client must retain the running fixture process");

    drop(client);

    for _ in 0..20 {
        if !process_exists(process_id) {
            return;
        }
        std::thread::sleep(std::time::Duration::from_millis(50));
    }

    assert!(!process_exists(process_id));
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

fn engine_project_path() -> PathBuf {
    Path::new(env!("CARGO_MANIFEST_DIR"))
        .join("../../engine/ChangeLens.Engine/ChangeLens.Engine.csproj")
}

fn fixture_project_path() -> PathBuf {
    Path::new(env!("CARGO_MANIFEST_DIR")).join(
        "../../../tests/integration/desktop/EngineInformation/Fixtures/ChangeLens.EngineProtocolFixture/ChangeLens.EngineProtocolFixture.csproj",
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
