use crate::engine_information::{EngineCommandError, EngineInformation};

pub trait EngineInformationService: Send + Sync {
    fn get_information(&self) -> Result<EngineInformation, EngineCommandError>;
}
