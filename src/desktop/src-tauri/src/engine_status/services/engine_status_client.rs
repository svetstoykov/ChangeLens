use crate::engine_protocol::{EngineActionError, EngineClient};
use crate::engine_status::EngineStatusService;
use crate::engine_status::constants::ENGINE_STATUS_ACTION;

impl EngineStatusService for EngineClient {
    fn check_status(&self) -> Result<(), EngineActionError> {
        self.execute_action::<(), ()>(ENGINE_STATUS_ACTION, None)
    }
}
