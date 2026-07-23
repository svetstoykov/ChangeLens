use crate::engine_protocol::{EngineActionError, EngineClient};
use crate::repositories::constants::{REPOSITORY_OPEN_ACTION, REPOSITORY_RESPONSE_TIMEOUT};
use crate::repositories::{
    RepositoryDescriptor, RepositoryOpenParameters, RepositoryOpenResult, RepositoryService,
};
use std::time::Duration;

impl RepositoryService for EngineClient {
    fn open_repository(&self, path: &str) -> Result<RepositoryDescriptor, EngineActionError> {
        self.open_repository_with_timeout(path, REPOSITORY_RESPONSE_TIMEOUT)
    }
}

impl EngineClient {
    #[doc(hidden)]
    pub fn open_repository_with_timeout_for_testing(
        &self,
        path: &str,
        timeout: Duration,
    ) -> Result<RepositoryDescriptor, EngineActionError> {
        self.open_repository_with_timeout(path, timeout)
    }

    fn open_repository_with_timeout(
        &self,
        path: &str,
        timeout: Duration,
    ) -> Result<RepositoryDescriptor, EngineActionError> {
        let result = self.execute_action::<_, RepositoryOpenResult>(
            REPOSITORY_OPEN_ACTION,
            Some(RepositoryOpenParameters { path }),
            timeout,
        )?;

        Ok(result.repository)
    }
}

#[cfg(test)]
mod tests {
    use crate::engine_protocol::EngineClient;
    use crate::repositories::RepositoryService;

    #[test]
    fn engine_client_implements_repository_service() {
        fn assert_implementation<T: RepositoryService>() {}

        assert_implementation::<EngineClient>();
    }
}
