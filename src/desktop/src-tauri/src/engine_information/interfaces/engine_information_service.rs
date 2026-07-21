use crate::engine_information::EngineInformation;
use crate::engine_protocol::EngineActionError;

pub trait EngineInformationService: Send + Sync {
    fn get_information(&self) -> Result<EngineInformation, EngineActionError>;
}
