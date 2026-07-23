export interface RepositoryFolderPicker {
  selectFolder(): Promise<string | null>;
}
