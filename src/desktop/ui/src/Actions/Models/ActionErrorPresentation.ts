export interface ActionErrorPresentation {
  readonly title: string;
  readonly messages: readonly string[];
  readonly requestId?: string;
}
