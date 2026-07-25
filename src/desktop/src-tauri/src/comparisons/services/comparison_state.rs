use crate::comparisons::ComparisonService;
use std::sync::Arc;

/// Stores the comparison service shared by desktop comparison commands.
pub struct ComparisonState(Arc<dyn ComparisonService>);

impl ComparisonState {
    /// Creates comparison command state backed by `comparison_service`.
    pub fn new(comparison_service: Arc<dyn ComparisonService>) -> Self {
        Self(comparison_service)
    }

    /// Returns the configured comparison service.
    pub fn service(&self) -> Arc<dyn ComparisonService> {
        Arc::clone(&self.0)
    }
}

#[cfg(test)]
mod tests {
    use super::ComparisonState;
    use crate::comparisons::{
        ComparisonFreshness, ComparisonService, ComparisonTargetPage, PreparedComparison,
    };
    use crate::engine_protocol::{EngineActionError, EngineClient};
    use std::sync::Arc;

    struct ComparisonServiceFixture;

    impl ComparisonService for ComparisonServiceFixture {
        fn list_targets(
            &self,
            _path: &str,
            _query: Option<&str>,
            _after: Option<&str>,
            _target_set_token: Option<&str>,
        ) -> Result<ComparisonTargetPage, EngineActionError> {
            unreachable!("the state test does not execute comparison actions")
        }

        fn prepare(
            &self,
            _path: &str,
            _target: &str,
        ) -> Result<PreparedComparison, EngineActionError> {
            unreachable!("the state test does not execute comparison actions")
        }

        fn check_freshness(
            &self,
            _path: &str,
            _target: &str,
            _freshness_token: &str,
        ) -> Result<ComparisonFreshness, EngineActionError> {
            unreachable!("the state test does not execute comparison actions")
        }
    }

    #[test]
    fn clones_the_configured_comparison_service() {
        let state = ComparisonState::new(Arc::new(ComparisonServiceFixture));

        let first = state.service();
        let second = state.service();

        assert!(Arc::ptr_eq(&first, &second));
    }

    #[test]
    fn accepts_the_engine_client() {
        let _state = ComparisonState::new(Arc::new(EngineClient::new()));
    }
}
