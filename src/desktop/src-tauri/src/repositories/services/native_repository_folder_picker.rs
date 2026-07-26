use crate::engine_protocol::{EngineActionError, OperationErrorType};
use crate::repositories::RepositoryFolderPicker;
use raw_window_handle::{HandleError, HasWindowHandle, WindowHandle};
use std::path::PathBuf;

struct DialogOwner<'a>(&'a dyn HasWindowHandle);

impl HasWindowHandle for DialogOwner<'_> {
    fn window_handle(&self) -> Result<WindowHandle<'_>, HandleError> {
        self.0.window_handle()
    }
}

/// Selects repository folders through the operating system's native dialog.
pub struct NativeRepositoryFolderPicker;

impl RepositoryFolderPicker for NativeRepositoryFolderPicker {
    fn select_folder(
        &self,
        owner: &dyn HasWindowHandle,
    ) -> Result<Option<PathBuf>, EngineActionError> {
        let dialog_owner = DialogOwner(owner);

        native_dialog::DialogBuilder::file()
            .set_title("Open a repository")
            .set_owner(&dialog_owner)
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
