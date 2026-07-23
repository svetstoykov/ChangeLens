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

#[cfg(test)]
mod tests {
    use super::RepositoryFolderPickerState;
    use crate::engine_protocol::EngineActionError;
    use crate::repositories::RepositoryFolderPicker;
    use std::path::PathBuf;
    use std::sync::Arc;

    struct RepositoryFolderPickerFixture;

    impl RepositoryFolderPicker for RepositoryFolderPickerFixture {
        fn select_folder(&self) -> Result<Option<PathBuf>, EngineActionError> {
            unreachable!("the state test does not open a folder picker")
        }
    }

    #[test]
    fn clones_the_configured_repository_folder_picker() {
        let state = RepositoryFolderPickerState::new(Arc::new(RepositoryFolderPickerFixture));

        let first = state.picker();
        let second = state.picker();

        assert!(Arc::ptr_eq(&first, &second));
    }
}
