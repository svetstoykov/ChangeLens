use crate::analysis::{AnalysisGetActiveResult, AnalysisRunProjection, AnalysisStartResult};
use crate::engine_protocol::EngineActionError;

/// Defines analysis-run actions provided by the local analysis engine.
pub trait AnalysisService: Send + Sync {
    /// Starts an analysis run for a prepared comparison.
    fn start(
        &self,
        path: &str,
        target: &str,
        freshness_token: &str,
        change_context: Option<&str>,
    ) -> Result<AnalysisStartResult, EngineActionError>;

    /// Looks up the active analysis run for a repository, if one exists.
    fn get_active(&self, path: &str) -> Result<AnalysisGetActiveResult, EngineActionError>;

    /// Polls the current projection of one analysis run.
    fn poll_run(&self, run_id: &str) -> Result<AnalysisRunProjection, EngineActionError>;

    /// Requests cancellation of one analysis run.
    fn cancel(&self, run_id: &str) -> Result<(), EngineActionError>;
}
