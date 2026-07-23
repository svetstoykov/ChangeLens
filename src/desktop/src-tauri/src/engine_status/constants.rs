use std::time::Duration;

pub(crate) const ENGINE_STATUS_ACTION: &str = "engine.checkStatus";
pub(crate) const ENGINE_STATUS_RESPONSE_TIMEOUT: Duration = Duration::from_secs(5);
