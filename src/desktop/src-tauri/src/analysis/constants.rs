use std::time::Duration;

pub(crate) const ANALYSIS_START_ACTION: &str = "analysis.start";
pub(crate) const ANALYSIS_GET_ACTIVE_ACTION: &str = "analysis.getActive";
pub(crate) const ANALYSIS_POLL_RUN_ACTION: &str = "analysis.pollRun";
pub(crate) const ANALYSIS_CANCEL_ACTION: &str = "analysis.cancel";
pub(crate) const ANALYSIS_START_RESPONSE_TIMEOUT: Duration = Duration::from_secs(15);
pub(crate) const ANALYSIS_OBSERVATION_RESPONSE_TIMEOUT: Duration = Duration::from_secs(5);
