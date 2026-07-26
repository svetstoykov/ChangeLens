use crate::engine_protocol::EngineActionError;
use raw_window_handle::HasWindowHandle;
use std::path::PathBuf;

/// Defines the native folder-selection capability used by repository commands.
pub trait RepositoryFolderPicker: Send + Sync {
    /// Opens the native folder picker owned by `owner` and returns the selected folder, if any.
    fn select_folder(
        &self,
        owner: &dyn HasWindowHandle,
    ) -> Result<Option<PathBuf>, EngineActionError>;
}
