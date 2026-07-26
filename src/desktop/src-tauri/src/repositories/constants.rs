use std::time::Duration;

pub(crate) const REPOSITORY_OPEN_ACTION: &str = "repositories.open";
pub(crate) const REPOSITORY_RESTORE_LAST_ACTION: &str = "repositories.restoreLast";
pub(crate) const REPOSITORY_LIST_RECENT_ACTION: &str = "repositories.listRecent";
pub(crate) const REPOSITORY_REMOVE_RECENT_ACTION: &str = "repositories.removeRecent";
pub(crate) const REPOSITORY_RESPONSE_TIMEOUT: Duration = Duration::from_secs(20);
