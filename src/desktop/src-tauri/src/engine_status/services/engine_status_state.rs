use crate::engine_status::EngineStatusService;
use std::sync::Arc;

pub struct EngineStatusState(Arc<dyn EngineStatusService>);

impl EngineStatusState {
    pub fn new(engine_status_service: Arc<dyn EngineStatusService>) -> Self {
        Self(engine_status_service)
    }

    pub(crate) fn service(&self) -> Arc<dyn EngineStatusService> {
        Arc::clone(&self.0)
    }
}
