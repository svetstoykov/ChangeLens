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

#[cfg(test)]
mod tests {
    use super::RepositoryState;
    use crate::engine_protocol::{EngineActionError, EngineClient};
    use crate::repositories::{RepositoryDescriptor, RepositoryService};
    use std::sync::Arc;

    struct RepositoryServiceFixture;

    impl RepositoryService for RepositoryServiceFixture {
        fn open_repository(&self, _path: &str) -> Result<RepositoryDescriptor, EngineActionError> {
            unreachable!("the state test does not execute repository actions")
        }
    }

    #[test]
    fn clones_the_configured_repository_service() {
        let state = RepositoryState::new(Arc::new(RepositoryServiceFixture));

        let first = state.service();
        let second = state.service();

        assert!(Arc::ptr_eq(&first, &second));
    }

    #[test]
    fn accepts_the_engine_client() {
        let _state = RepositoryState::new(Arc::new(EngineClient::new()));
    }
}
