use crate::comparisons::ComparisonService;
use std::sync::Arc;

/// Stores the comparison service shared by desktop comparison commands.
pub struct ComparisonState(Arc<dyn ComparisonService>);

impl ComparisonState {
    /// Creates comparison command state backed by `comparison_service`.
    pub fn new(comparison_service: Arc<dyn ComparisonService>) -> Self {
        Self(comparison_service)
    }

    /// Returns the configured comparison service.
    pub fn service(&self) -> Arc<dyn ComparisonService> {
        Arc::clone(&self.0)
    }
}
