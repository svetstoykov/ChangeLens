mod analysis_cancel_parameters;
mod analysis_comparison;
mod analysis_fact;
mod analysis_get_active_parameters;
mod analysis_get_active_result;
mod analysis_poll_run_parameters;
mod analysis_repository;
mod analysis_run_state;
mod analysis_run_summary;
mod analysis_start_parameters;
mod analysis_start_result;
mod analysis_terminal;
mod reading_area;
mod reading_assurance;
mod reading_assurance_kind;
mod reading_citation;
mod reading_evidence;
mod reading_finding;
mod reading_finding_severity;
mod reading_focus_range;
mod reading_limitation;
mod reading_limitation_kind;
mod reading_model;
mod reading_omission_source_kind;
mod reading_omission_summary;
mod reading_participant;
mod reading_relationship;
mod reading_review;
mod reading_review_recommendation;
mod reading_review_status;
mod reading_shape;
mod reading_side;
mod reading_statement;
mod reading_trust;
mod validation;
mod validation_removal;
mod validation_removal_scope;

pub(crate) use analysis_cancel_parameters::AnalysisCancelParameters;
pub use analysis_comparison::AnalysisComparison;
pub use analysis_fact::AnalysisFact;
pub(crate) use analysis_get_active_parameters::AnalysisGetActiveParameters;
pub use analysis_get_active_result::AnalysisGetActiveResult;
pub(crate) use analysis_poll_run_parameters::AnalysisPollRunParameters;
pub use analysis_repository::AnalysisRepository;
pub use analysis_run_state::AnalysisRunState;
pub use analysis_run_summary::AnalysisRunSummary;
pub(crate) use analysis_start_parameters::AnalysisStartParameters;
pub use analysis_start_result::AnalysisStartResult;
pub use analysis_terminal::AnalysisTerminal;
pub use reading_area::ReadingArea;
pub use reading_assurance::ReadingAssurance;
pub use reading_assurance_kind::ReadingAssuranceKind;
pub use reading_citation::ReadingCitation;
pub use reading_evidence::ReadingEvidence;
pub use reading_finding::ReadingFinding;
pub use reading_finding_severity::ReadingFindingSeverity;
pub use reading_focus_range::ReadingFocusRange;
pub use reading_limitation::ReadingLimitation;
pub use reading_limitation_kind::ReadingLimitationKind;
pub use reading_model::ReadingModel;
pub use reading_omission_source_kind::ReadingOmissionSourceKind;
pub use reading_omission_summary::ReadingOmissionSummary;
pub use reading_participant::ReadingParticipant;
pub use reading_relationship::ReadingRelationship;
pub use reading_review::ReadingReview;
pub use reading_review_recommendation::ReadingReviewRecommendation;
pub use reading_review_status::ReadingReviewStatus;
pub use reading_shape::ReadingShape;
pub use reading_side::ReadingSide;
pub use reading_statement::ReadingStatement;
pub use reading_trust::ReadingTrust;
pub use validation_removal::ValidationRemoval;
pub use validation_removal_scope::ValidationRemovalScope;

#[cfg(test)]
mod tests {
    use super::{
        AnalysisGetActiveResult, AnalysisRunState, AnalysisRunSummary, AnalysisStartResult,
        ReadingAssuranceKind, ReadingFindingSeverity, ReadingLimitationKind, ReadingModel,
        ReadingReviewRecommendation, ReadingReviewStatus, ValidationRemovalScope,
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
    const COMPLETED_WITH_READING_MODEL_FIXTURE: &str = include_str!(concat!(
        env!("CARGO_MANIFEST_DIR"),
        "/../../../contracts/engine-protocol/v1/fixtures/analysis-poll-run.completed-with-reading-model.result.json"
    ));
    const COMPLETED_WITH_FINDINGS_FIXTURE: &str = include_str!(concat!(
        env!("CARGO_MANIFEST_DIR"),
        "/../../../contracts/engine-protocol/v1/fixtures/analysis-poll-run.completed-with-findings.result.json"
    ));
    const REVIEW_CLEAN_FIXTURE: &str = include_str!(concat!(
        env!("CARGO_MANIFEST_DIR"),
        "/../../../contracts/engine-protocol/v1/fixtures/analysis-poll-run.review-clean.result.json"
    ));
    const REVIEW_NO_DEFECTS_CONFIRMED_FIXTURE: &str = include_str!(concat!(
        env!("CARGO_MANIFEST_DIR"),
        "/../../../contracts/engine-protocol/v1/fixtures/analysis-poll-run.review-no-defects-confirmed.result.json"
    ));
    const REVIEW_FAILED_FIXTURE: &str = include_str!(concat!(
        env!("CARGO_MANIFEST_DIR"),
        "/../../../contracts/engine-protocol/v1/fixtures/analysis-poll-run.review-failed.result.json"
    ));
    const REVIEW_TOO_LARGE_FIXTURE: &str = include_str!(concat!(
        env!("CARGO_MANIFEST_DIR"),
        "/../../../contracts/engine-protocol/v1/fixtures/analysis-poll-run.review-too-large.result.json"
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
        let pending_capture: AnalysisRunSummary = result_from_fixture(PENDING_CAPTURE_FIXTURE);
        let capturing: AnalysisRunSummary = result_from_fixture(CAPTURING_FIXTURE);
        let discovering: AnalysisRunSummary = result_from_fixture(DISCOVERING_FIXTURE);
        let collecting: AnalysisRunSummary = result_from_fixture(COLLECTING_FIXTURE);
        let persisting: AnalysisRunSummary = result_from_fixture(PERSISTING_FIXTURE);
        let completed: AnalysisRunSummary = result_from_fixture(COMPLETED_FIXTURE);
        let completed_with_limitations: AnalysisRunSummary =
            result_from_fixture(COMPLETED_WITH_LIMITATIONS_FIXTURE);
        let cancelled: AnalysisRunSummary = result_from_fixture(CANCELLED_FIXTURE);
        let failed: AnalysisRunSummary = result_from_fixture(FAILED_FIXTURE);
        let interrupted: AnalysisRunSummary = result_from_fixture(INTERRUPTED_FIXTURE);
        let get_active_none: AnalysisGetActiveResult = result_from_fixture(GET_ACTIVE_NONE_FIXTURE);
        let get_active_active: AnalysisGetActiveResult =
            result_from_fixture(GET_ACTIVE_ACTIVE_FIXTURE);
        let captured: AnalysisRunSummary = result_from_fixture(CAPTURED_FIXTURE);

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
        if let AnalysisGetActiveResult::Active { run } = &get_active_active {
            assert!(run.reading_model.is_none());
            assert!(run.validation_removals.is_none());
        }
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
    fn deserializes_completed_fixture_with_a_populated_reading_model() {
        let summary: AnalysisRunSummary = result_from_fixture(COMPLETED_WITH_READING_MODEL_FIXTURE);
        let reading_model = summary
            .reading_model
            .as_ref()
            .expect("the fixture must carry a reading model");

        let citation = reading_model
            .citations
            .first()
            .expect("the reading model must carry a citation");
        assert_eq!(citation.focus.len(), 2);
        assert_eq!(
            (citation.focus[0].start_line, citation.focus[0].end_line),
            (12, 12)
        );
        assert_eq!(
            (citation.focus[1].start_line, citation.focus[1].end_line),
            (16, 17)
        );

        assert_eq!(reading_model.omission_summaries.len(), 1);
        let omission = &reading_model.omission_summaries[0];
        assert_eq!(
            (
                omission.total_count,
                omission.sample_count,
                omission.resolved_sample_count
            ),
            (1, 1, 1)
        );

        assert!(
            reading_model
                .limitations
                .iter()
                .any(|limitation| limitation.path.as_deref() == Some("assets/blob.bin"))
        );
        assert!(reading_model.limitations.iter().any(|limitation| {
            limitation.kind == ReadingLimitationKind::UncommittedWorkExcluded
                && limitation.path.is_none()
        }));

        let removals = summary
            .validation_removals
            .as_ref()
            .expect("the fixture must carry validation removals");
        assert_eq!(removals.len(), 1);
        assert_eq!(removals[0].scope, ValidationRemovalScope::Statement);
        assert_eq!(removals[0].id, "thesis");
    }

    #[test]
    fn deserializes_the_completed_fixture_with_findings_and_a_nonzero_withheld_count() {
        let reading_model = reading_model_from_fixture(COMPLETED_WITH_FINDINGS_FIXTURE);

        assert_eq!(reading_model.findings.len(), 2);
        assert_eq!(reading_model.findings[0].id, "f1");
        assert_eq!(
            reading_model.findings[0].severity,
            ReadingFindingSeverity::Warning
        );
        assert_eq!(reading_model.findings[0].area_id.as_deref(), Some("parse"));
        assert_eq!(
            reading_model.findings[1].severity,
            ReadingFindingSeverity::Info
        );
        assert!(reading_model.findings[1].area_id.is_none());
        assert_eq!(reading_model.review.status, ReadingReviewStatus::Ran);
        assert_eq!(
            reading_model.review.recommendation,
            Some(ReadingReviewRecommendation::IssuesWorthAddressing)
        );
        assert_eq!(reading_model.review.withheld_count, 2);
        assert!(
            reading_model
                .citations
                .iter()
                .any(|citation| citation.claim_id == "finding:f1")
        );
    }

    #[test]
    fn deserializes_the_clean_and_no_defects_confirmed_fixtures() {
        let clean = reading_model_from_fixture(REVIEW_CLEAN_FIXTURE);
        let confirmed = reading_model_from_fixture(REVIEW_NO_DEFECTS_CONFIRMED_FIXTURE);

        assert!(clean.findings.is_empty());
        assert_eq!(
            clean.review.recommendation,
            Some(ReadingReviewRecommendation::NoDefectsFound)
        );
        assert_eq!(clean.review.withheld_count, 0);
        assert!(confirmed.findings.is_empty());
        assert_eq!(
            confirmed.review.recommendation,
            Some(ReadingReviewRecommendation::NoDefectsConfirmed)
        );
        assert_eq!(confirmed.review.withheld_count, 2);
    }

    #[test]
    fn deserializes_the_failed_and_too_large_review_fixtures_without_a_recommendation() {
        let failed = reading_model_from_fixture(REVIEW_FAILED_FIXTURE);
        let too_large = reading_model_from_fixture(REVIEW_TOO_LARGE_FIXTURE);

        assert_eq!(failed.review.status, ReadingReviewStatus::Failed);
        assert_eq!(too_large.review.status, ReadingReviewStatus::TooLarge);
        for model in [&failed, &too_large] {
            assert!(model.findings.is_empty());
            assert!(model.review.recommendation.is_none());
            assert_eq!(model.review.withheld_count, 0);
        }
        assert!(
            failed
                .assurances
                .iter()
                .any(|assurance| assurance.kind == ReadingAssuranceKind::ReviewFailed)
        );
        assert!(
            too_large
                .assurances
                .iter()
                .any(|assurance| assurance.kind == ReadingAssuranceKind::ReviewTooLarge)
        );
    }

    #[test]
    fn the_populated_reading_model_fixture_carries_a_review_that_was_not_run() {
        let reading_model = reading_model_from_fixture(COMPLETED_WITH_READING_MODEL_FIXTURE);

        assert!(reading_model.findings.is_empty());
        assert_eq!(reading_model.review.status, ReadingReviewStatus::NotRun);
        assert!(
            reading_model
                .assurances
                .iter()
                .any(|assurance| assurance.kind == ReadingAssuranceKind::ReviewNotRun)
        );
    }

    #[test]
    fn deserializes_the_finding_removal_scope() {
        let value: ValidationRemovalScope = serde_json::from_str("\"finding\"")
            .expect("the finding removal scope must deserialize");

        assert_eq!(value, ValidationRemovalScope::Finding);
    }

    #[test]
    fn rejects_a_reading_model_missing_findings_or_review() {
        for missing in ["findings", "review"] {
            let mut value = fixture_result(COMPLETED_WITH_FINDINGS_FIXTURE);
            value["readingModel"]
                .as_object_mut()
                .expect("the reading model must be an object")
                .remove(missing);

            serde_json::from_value::<AnalysisRunSummary>(value)
                .expect_err("a reading model missing findings or review must be rejected");
        }
    }

    #[test]
    fn rejects_malformed_findings_and_reviews() {
        let mutations: [fn(&mut serde_json::Value); 6] = [
            |model| model["findings"][0]["severity"] = serde_json::json!("question"),
            |model| model["findings"][0]["extra"] = serde_json::json!(true),
            |model| {
                let finding = model["findings"][0].clone();
                model["findings"] = serde_json::Value::Array(vec![finding; 11]);
            },
            |model| model["review"]["recommendation"] = serde_json::Value::Null,
            |model| {
                model["review"] = serde_json::json!({
                    "status": "failed", "recommendation": "noDefectsFound", "withheldCount": 0
                });
            },
            |model| {
                model["review"] = serde_json::json!({
                    "status": "tooLarge", "recommendation": null, "withheldCount": 1
                });
            },
        ];

        for mutate in mutations {
            let mut value = fixture_result(COMPLETED_WITH_FINDINGS_FIXTURE);
            mutate(&mut value["readingModel"]);

            serde_json::from_value::<AnalysisRunSummary>(value)
                .expect_err("a malformed finding or review must be rejected");
        }
    }

    #[test]
    fn null_reading_model_fixtures_deserialize_without_a_reading_model() {
        for fixture in [
            PENDING_CAPTURE_FIXTURE,
            CAPTURING_FIXTURE,
            DISCOVERING_FIXTURE,
            COLLECTING_FIXTURE,
            PERSISTING_FIXTURE,
            COMPLETED_FIXTURE,
            COMPLETED_WITH_LIMITATIONS_FIXTURE,
            CANCELLED_FIXTURE,
            FAILED_FIXTURE,
            INTERRUPTED_FIXTURE,
            CAPTURED_FIXTURE,
        ] {
            let summary: AnalysisRunSummary = result_from_fixture(fixture);

            assert!(summary.reading_model.is_none());
            assert!(summary.validation_removals.is_none());
        }
    }

    #[test]
    fn rejects_unknown_reading_area_shape_rather_than_defaulting() {
        let mut value = fixture_result(COMPLETED_WITH_READING_MODEL_FIXTURE);
        value["readingModel"]["areas"][0]["shape"] = serde_json::json!("spiral");

        serde_json::from_value::<AnalysisRunSummary>(value)
            .expect_err("an unknown reading area shape must be rejected, not defaulted");
    }

    #[test]
    fn rejects_poll_result_missing_the_required_reading_model_key() {
        let mut value = fixture_result(COMPLETED_FIXTURE);
        value
            .as_object_mut()
            .expect("the poll result must be an object")
            .remove("readingModel");

        serde_json::from_value::<AnalysisRunSummary>(value)
            .expect_err("a missing required readingModel key must be rejected");
    }

    #[test]
    fn rejects_unknown_state_discriminant_rather_than_defaulting() {
        let malformed =
            COMPLETED_FIXTURE.replace("\"state\":\"completed\"", "\"state\":\"unknownState\"");
        let value = fixture_result(&malformed);

        serde_json::from_value::<AnalysisRunSummary>(value)
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
        serde_json::from_value::<AnalysisRunSummary>(fixture_result(&negative_limitation))
            .expect_err("a negative limitation count must be rejected");
    }

    #[test]
    fn rejects_malformed_snapshot_id() {
        let malformed = CAPTURED_FIXTURE.replace(
            "\"snapshotId\":\"7198a1b2-3c4d-4e5f-8a9b-0123456789ab\"",
            "\"snapshotId\":\"snapshot-1\"",
        );

        serde_json::from_value::<AnalysisRunSummary>(fixture_result(&malformed))
            .expect_err("a malformed snapshot id must be rejected");
    }

    fn reading_model_from_fixture(fixture: &str) -> ReadingModel {
        let summary: AnalysisRunSummary = result_from_fixture(fixture);

        summary
            .reading_model
            .expect("the fixture must carry a reading model")
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
