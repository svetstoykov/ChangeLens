use crate::engine_protocol::{EngineActionError, EngineClient};
use crate::repositories::constants::{
    REPOSITORY_LIST_RECENT_ACTION, REPOSITORY_OPEN_ACTION, REPOSITORY_REMOVE_RECENT_ACTION,
    REPOSITORY_RESPONSE_TIMEOUT, REPOSITORY_RESTORE_LAST_ACTION,
};
use crate::repositories::{
    RepositoryDescriptor, RepositoryHistory, RepositoryOpenParameters, RepositoryOpenResult,
    RepositoryRemoveRecentParameters, RepositoryRestoreResult, RepositoryService,
};
use serde::Deserialize;
use std::time::Duration;

impl RepositoryService for EngineClient {
    fn open_repository(&self, path: &str) -> Result<RepositoryDescriptor, EngineActionError> {
        self.open_repository_with_timeout(path, REPOSITORY_RESPONSE_TIMEOUT)
            .map(|result| result.repository)
    }

    fn open_repository_record(
        &self,
        path: &str,
    ) -> Result<RepositoryOpenResult, EngineActionError> {
        self.open_repository_with_timeout(path, REPOSITORY_RESPONSE_TIMEOUT)
    }

    fn restore_last_repository(&self) -> Result<RepositoryRestoreResult, EngineActionError> {
        self.execute_action::<Option<()>, RepositoryRestoreResult>(
            REPOSITORY_RESTORE_LAST_ACTION,
            None,
            REPOSITORY_RESPONSE_TIMEOUT,
        )
    }

    fn list_recent_repositories(&self) -> Result<RepositoryHistory, EngineActionError> {
        self.execute_action::<Option<()>, RepositoryHistory>(
            REPOSITORY_LIST_RECENT_ACTION,
            None,
            REPOSITORY_RESPONSE_TIMEOUT,
        )
    }

    fn remove_recent_repository(&self, repository_id: &str) -> Result<(), EngineActionError> {
        self.execute_action::<_, NullResult>(
            REPOSITORY_REMOVE_RECENT_ACTION,
            Some(RepositoryRemoveRecentParameters { repository_id }),
            REPOSITORY_RESPONSE_TIMEOUT,
        )
        .map(|_| ())
    }
}

impl EngineClient {
    #[doc(hidden)]
    pub fn open_repository_with_timeout_for_testing(
        &self,
        path: &str,
        timeout: Duration,
    ) -> Result<RepositoryOpenResult, EngineActionError> {
        self.open_repository_with_timeout(path, timeout)
    }

    fn open_repository_with_timeout(
        &self,
        path: &str,
        timeout: Duration,
    ) -> Result<RepositoryOpenResult, EngineActionError> {
        self.execute_action::<_, RepositoryOpenResult>(
            REPOSITORY_OPEN_ACTION,
            Some(RepositoryOpenParameters { path }),
            timeout,
        )
    }
}

#[derive(Deserialize)]
struct NullResult;
