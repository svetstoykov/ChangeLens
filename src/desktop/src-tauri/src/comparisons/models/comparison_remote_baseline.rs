use serde::de::Error;
use serde::{Deserialize, Serialize};

/// Represents whether a cached remote-tracking reference still matches the server's branch.
#[derive(Clone, Debug, PartialEq, Eq, Serialize)]
#[serde(tag = "state", rename_all = "camelCase")]
pub enum ComparisonRemoteBaseline {
    /// The cached remote-tracking reference matches the server's branch.
    Current {
        /// The full object identifier the server advertised for the branch.
        #[serde(rename = "remoteRevision")]
        remote_revision: String,
    },
    /// The server's branch has moved since the cached remote-tracking reference was last fetched.
    Moved {
        /// The full object identifier the server currently advertises for the branch.
        #[serde(rename = "remoteRevision")]
        remote_revision: String,
    },
    /// The repository has no configured remote for the selected target.
    NoRemote,
}

impl<'de> Deserialize<'de> for ComparisonRemoteBaseline {
    fn deserialize<D>(deserializer: D) -> Result<Self, D::Error>
    where
        D: serde::Deserializer<'de>,
    {
        let value = serde_json::Value::deserialize(deserializer)?;
        let object = value
            .as_object()
            .ok_or_else(|| D::Error::custom("the remote-baseline result must be an object"))?;

        let state = object
            .get("state")
            .and_then(serde_json::Value::as_str)
            .ok_or_else(|| D::Error::custom("the remote-baseline state is missing"))?;

        match state {
            "current" | "moved" => {
                if object.len() != 2 {
                    return Err(D::Error::custom(
                        "the remote-baseline result must contain only its state and remote revision",
                    ));
                }
                let remote_revision = object
                    .get("remoteRevision")
                    .and_then(serde_json::Value::as_str)
                    .ok_or_else(|| D::Error::custom("the remote revision is missing"))?
                    .to_owned();
                if state == "current" {
                    Ok(Self::Current { remote_revision })
                } else {
                    Ok(Self::Moved { remote_revision })
                }
            }
            "noRemote" => {
                if object.len() != 1 {
                    return Err(D::Error::custom(
                        "the remote-baseline result must contain only its state",
                    ));
                }
                Ok(Self::NoRemote)
            }
            _ => Err(D::Error::custom(
                "the remote-baseline state is not supported",
            )),
        }
    }
}
