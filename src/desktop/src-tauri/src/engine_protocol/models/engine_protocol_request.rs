use serde::Serialize;

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
pub(crate) struct EngineProtocolRequest<'a, TParameters> {
    pub(crate) protocol_version: u32,
    pub(crate) request_id: &'a str,
    pub(crate) action: &'a str,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub(crate) parameters: Option<TParameters>,
}

#[cfg(test)]
mod tests {
    use super::EngineProtocolRequest;
    use serde::Serialize;

    #[derive(Serialize)]
    struct RepositoryOpenParameters<'a> {
        path: &'a str,
    }

    #[test]
    fn serializes_payload_free_action_without_parameters() {
        let request = EngineProtocolRequest {
            protocol_version: 1,
            request_id: "desktop-1",
            action: "engine.checkStatus",
            parameters: None::<()>,
        };

        let serialized =
            serde_json::to_string(&request).expect("the protocol request should serialize");

        assert_eq!(
            serialized,
            r#"{"protocolVersion":1,"requestId":"desktop-1","action":"engine.checkStatus"}"#
        );
    }

    #[test]
    fn serializes_repository_action_with_exact_parameters() {
        let request = EngineProtocolRequest {
            protocol_version: 1,
            request_id: "desktop-42",
            action: "repositories.open",
            parameters: Some(RepositoryOpenParameters {
                path: "/projects/change_lens",
            }),
        };

        let serialized =
            serde_json::to_string(&request).expect("the repository request should serialize");

        assert_eq!(
            serialized,
            r#"{"protocolVersion":1,"requestId":"desktop-42","action":"repositories.open","parameters":{"path":"/projects/change_lens"}}"#
        );
    }
}
