use serde::{Deserialize, Serialize};

/// Classifies a disclosed verification assurance gap.
#[derive(Clone, Copy, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase")]
pub enum ReadingAssuranceKind {
    CheckerNotRun,
    CheckerFailed,
    TestsNotExecuted,
    BuildNotExecuted,
    RepositoryNotFullyRead,
    ClaimNotCited,
    DuplicateClaimId,
}
