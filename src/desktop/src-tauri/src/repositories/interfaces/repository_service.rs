use crate::engine_protocol::EngineActionError;
use crate::repositories::{
    RepositoryDescriptor, RepositoryHistory, RepositoryOpenResult, RepositoryRestoreResult,
};

/// Defines repository actions provided by the local analysis engine.
pub trait RepositoryService: Send + Sync {
    /// Opens and inspects the repository selected by `path`.
    fn open_repository(&self, path: &str) -> Result<RepositoryDescriptor, EngineActionError>;

    /// Opens a repository and returns its durable history metadata.
    fn open_repository_record(
        &self,
        path: &str,
    ) -> Result<RepositoryOpenResult, EngineActionError> {
        self.open_repository(path)
            .map(|repository| RepositoryOpenResult {
                repository_id: "00000000-0000-0000-0000-000000000000".to_owned(),
                repository,
                preferred_target: None,
            })
    }

    /// Restores and revalidates the last selected repository.
    fn restore_last_repository(&self) -> Result<RepositoryRestoreResult, EngineActionError> {
        unreachable!("repository restoration is not configured for this service")
    }

    /// Lists retained recent repository metadata without revalidation.
    fn list_recent_repositories(&self) -> Result<RepositoryHistory, EngineActionError> {
        unreachable!("repository history is not configured for this service")
    }

    /// Removes one retained repository-history entry.
    fn remove_recent_repository(&self, _repository_id: &str) -> Result<(), EngineActionError> {
        unreachable!("repository history is not configured for this service")
    }
}

#[cfg(test)]
mod tests {
    use super::RepositoryService;

    fn assert_service_contract<T: RepositoryService>() {}

    #[test]
    fn defines_a_send_and_sync_repository_boundary() {
        assert_service_contract::<crate::engine_protocol::EngineClient>();
    }
}
