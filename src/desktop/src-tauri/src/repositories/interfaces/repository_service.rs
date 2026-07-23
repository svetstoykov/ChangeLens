use crate::engine_protocol::EngineActionError;
use crate::repositories::RepositoryDescriptor;

/// Defines repository actions provided by the local analysis engine.
pub trait RepositoryService: Send + Sync {
    /// Opens and inspects the repository selected by `path`.
    fn open_repository(&self, path: &str) -> Result<RepositoryDescriptor, EngineActionError>;
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
