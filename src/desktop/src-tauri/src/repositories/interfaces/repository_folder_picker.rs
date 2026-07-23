use crate::engine_protocol::EngineActionError;
use std::path::PathBuf;

/// Defines the native folder-selection capability used by repository commands.
pub trait RepositoryFolderPicker: Send + Sync {
    /// Opens the native folder picker and returns the selected folder, if any.
    fn select_folder(&self) -> Result<Option<PathBuf>, EngineActionError>;
}
