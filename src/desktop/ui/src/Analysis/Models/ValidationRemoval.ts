import type { ValidationRemovalScope } from "./ValidationRemovalScope";

export interface ValidationRemoval {
  readonly scope: ValidationRemovalScope;
  readonly id: string;
  readonly reason: string;
}
