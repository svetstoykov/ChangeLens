mod comparison_check_freshness_parameters;
mod comparison_freshness;
mod comparison_list_targets_parameters;
mod comparison_prepare_parameters;
mod comparison_readiness;
mod comparison_target;
mod comparison_target_kind;
mod comparison_target_page;
mod prepared_comparison;
mod validation;

pub(crate) use comparison_check_freshness_parameters::ComparisonCheckFreshnessParameters;
pub use comparison_freshness::ComparisonFreshness;
pub(crate) use comparison_list_targets_parameters::ComparisonListTargetsParameters;
pub(crate) use comparison_prepare_parameters::ComparisonPrepareParameters;
pub use comparison_readiness::ComparisonReadiness;
pub use comparison_target::ComparisonTarget;
pub use comparison_target_kind::ComparisonTargetKind;
pub use comparison_target_page::ComparisonTargetPage;
pub use prepared_comparison::PreparedComparison;

#[cfg(test)]
mod tests {
    use super::{
        ComparisonFreshness, ComparisonReadiness, ComparisonTargetKind, ComparisonTargetPage,
        PreparedComparison,
    };

    const FIRST_PAGE_FIXTURE: &str = include_str!(concat!(
        env!("CARGO_MANIFEST_DIR"),
        "/../../../contracts/engine-protocol/v1/fixtures/comparisons-list-targets.first-page.result.json"
    ));
    const EMPTY_PAGE_FIXTURE: &str = include_str!(concat!(
        env!("CARGO_MANIFEST_DIR"),
        "/../../../contracts/engine-protocol/v1/fixtures/comparisons-list-targets.empty.result.json"
    ));
    const READY_FIXTURE: &str = include_str!(concat!(
        env!("CARGO_MANIFEST_DIR"),
        "/../../../contracts/engine-protocol/v1/fixtures/comparisons-prepare.ready.result.json"
    ));
    const EMPTY_FIXTURE: &str = include_str!(concat!(
        env!("CARGO_MANIFEST_DIR"),
        "/../../../contracts/engine-protocol/v1/fixtures/comparisons-prepare.empty.result.json"
    ));
    const CONFLICTS_FIXTURE: &str = include_str!(concat!(
        env!("CARGO_MANIFEST_DIR"),
        "/../../../contracts/engine-protocol/v1/fixtures/comparisons-prepare.conflicts.result.json"
    ));
    const CURRENT_FIXTURE: &str = include_str!(concat!(
        env!("CARGO_MANIFEST_DIR"),
        "/../../../contracts/engine-protocol/v1/fixtures/comparisons-check-freshness.current.result.json"
    ));
    const STALE_FIXTURE: &str = include_str!(concat!(
        env!("CARGO_MANIFEST_DIR"),
        "/../../../contracts/engine-protocol/v1/fixtures/comparisons-check-freshness.stale.result.json"
    ));

    #[test]
    fn deserializes_every_shared_comparison_success_fixture() {
        let first_page: ComparisonTargetPage = result_from_fixture(FIRST_PAGE_FIXTURE);
        let empty_page: ComparisonTargetPage = result_from_fixture(EMPTY_PAGE_FIXTURE);
        let ready: PreparedComparison = result_from_fixture(READY_FIXTURE);
        let empty: PreparedComparison = result_from_fixture(EMPTY_FIXTURE);
        let conflicts: PreparedComparison = result_from_fixture(CONFLICTS_FIXTURE);
        let current: ComparisonFreshness = result_from_fixture(CURRENT_FIXTURE);
        let stale: ComparisonFreshness = result_from_fixture(STALE_FIXTURE);

        assert_eq!(first_page.targets.len(), 2);
        assert_eq!(first_page.targets[0].kind, ComparisonTargetKind::Local);
        assert_eq!(
            first_page.targets[1].kind,
            ComparisonTargetKind::RemoteTracking
        );
        assert_eq!(
            first_page.suggested_target,
            Some(first_page.targets[1].clone())
        );
        assert_eq!(
            first_page.next_cursor.as_deref(),
            Some("refs/remotes/origin/main")
        );
        assert!(empty_page.targets.is_empty());
        assert!(empty_page.suggested_target.is_none());
        assert!(empty_page.next_cursor.is_none());
        assert_eq!(ready.current_work_commit_count, 3);
        assert!(matches!(ready.readiness, ComparisonReadiness::Ready));
        assert!(matches!(empty.readiness, ComparisonReadiness::Empty));
        assert_eq!(
            conflicts.readiness,
            ComparisonReadiness::Conflicts {
                conflicted_file_count: 2
            }
        );
        assert_eq!(current, ComparisonFreshness::Current);
        assert_eq!(stale, ComparisonFreshness::Stale);
    }

    #[test]
    fn rejects_malformed_comparison_success_shapes() {
        for page in [
            r#"{}"#,
            r#"{"targets":[],"suggestedTarget":null,"nextCursor":null,"targetSetToken":"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef","unsupportedTargetCount":0,"extra":true}"#,
            r#"{"targets":[{"kind":"other","name":"main","fullName":"refs/heads/main","revision":"0123456789abcdef0123456789abcdef01234567"}],"suggestedTarget":null,"nextCursor":null,"targetSetToken":"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef","unsupportedTargetCount":0}"#,
            r#"{"targets":[{"kind":"local","name":"main","fullName":"refs/tags/main","revision":"0123456789abcdef0123456789abcdef01234567"}],"suggestedTarget":null,"nextCursor":null,"targetSetToken":"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef","unsupportedTargetCount":0}"#,
            r#"{"targets":[],"suggestedTarget":null,"nextCursor":null,"targetSetToken":"ABCDEF0123456789abcdef0123456789abcdef0123456789abcdef01234567","unsupportedTargetCount":0}"#,
        ] {
            serde_json::from_str::<ComparisonTargetPage>(page)
                .expect_err("invalid target-page data must be rejected");
        }

        for readiness in [
            r#"{"state":"ready","conflictedFileCount":0}"#,
            r#"{"state":"empty","conflictedFileCount":0}"#,
            r#"{"state":"conflicts"}"#,
            r#"{"state":"unknown"}"#,
        ] {
            serde_json::from_str::<ComparisonReadiness>(readiness)
                .expect_err("invalid readiness data must be rejected");
        }
    }

    #[test]
    fn rejects_unsafe_revisions_refs_tokens_and_counts() {
        for replacement in ["-1", "1.5", "2147483648"] {
            let result = READY_FIXTURE.replace(
                "\"currentWorkCommitCount\": 3",
                &format!("\"currentWorkCommitCount\": {replacement}"),
            );
            let value = fixture_result(&result);
            serde_json::from_value::<PreparedComparison>(value)
                .expect_err("unsafe counts must be rejected");
        }

        for (from, to) in [
            ("refs/remotes/origin/main", "refs/tags/main"),
            (
                "89abcdef0123456789abcdef0123456789abcdef",
                "89ABCDEF0123456789abcdef0123456789abcdef",
            ),
            ("fedcba9876543210fedcba9876543210fedcba98", "fedcba98"),
            (
                "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
                "not-a-token",
            ),
        ] {
            let result = READY_FIXTURE.replace(from, to);
            let value = fixture_result(&result);
            serde_json::from_value::<PreparedComparison>(value)
                .expect_err("unsafe comparison facts must be rejected");
        }
    }

    fn result_from_fixture<T: serde::de::DeserializeOwned>(fixture: &str) -> T {
        serde_json::from_value(fixture_result(fixture))
            .expect("the canonical shared comparison fixture must deserialize")
    }

    fn fixture_result(fixture: &str) -> serde_json::Value {
        let envelope: serde_json::Value =
            serde_json::from_str(fixture).expect("the shared comparison fixture must be JSON");
        envelope["result"].clone()
    }
}
