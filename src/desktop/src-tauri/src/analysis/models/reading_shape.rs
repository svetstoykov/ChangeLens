use serde::{Deserialize, Serialize};

/// Selects the presentation shape of a reading area.
#[derive(Clone, Copy, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase")]
pub enum ReadingShape {
    Walk,
    ParticipantMap,
    PurposeCards,
    ParticipantList,
}
