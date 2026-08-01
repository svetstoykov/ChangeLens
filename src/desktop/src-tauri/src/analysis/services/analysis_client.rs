use crate::analysis::constants::{
    ANALYSIS_CANCEL_ACTION, ANALYSIS_GET_ACTIVE_ACTION, ANALYSIS_OBSERVATION_RESPONSE_TIMEOUT,
    ANALYSIS_POLL_RUN_ACTION, ANALYSIS_START_ACTION, ANALYSIS_START_RESPONSE_TIMEOUT,
};
use crate::analysis::models::{
    AnalysisCancelParameters, AnalysisGetActiveParameters, AnalysisPollRunParameters,
    AnalysisStartParameters,
};
use crate::analysis::{
    AnalysisGetActiveResult, AnalysisRunProjection, AnalysisService, AnalysisStartResult,
};
use crate::engine_protocol::{EngineActionError, EngineClient};
use serde::Deserialize;
use std::time::Duration;

impl AnalysisService for EngineClient {
    fn start(
        &self,
        path: &str,
        target: &str,
        freshness_token: &str,
        change_context: Option<&str>,
    ) -> Result<AnalysisStartResult, EngineActionError> {
        self.start_with_timeout_internal(
            path,
            target,
            freshness_token,
            change_context,
            ANALYSIS_START_RESPONSE_TIMEOUT,
        )
    }

    fn get_active(&self, path: &str) -> Result<AnalysisGetActiveResult, EngineActionError> {
        self.execute_action(
            ANALYSIS_GET_ACTIVE_ACTION,
            Some(AnalysisGetActiveParameters { path }),
            ANALYSIS_OBSERVATION_RESPONSE_TIMEOUT,
        )
    }

    fn poll_run(&self, run_id: &str) -> Result<AnalysisRunProjection, EngineActionError> {
        self.execute_action(
            ANALYSIS_POLL_RUN_ACTION,
            Some(AnalysisPollRunParameters { run_id }),
            ANALYSIS_OBSERVATION_RESPONSE_TIMEOUT,
        )
    }

    fn cancel(&self, run_id: &str) -> Result<(), EngineActionError> {
        self.execute_action::<_, NullResult>(
            ANALYSIS_CANCEL_ACTION,
            Some(AnalysisCancelParameters { run_id }),
            ANALYSIS_OBSERVATION_RESPONSE_TIMEOUT,
        )
        .map(|_| ())
    }
}

impl EngineClient {
    fn start_with_timeout_internal(
        &self,
        path: &str,
        target: &str,
        freshness_token: &str,
        change_context: Option<&str>,
        response_timeout: Duration,
    ) -> Result<AnalysisStartResult, EngineActionError> {
        self.execute_action(
            ANALYSIS_START_ACTION,
            Some(AnalysisStartParameters {
                path,
                target,
                freshness_token,
                change_context,
            }),
            response_timeout,
        )
    }

    #[doc(hidden)]
    pub fn start_with_timeout_for_testing(
        &self,
        path: &str,
        target: &str,
        freshness_token: &str,
        change_context: Option<&str>,
        response_timeout: Duration,
    ) -> Result<AnalysisStartResult, EngineActionError> {
        self.start_with_timeout_internal(
            path,
            target,
            freshness_token,
            change_context,
            response_timeout,
        )
    }
}

#[derive(Deserialize)]
struct NullResult;

#[cfg(test)]
mod tests {
    #[test]
    fn analysis_parameter_types_use_capability_field_names() {
        let start = crate::analysis::models::AnalysisStartParameters {
            path: "/projects/change_lens",
            target: "refs/remotes/origin/main",
            freshness_token: "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
            change_context: None,
        };
        let get_active = crate::analysis::models::AnalysisGetActiveParameters {
            path: "/projects/change_lens",
        };
        let poll_run = crate::analysis::models::AnalysisPollRunParameters {
            run_id: "0198a1b2-3c4d-4e5f-8a9b-0123456789ab",
        };
        let cancel = crate::analysis::models::AnalysisCancelParameters {
            run_id: "0198a1b2-3c4d-4e5f-8a9b-0123456789ab",
        };

        assert_eq!(
            serde_json::to_string(&start).unwrap(),
            r#"{"path":"/projects/change_lens","target":"refs/remotes/origin/main","freshnessToken":"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"}"#
        );
        assert_eq!(
            serde_json::to_string(&get_active).unwrap(),
            r#"{"path":"/projects/change_lens"}"#
        );
        assert_eq!(
            serde_json::to_string(&poll_run).unwrap(),
            r#"{"runId":"0198a1b2-3c4d-4e5f-8a9b-0123456789ab"}"#
        );
        assert_eq!(
            serde_json::to_string(&cancel).unwrap(),
            r#"{"runId":"0198a1b2-3c4d-4e5f-8a9b-0123456789ab"}"#
        );
    }
}
