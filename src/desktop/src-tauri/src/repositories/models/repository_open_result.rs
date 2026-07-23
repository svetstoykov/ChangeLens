use crate::repositories::RepositoryDescriptor;
use serde::Deserialize;

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub(crate) struct RepositoryOpenResult {
    pub(crate) repository: RepositoryDescriptor,
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
                r#"{{"repository":{{"name":"change_lens","canonicalPath":"/projects/change_lens","head":{{"kind":"detached","revision":"{SHA1_REVISION}"}}}},"extra":true}}"#
            ),
        ] {
            serde_json::from_str::<RepositoryOpenResult>(&result)
                .expect_err("a result without exactly one repository must be rejected");
        }
    }
}
