use crate::comparisons::{
    ComparisonFreshness, ComparisonRefreshRemoteBaselineResult, ComparisonRemoteBaseline,
    ComparisonTargetPage, PreparedComparison,
};
use crate::engine_protocol::EngineActionError;

/// Defines comparison actions provided by the local analysis engine.
pub trait ComparisonService: Send + Sync {
    /// Lists local and cached remote-tracking comparison targets for a repository.
    fn list_targets(
        &self,
        path: &str,
        query: Option<&str>,
        after: Option<&str>,
        target_set_token: Option<&str>,
    ) -> Result<ComparisonTargetPage, EngineActionError>;

    /// Prepares the exact comparison against the selected target.
    fn prepare(&self, path: &str, target: &str) -> Result<PreparedComparison, EngineActionError>;

    /// Checks whether an already prepared comparison remains current.
    fn check_freshness(
        &self,
        path: &str,
        target: &str,
        freshness_token: &str,
    ) -> Result<ComparisonFreshness, EngineActionError>;

    /// Checks whether a cached remote-tracking comparison baseline matches the server's branch.
    fn check_remote_baseline(
        &self,
        path: &str,
        target: &str,
    ) -> Result<ComparisonRemoteBaseline, EngineActionError>;

    /// Fetches the selected branch to refresh its cached remote-tracking comparison baseline.
    fn refresh_remote_baseline(
        &self,
        path: &str,
        target: &str,
    ) -> Result<ComparisonRefreshRemoteBaselineResult, EngineActionError>;
}
