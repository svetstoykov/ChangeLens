use changelens_desktop_lib::engine_information::EngineClient;
use std::path::{Path, PathBuf};
use std::process::Command;

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
fn restarts_the_engine_after_a_response_timeout() {
    let fixture_project = fixture_project_path();
    build_dotnet_project(&fixture_project);
    let fixture_dll = fixture_project
        .parent()
        .expect("the fixture project should have a parent directory")
        .join("bin/Debug/net10.0/ChangeLens.EngineProtocolFixture.dll");
    let engine_client = EngineClient::with_engine_path(fixture_dll);

    let first_error = engine_client
        .get_information()
        .expect_err("the first fixture process should exceed the response deadline");
    let information = engine_client
        .get_information()
        .expect("a fresh fixture process should handle the next request");

    assert_eq!(first_error.code, "engine.responseTimedOut");
    assert_eq!(information.name, "ChangeLens.Engine");
    assert_eq!(information.protocol_version, 1);
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
