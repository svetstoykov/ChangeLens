use crate::repositories::RepositoryService;
use std::sync::Arc;

/// Stores the repository service shared by desktop repository commands.
pub struct RepositoryState(Arc<dyn RepositoryService>);

impl RepositoryState {
    /// Creates repository command state backed by `repository_service`.
    pub fn new(repository_service: Arc<dyn RepositoryService>) -> Self {
        Self(repository_service)
    }

    /// Returns the configured repository service.
    pub fn service(&self) -> Arc<dyn RepositoryService> {
        Arc::clone(&self.0)
    }
}
