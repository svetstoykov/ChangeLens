use serde::{Deserialize, Serialize};

/// Defines the source of a comparison target.
#[derive(Clone, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase")]
pub enum ComparisonTargetKind {
    /// A local branch target.
    Local,
    /// A cached remote-tracking branch target.
    RemoteTracking,
}
