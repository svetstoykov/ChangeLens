import type { OperationErrorType } from "./OperationErrorType";

export interface ActionErrorDetail {
  readonly type: OperationErrorType;
  readonly code: string;
  readonly message: string;
}

export type ActionErrorDetails = readonly [
  ActionErrorDetail,
  ...ActionErrorDetail[],
];
