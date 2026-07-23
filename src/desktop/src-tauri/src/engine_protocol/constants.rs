use std::time::Duration;

pub(crate) const ENGINE_SHUTTING_DOWN_CODE: &str = "engine.shuttingDown";
pub(crate) const ENGINE_SHUTTING_DOWN_MESSAGE: &str = "The desktop is shutting down the Engine.";
pub(crate) const ENGINE_SHUTDOWN_FORCED_CODE: &str = "engine.shutdownForced";
pub(crate) const GRACEFUL_SHUTDOWN_TIMEOUT: Duration = Duration::from_secs(2);
