export type ReadingAssuranceKind =
  | "checkerNotRun"
  | "checkerFailed"
  | "testsNotExecuted"
  | "buildNotExecuted"
  | "repositoryNotFullyRead"
  | "claimNotCited"
  | "duplicateClaimId"
  | "reviewNotRun"
  | "reviewFailed"
  | "reviewTooLarge";
