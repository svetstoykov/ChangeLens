use serde::Serialize;

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
pub(crate) struct RepositoryOpenParameters<'a> {
    pub(crate) path: &'a str,
}

#[cfg(test)]
mod tests {
    use super::RepositoryOpenParameters;

    #[test]
    fn serializes_exact_path_parameters() {
        let parameters = RepositoryOpenParameters {
            path: "/projects/change_lens",
        };

        let serialized =
            serde_json::to_string(&parameters).expect("repository parameters must serialize");

        assert_eq!(serialized, r#"{"path":"/projects/change_lens"}"#);
    }
}
