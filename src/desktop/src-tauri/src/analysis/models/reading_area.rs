use crate::analysis::models::{
    ReadingParticipant, ReadingRelationship, ReadingShape, ReadingStatement,
};
use serde::{Deserialize, Serialize};

/// Represents one reading area, a bounded narrative region of the change.
#[derive(Clone, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct ReadingArea {
    pub id: String,
    pub title: String,
    pub summary: Option<ReadingStatement>,
    pub shape: ReadingShape,
    pub participants: Vec<ReadingParticipant>,
    pub relationships: Vec<ReadingRelationship>,
    pub ordered_steps: Vec<ReadingStatement>,
    pub purposes: Vec<ReadingStatement>,
}
