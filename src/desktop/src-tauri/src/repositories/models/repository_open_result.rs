use crate::repositories::RepositoryDescriptor;
use crate::repositories::models::validation::deserialize_repository_id;
use serde::{Deserialize, Serialize};

/// Represents a successfully opened and retained repository.
#[derive(Clone, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct RepositoryOpenResult {
    /// The ChangeLens-generated history identifier.
    #[serde(deserialize_with = "deserialize_repository_id")]
    pub repository_id: String,
    /// The current repository facts.
    pub repository: RepositoryDescriptor,
    /// The saved full comparison ref, when one exists.
    pub preferred_target: Option<String>,
}

#[cfg(test)]
mod tests {
    use super::RepositoryOpenResult;

    const SHA1_REVISION: &str = "0123456789abcdef0123456789abcdef01234567";

    #[test]
    fn rejects_missing_or_unknown_result_properties() {
        for result in [
            "{}".to_owned(),
            format!(
                r#"{{"repositoryId":"01234567-89ab-cdef-0123-456789abcdef","repository":{{"name":"change_lens","canonicalPath":"/projects/change_lens","head":{{"kind":"detached","revision":"{SHA1_REVISION}"}}}},"preferredTarget":null,"extra":true}}"#
            ),
        ] {
            serde_json::from_str::<RepositoryOpenResult>(&result)
                .expect_err("a result without exactly one repository must be rejected");
        }
    }
}
