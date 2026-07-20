use crate::engine_information::EngineInformationService;
use std::sync::Arc;

pub struct EngineState(Arc<dyn EngineInformationService>);

impl EngineState {
    pub fn new(engine_information_service: Arc<dyn EngineInformationService>) -> Self {
        Self(engine_information_service)
    }

    pub(crate) fn service(&self) -> Arc<dyn EngineInformationService> {
        Arc::clone(&self.0)
    }
}
