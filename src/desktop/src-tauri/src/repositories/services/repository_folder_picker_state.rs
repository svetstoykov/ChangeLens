use crate::repositories::RepositoryFolderPicker;
use std::sync::Arc;

/// Stores the folder picker shared by desktop repository commands.
pub struct RepositoryFolderPickerState(Arc<dyn RepositoryFolderPicker>);

impl RepositoryFolderPickerState {
    /// Creates repository folder-picker state backed by `folder_picker`.
    pub fn new(folder_picker: Arc<dyn RepositoryFolderPicker>) -> Self {
        Self(folder_picker)
    }

    /// Returns the configured repository folder picker.
    pub(crate) fn picker(&self) -> Arc<dyn RepositoryFolderPicker> {
        Arc::clone(&self.0)
    }
}
