use std::time::Duration;

pub(crate) const COMPARISON_LIST_TARGETS_ACTION: &str = "comparisons.listTargets";
pub(crate) const COMPARISON_PREPARE_ACTION: &str = "comparisons.prepare";
pub(crate) const COMPARISON_CHECK_FRESHNESS_ACTION: &str = "comparisons.checkFreshness";
pub(crate) const COMPARISON_CHECK_REMOTE_BASELINE_ACTION: &str = "comparisons.checkRemoteBaseline";
pub(crate) const COMPARISON_REFRESH_REMOTE_BASELINE_ACTION: &str =
    "comparisons.refreshRemoteBaseline";
pub(crate) const TARGET_DISCOVERY_RESPONSE_TIMEOUT: Duration = Duration::from_secs(15);
pub(crate) const PREPARATION_RESPONSE_TIMEOUT: Duration = Duration::from_secs(35);
pub(crate) const FRESHNESS_RESPONSE_TIMEOUT: Duration = Duration::from_secs(15);
pub(crate) const REMOTE_BASELINE_CHECK_RESPONSE_TIMEOUT: Duration = Duration::from_secs(15);
pub(crate) const REMOTE_BASELINE_REFRESH_RESPONSE_TIMEOUT: Duration = Duration::from_secs(130);
