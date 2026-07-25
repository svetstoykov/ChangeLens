use crate::comparisons::constants::{
    COMPARISON_CHECK_FRESHNESS_ACTION, COMPARISON_LIST_TARGETS_ACTION, COMPARISON_PREPARE_ACTION,
    FRESHNESS_RESPONSE_TIMEOUT, PREPARATION_RESPONSE_TIMEOUT, TARGET_DISCOVERY_RESPONSE_TIMEOUT,
};
use crate::comparisons::models::{
    ComparisonCheckFreshnessParameters, ComparisonListTargetsParameters,
    ComparisonPrepareParameters,
};
use crate::comparisons::{
    ComparisonFreshness, ComparisonService, ComparisonTargetPage, PreparedComparison,
};
use crate::engine_protocol::{EngineActionError, EngineClient};
use std::time::Duration;

impl ComparisonService for EngineClient {
    fn list_targets(
        &self,
        path: &str,
        query: Option<&str>,
        after: Option<&str>,
        target_set_token: Option<&str>,
    ) -> Result<ComparisonTargetPage, EngineActionError> {
        self.list_targets_with_timeout(
            path,
            query,
            after,
            target_set_token,
            TARGET_DISCOVERY_RESPONSE_TIMEOUT,
        )
    }

    fn prepare(&self, path: &str, target: &str) -> Result<PreparedComparison, EngineActionError> {
        self.prepare_with_timeout(path, target, PREPARATION_RESPONSE_TIMEOUT)
    }

    fn check_freshness(
        &self,
        path: &str,
        target: &str,
        freshness_token: &str,
    ) -> Result<ComparisonFreshness, EngineActionError> {
        self.check_freshness_with_timeout(path, target, freshness_token, FRESHNESS_RESPONSE_TIMEOUT)
    }
}

impl EngineClient {
    #[doc(hidden)]
    pub fn list_targets_with_timeout_for_testing(
        &self,
        path: &str,
        query: Option<&str>,
        after: Option<&str>,
        target_set_token: Option<&str>,
        timeout: Duration,
    ) -> Result<ComparisonTargetPage, EngineActionError> {
        self.list_targets_with_timeout(path, query, after, target_set_token, timeout)
    }

    #[doc(hidden)]
    pub fn prepare_with_timeout_for_testing(
        &self,
        path: &str,
        target: &str,
        timeout: Duration,
    ) -> Result<PreparedComparison, EngineActionError> {
        self.prepare_with_timeout(path, target, timeout)
    }

    #[doc(hidden)]
    pub fn check_freshness_with_timeout_for_testing(
        &self,
        path: &str,
        target: &str,
        freshness_token: &str,
        timeout: Duration,
    ) -> Result<ComparisonFreshness, EngineActionError> {
        self.check_freshness_with_timeout(path, target, freshness_token, timeout)
    }

    fn list_targets_with_timeout(
        &self,
        path: &str,
        query: Option<&str>,
        after: Option<&str>,
        target_set_token: Option<&str>,
        timeout: Duration,
    ) -> Result<ComparisonTargetPage, EngineActionError> {
        self.execute_action(
            COMPARISON_LIST_TARGETS_ACTION,
            Some(ComparisonListTargetsParameters {
                path,
                query,
                after,
                target_set_token,
            }),
            timeout,
        )
    }

    fn prepare_with_timeout(
        &self,
        path: &str,
        target: &str,
        timeout: Duration,
    ) -> Result<PreparedComparison, EngineActionError> {
        self.execute_action(
            COMPARISON_PREPARE_ACTION,
            Some(ComparisonPrepareParameters { path, target }),
            timeout,
        )
    }

    fn check_freshness_with_timeout(
        &self,
        path: &str,
        target: &str,
        freshness_token: &str,
        timeout: Duration,
    ) -> Result<ComparisonFreshness, EngineActionError> {
        self.execute_action(
            COMPARISON_CHECK_FRESHNESS_ACTION,
            Some(ComparisonCheckFreshnessParameters {
                path,
                target,
                freshness_token,
            }),
            timeout,
        )
    }
}

#[cfg(test)]
mod tests {
    use crate::comparisons::ComparisonService;
    use crate::engine_protocol::EngineClient;

    #[test]
    fn engine_client_implements_comparison_service() {
        fn assert_implementation<T: ComparisonService>() {}

        assert_implementation::<EngineClient>();
    }

    #[test]
    fn comparison_parameter_types_use_capability_field_names() {
        let list = crate::comparisons::models::ComparisonListTargetsParameters {
            path: "/projects/change_lens",
            query: None,
            after: None,
            target_set_token: None,
        };
        let continued = crate::comparisons::models::ComparisonListTargetsParameters {
            path: "/projects/change_lens",
            query: Some("main"),
            after: Some("refs/heads/release"),
            target_set_token: Some(
                "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
            ),
        };
        let prepare = crate::comparisons::models::ComparisonPrepareParameters {
            path: "/projects/change_lens",
            target: "refs/remotes/origin/main",
        };
        let freshness = crate::comparisons::models::ComparisonCheckFreshnessParameters {
            path: "/projects/change_lens",
            target: "refs/remotes/origin/main",
            freshness_token: "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
        };

        assert_eq!(
            serde_json::to_string(&list).unwrap(),
            r#"{"path":"/projects/change_lens"}"#
        );
        assert_eq!(
            serde_json::to_string(&continued).unwrap(),
            r#"{"path":"/projects/change_lens","query":"main","after":"refs/heads/release","targetSetToken":"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"}"#
        );
        assert_eq!(
            serde_json::to_string(&prepare).unwrap(),
            r#"{"path":"/projects/change_lens","target":"refs/remotes/origin/main"}"#
        );
        assert_eq!(
            serde_json::to_string(&freshness).unwrap(),
            r#"{"path":"/projects/change_lens","target":"refs/remotes/origin/main","freshnessToken":"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"}"#
        );
    }
}
