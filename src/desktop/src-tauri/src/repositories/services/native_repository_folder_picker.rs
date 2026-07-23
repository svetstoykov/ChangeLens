use crate::engine_protocol::{EngineActionError, OperationErrorType};
use crate::repositories::RepositoryFolderPicker;
use std::path::PathBuf;

/// Selects repository folders through the operating system's native dialog.
pub struct NativeRepositoryFolderPicker;

impl RepositoryFolderPicker for NativeRepositoryFolderPicker {
    fn select_folder(&self) -> Result<Option<PathBuf>, EngineActionError> {
        native_dialog::DialogBuilder::file()
            .set_title("Open a repository")
            .open_single_dir()
            .show()
            .map_err(|_| {
                EngineActionError::transport(
                    None,
                    "desktop.folderPickerUnavailable",
                    OperationErrorType::ExternalDependencyFailure,
                    "The desktop folder picker is unavailable.",
                )
            })
    }
}
