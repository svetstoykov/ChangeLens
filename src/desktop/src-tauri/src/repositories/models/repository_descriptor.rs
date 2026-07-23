use crate::repositories::RepositoryHead;
use crate::repositories::models::validation::deserialize_non_blank;
use serde::{Deserialize, Serialize};

/// Represents the identity and current HEAD of an opened repository.
#[derive(Clone, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct RepositoryDescriptor {
    /// The repository directory name.
    #[serde(deserialize_with = "deserialize_non_blank")]
    pub name: String,
    /// The canonical physical path to the repository root.
    #[serde(deserialize_with = "deserialize_non_blank")]
    pub canonical_path: String,
    /// The repository's current HEAD.
    pub head: RepositoryHead,
}

#[cfg(test)]
mod tests {
    use super::RepositoryDescriptor;
    use crate::repositories::RepositoryHead;

    const SHA1_REVISION: &str = "0123456789abcdef0123456789abcdef01234567";
    const BRANCH_RESULT_FIXTURE: &str = include_str!(concat!(
        env!("CARGO_MANIFEST_DIR"),
        "/../../../contracts/engine-protocol/v1/fixtures/repositories-open.branch.result.json"
    ));
    const DETACHED_RESULT_FIXTURE: &str = include_str!(concat!(
        env!("CARGO_MANIFEST_DIR"),
        "/../../../contracts/engine-protocol/v1/fixtures/repositories-open.detached.result.json"
    ));

    #[test]
    fn deserializes_the_shared_branch_result_fixture() {
        let repository = repository_from_fixture(BRANCH_RESULT_FIXTURE);

        assert_eq!(repository.name, "change_lens");
        assert_eq!(repository.canonical_path, "/projects/change_lens");
        assert_eq!(
            repository.head,
            RepositoryHead::Branch {
                name: "main".to_owned(),
                revision: SHA1_REVISION.to_owned(),
            }
        );
    }

    #[test]
    fn deserializes_the_shared_detached_result_fixture() {
        let repository = repository_from_fixture(DETACHED_RESULT_FIXTURE);

        assert_eq!(repository.name, "change_lens");
        assert_eq!(repository.canonical_path, "/projects/change_lens");
        assert_eq!(
            repository.head,
            RepositoryHead::Detached {
                revision: SHA1_REVISION.to_owned(),
            }
        );
    }

    #[test]
    fn preserves_nonblank_descriptor_values_without_trimming() {
        let repository: RepositoryDescriptor = serde_json::from_str(&format!(
            r#"{{"name":" change_lens ","canonicalPath":" /projects/change_lens ","head":{{"kind":"detached","revision":"{SHA1_REVISION}"}}}}"#
        ))
        .expect("surrounding whitespace must be preserved");

        assert_eq!(repository.name, " change_lens ");
        assert_eq!(repository.canonical_path, " /projects/change_lens ");
    }

    #[test]
    fn rejects_blank_or_unknown_descriptor_values() {
        for repository in [
            format!(
                r#"{{"name":" ","canonicalPath":"/projects/change_lens","head":{{"kind":"detached","revision":"{SHA1_REVISION}"}}}}"#
            ),
            format!(
                r#"{{"name":"change_lens","canonicalPath":"\t","head":{{"kind":"detached","revision":"{SHA1_REVISION}"}}}}"#
            ),
            format!(
                r#"{{"name":"change_lens","canonicalPath":"/projects/change_lens","head":{{"kind":"detached","revision":"{SHA1_REVISION}"}},"extra":true}}"#
            ),
        ] {
            serde_json::from_str::<RepositoryDescriptor>(&repository)
                .expect_err("an invalid repository descriptor must be rejected");
        }
    }

    fn repository_from_fixture(fixture: &str) -> RepositoryDescriptor {
        let envelope: serde_json::Value =
            serde_json::from_str(fixture).expect("the shared result fixture must contain JSON");

        serde_json::from_value(envelope["result"]["repository"].clone())
            .expect("the shared repository descriptor must deserialize")
    }
}
