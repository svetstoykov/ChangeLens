use crate::engine_protocol::EngineActionError;

pub trait EngineStatusService: Send + Sync {
    fn check_status(&self) -> Result<(), EngineActionError>;
}
