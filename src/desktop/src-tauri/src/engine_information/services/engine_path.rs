use crate::engine_information::EngineCommandError;
use std::env;
use std::path::{Path, PathBuf};

pub(crate) fn resolve_engine_path() -> Result<PathBuf, EngineCommandError> {
    if let Some(configured_path) = env::var_os("CHANGELENS_ENGINE_PATH") {
        return Ok(PathBuf::from(configured_path));
    }

    if cfg!(debug_assertions) {
        return Ok(Path::new(env!("CARGO_MANIFEST_DIR"))
            .join("../../engine/ChangeLens.Engine/bin/Debug/net10.0/ChangeLens.Engine.dll"));
    }

    Err(EngineCommandError::new(
        "engine.pathUnavailable",
        "CHANGELENS_ENGINE_PATH must point to the packaged engine in release builds.",
    ))
}
