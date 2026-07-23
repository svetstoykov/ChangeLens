use crate::repositories::models::validation::{deserialize_non_blank, deserialize_revision};
use serde::{Deserialize, Serialize};

/// Defines whether a repository HEAD is attached to a branch or detached at a revision.
#[derive(Clone, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(tag = "kind", rename_all = "camelCase", deny_unknown_fields)]
pub enum RepositoryHead {
    /// A HEAD attached to a named branch.
    #[serde(rename = "branch")]
    Branch {
        /// The short branch name.
        #[serde(deserialize_with = "deserialize_non_blank")]
        name: String,
        /// The full object revision.
        #[serde(deserialize_with = "deserialize_revision")]
        revision: String,
    },
    /// A HEAD detached at a revision.
    #[serde(rename = "detached")]
    Detached {
        /// The full object revision.
        #[serde(deserialize_with = "deserialize_revision")]
        revision: String,
    },
}

#[cfg(test)]
mod tests {
    use super::RepositoryHead;

    const SHA1_REVISION: &str = "0123456789abcdef0123456789abcdef01234567";
    const SHA256_REVISION: &str =
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    #[test]
    fn parses_branch_and_detached_heads() {
        let branch: RepositoryHead = serde_json::from_str(&format!(
            r#"{{"kind":"branch","name":"main","revision":"{SHA1_REVISION}"}}"#
        ))
        .expect("a canonical branch head must deserialize");
        let detached: RepositoryHead = serde_json::from_str(&format!(
            r#"{{"kind":"detached","revision":"{SHA256_REVISION}"}}"#
        ))
        .expect("a canonical detached head must deserialize");

        assert_eq!(
            branch,
            RepositoryHead::Branch {
                name: "main".to_owned(),
                revision: SHA1_REVISION.to_owned(),
            }
        );
        assert_eq!(
            detached,
            RepositoryHead::Detached {
                revision: SHA256_REVISION.to_owned(),
            }
        );
    }

    #[test]
    fn preserves_nonblank_values_without_trimming() {
        let head: RepositoryHead = serde_json::from_str(&format!(
            r#"{{"kind":"branch","name":" main ","revision":"{SHA1_REVISION}"}}"#
        ))
        .expect("surrounding whitespace must be preserved");

        assert_eq!(
            head,
            RepositoryHead::Branch {
                name: " main ".to_owned(),
                revision: SHA1_REVISION.to_owned(),
            }
        );
    }

    #[test]
    fn rejects_invalid_head_shapes() {
        for head in [
            format!(r#"{{"kind":"other","revision":"{SHA1_REVISION}"}}"#),
            format!(r#"{{"kind":"detached","name":"main","revision":"{SHA1_REVISION}"}}"#),
            format!(r#"{{"kind":"branch","revision":"{SHA1_REVISION}"}}"#),
            format!(r#"{{"kind":"branch","name":" ","revision":"{SHA1_REVISION}"}}"#),
            format!(
                r#"{{"kind":"branch","name":"main","revision":"{SHA1_REVISION}","extra":true}}"#
            ),
        ] {
            serde_json::from_str::<RepositoryHead>(&head)
                .expect_err("an invalid repository head shape must be rejected");
        }
    }

    #[test]
    fn rejects_invalid_revisions() {
        for revision in [
            "0123456789ABCDEF0123456789ABCDEF01234567",
            "0123456789abcdef",
            "g123456789abcdef0123456789abcdef01234567",
            "é123456789abcdef0123456789abcdef0123456",
        ] {
            let head = format!(r#"{{"kind":"detached","revision":"{revision}"}}"#);

            serde_json::from_str::<RepositoryHead>(&head)
                .expect_err("a noncanonical revision must be rejected");
        }
    }
}
