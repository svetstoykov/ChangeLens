use crate::analysis::AnalysisService;
use std::sync::Arc;

/// Stores the analysis service shared by desktop analysis commands.
pub struct AnalysisState(Arc<dyn AnalysisService>);

impl AnalysisState {
    /// Creates analysis command state backed by `analysis_service`.
    pub fn new(analysis_service: Arc<dyn AnalysisService>) -> Self {
        Self(analysis_service)
    }

    /// Returns the configured analysis service.
    pub fn service(&self) -> Arc<dyn AnalysisService> {
        Arc::clone(&self.0)
    }
}
