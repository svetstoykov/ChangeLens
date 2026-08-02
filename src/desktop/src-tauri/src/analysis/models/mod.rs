mod analysis_cancel_parameters;
mod analysis_comparison;
mod analysis_fact;
mod analysis_get_active_parameters;
mod analysis_get_active_result;
mod analysis_poll_run_parameters;
mod analysis_repository;
mod analysis_run_projection;
mod analysis_run_state;
mod analysis_start_parameters;
mod analysis_start_result;
mod analysis_terminal;
mod validation;

pub(crate) use analysis_cancel_parameters::AnalysisCancelParameters;
pub use analysis_comparison::AnalysisComparison;
pub use analysis_fact::AnalysisFact;
pub(crate) use analysis_get_active_parameters::AnalysisGetActiveParameters;
pub use analysis_get_active_result::AnalysisGetActiveResult;
pub(crate) use analysis_poll_run_parameters::AnalysisPollRunParameters;
pub use analysis_repository::AnalysisRepository;
pub use analysis_run_projection::AnalysisRunProjection;
pub use analysis_run_state::AnalysisRunState;
pub(crate) use analysis_start_parameters::AnalysisStartParameters;
pub use analysis_start_result::AnalysisStartResult;
pub use analysis_terminal::AnalysisTerminal;

#[cfg(test)]
mod tests {
    use super::{
        AnalysisGetActiveResult, AnalysisRunProjection, AnalysisRunState, AnalysisStartResult,
    };

    const ACCEPTED_FIXTURE: &str = include_str!(concat!(
        env!("CARGO_MANIFEST_DIR"),
        "/../../../contracts/engine-protocol/v1/fixtures/analysis-start.accepted.result.json"
    ));
    const REJECTED_STALE_FIXTURE: &str = include_str!(concat!(
        env!("CARGO_MANIFEST_DIR"),
        "/../../../contracts/engine-protocol/v1/fixtures/analysis-start.rejected-stale.result.json"
    ));
    const REJECTED_ACTIVE_FIXTURE: &str = include_str!(concat!(
        env!("CARGO_MANIFEST_DIR"),
        "/../../../contracts/engine-protocol/v1/fixtures/analysis-start.rejected-active.result.json"
    ));
    const PENDING_CAPTURE_FIXTURE: &str = include_str!(concat!(
        env!("CARGO_MANIFEST_DIR"),
        "/../../../contracts/engine-protocol/v1/fixtures/analysis-poll-run.pending-capture.result.json"
    ));
    const CAPTURING_FIXTURE: &str = include_str!(concat!(
        env!("CARGO_MANIFEST_DIR"),
        "/../../../contracts/engine-protocol/v1/fixtures/analysis-poll-run.capturing.result.json"
    ));
    const DISCOVERING_FIXTURE: &str = include_str!(concat!(
        env!("CARGO_MANIFEST_DIR"),
        "/../../../contracts/engine-protocol/v1/fixtures/analysis-poll-run.discovering.result.json"
    ));
    const COLLECTING_FIXTURE: &str = include_str!(concat!(
        env!("CARGO_MANIFEST_DIR"),
        "/../../../contracts/engine-protocol/v1/fixtures/analysis-poll-run.collecting.result.json"
    ));
    const PERSISTING_FIXTURE: &str = include_str!(concat!(
        env!("CARGO_MANIFEST_DIR"),
        "/../../../contracts/engine-protocol/v1/fixtures/analysis-poll-run.persisting.result.json"
    ));
    const COMPLETED_FIXTURE: &str = include_str!(concat!(
        env!("CARGO_MANIFEST_DIR"),
        "/../../../contracts/engine-protocol/v1/fixtures/analysis-poll-run.completed.result.json"
    ));
    const COMPLETED_WITH_LIMITATIONS_FIXTURE: &str = include_str!(concat!(
        env!("CARGO_MANIFEST_DIR"),
        "/../../../contracts/engine-protocol/v1/fixtures/analysis-poll-run.completed-with-limitations.result.json"
    ));
    const CANCELLED_FIXTURE: &str = include_str!(concat!(
        env!("CARGO_MANIFEST_DIR"),
        "/../../../contracts/engine-protocol/v1/fixtures/analysis-poll-run.cancelled.result.json"
    ));
    const FAILED_FIXTURE: &str = include_str!(concat!(
        env!("CARGO_MANIFEST_DIR"),
        "/../../../contracts/engine-protocol/v1/fixtures/analysis-poll-run.failed.result.json"
    ));
    const INTERRUPTED_FIXTURE: &str = include_str!(concat!(
        env!("CARGO_MANIFEST_DIR"),
        "/../../../contracts/engine-protocol/v1/fixtures/analysis-poll-run.interrupted.result.json"
    ));
    const GET_ACTIVE_NONE_FIXTURE: &str = include_str!(concat!(
        env!("CARGO_MANIFEST_DIR"),
        "/../../../contracts/engine-protocol/v1/fixtures/analysis-get-active.none.result.json"
    ));
    const GET_ACTIVE_ACTIVE_FIXTURE: &str = include_str!(concat!(
        env!("CARGO_MANIFEST_DIR"),
        "/../../../contracts/engine-protocol/v1/fixtures/analysis-get-active.active.result.json"
    ));
    const CAPTURED_FIXTURE: &str = include_str!(concat!(
        env!("CARGO_MANIFEST_DIR"),
        "/../../../contracts/engine-protocol/v1/fixtures/analysis-poll-run.captured.result.json"
    ));

    #[test]
    fn deserializes_every_shared_analysis_fixture() {
        let accepted: AnalysisStartResult = result_from_fixture(ACCEPTED_FIXTURE);
        let rejected_stale: AnalysisStartResult = result_from_fixture(REJECTED_STALE_FIXTURE);
        let rejected_active: AnalysisStartResult = result_from_fixture(REJECTED_ACTIVE_FIXTURE);
        let pending_capture: AnalysisRunProjection = result_from_fixture(PENDING_CAPTURE_FIXTURE);
        let capturing: AnalysisRunProjection = result_from_fixture(CAPTURING_FIXTURE);
        let discovering: AnalysisRunProjection = result_from_fixture(DISCOVERING_FIXTURE);
        let collecting: AnalysisRunProjection = result_from_fixture(COLLECTING_FIXTURE);
        let persisting: AnalysisRunProjection = result_from_fixture(PERSISTING_FIXTURE);
        let completed: AnalysisRunProjection = result_from_fixture(COMPLETED_FIXTURE);
        let completed_with_limitations: AnalysisRunProjection =
            result_from_fixture(COMPLETED_WITH_LIMITATIONS_FIXTURE);
        let cancelled: AnalysisRunProjection = result_from_fixture(CANCELLED_FIXTURE);
        let failed: AnalysisRunProjection = result_from_fixture(FAILED_FIXTURE);
        let interrupted: AnalysisRunProjection = result_from_fixture(INTERRUPTED_FIXTURE);
        let get_active_none: AnalysisGetActiveResult = result_from_fixture(GET_ACTIVE_NONE_FIXTURE);
        let get_active_active: AnalysisGetActiveResult =
            result_from_fixture(GET_ACTIVE_ACTIVE_FIXTURE);
        let captured: AnalysisRunProjection = result_from_fixture(CAPTURED_FIXTURE);

        assert!(matches!(accepted, AnalysisStartResult::Accepted { .. }));
        assert!(matches!(rejected_stale, AnalysisStartResult::RejectedStale));
        assert!(matches!(
            rejected_active,
            AnalysisStartResult::RejectedActive { .. }
        ));
        assert_eq!(pending_capture.state, AnalysisRunState::PendingCapture);
        assert_eq!(capturing.state, AnalysisRunState::Capturing);
        assert_eq!(discovering.state, AnalysisRunState::Discovering);
        assert_eq!(collecting.state, AnalysisRunState::Collecting);
        assert_eq!(persisting.state, AnalysisRunState::Persisting);
        assert_eq!(completed.state, AnalysisRunState::Completed);
        assert_eq!(
            completed_with_limitations.state,
            AnalysisRunState::CompletedWithLimitations
        );
        assert!(!completed_with_limitations.facts.is_empty());
        assert_eq!(cancelled.state, AnalysisRunState::Cancelled);
        assert_eq!(failed.state, AnalysisRunState::Failed);
        assert_eq!(interrupted.state, AnalysisRunState::Interrupted);
        assert_eq!(
            interrupted.interruption_reason.as_deref(),
            Some("engineStopped")
        );
        assert!(matches!(get_active_none, AnalysisGetActiveResult::None));
        assert!(matches!(
            get_active_active,
            AnalysisGetActiveResult::Active { .. }
        ));
        assert_eq!(
            captured.snapshot_id.as_deref(),
            Some("7198a1b2-3c4d-4e5f-8a9b-0123456789ab")
        );
        assert_eq!(captured.captured_at, Some(1_720_000_000_300));
        assert_eq!(captured.facts.len(), 2);
        assert_eq!(captured.facts[0].kind, "changedFilesCaptured");
        assert_eq!(captured.facts[1].count, 3);
    }

    #[test]
    fn rejects_unknown_state_discriminant_rather_than_defaulting() {
        let malformed =
            COMPLETED_FIXTURE.replace("\"state\":\"completed\"", "\"state\":\"unknownState\"");
        let value = fixture_result(&malformed);

        serde_json::from_value::<AnalysisRunProjection>(value)
            .expect_err("an unknown state discriminant must be rejected, not defaulted");
    }

    #[test]
    fn rejects_malformed_run_id_and_out_of_range_limitation_count() {
        let bad_run_id = ACCEPTED_FIXTURE.replace(
            "\"runId\":\"0198a1b2-3c4d-4e5f-8a9b-0123456789ab\"",
            "\"runId\":\"not-a-uuid\"",
        );
        serde_json::from_value::<AnalysisStartResult>(fixture_result(&bad_run_id))
            .expect_err("a malformed run id must be rejected");

        let negative_limitation = COMPLETED_WITH_LIMITATIONS_FIXTURE
            .replace("\"limitationCount\":2", "\"limitationCount\":-1");
        serde_json::from_value::<AnalysisRunProjection>(fixture_result(&negative_limitation))
            .expect_err("a negative limitation count must be rejected");
    }

    #[test]
    fn rejects_malformed_snapshot_id() {
        let malformed = CAPTURED_FIXTURE.replace(
            "\"snapshotId\":\"7198a1b2-3c4d-4e5f-8a9b-0123456789ab\"",
            "\"snapshotId\":\"snapshot-1\"",
        );

        serde_json::from_value::<AnalysisRunProjection>(fixture_result(&malformed))
            .expect_err("a malformed snapshot id must be rejected");
    }

    fn result_from_fixture<T: serde::de::DeserializeOwned>(fixture: &str) -> T {
        serde_json::from_value(fixture_result(fixture))
            .expect("the canonical shared analysis fixture must deserialize")
    }

    fn fixture_result(fixture: &str) -> serde_json::Value {
        let envelope: serde_json::Value =
            serde_json::from_str(fixture).expect("the shared analysis fixture must be JSON");
        envelope["result"].clone()
    }
}
