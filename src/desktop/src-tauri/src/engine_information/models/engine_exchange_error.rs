use crate::engine_information::EngineCommandError;

pub(crate) struct EngineExchangeError {
    command_error: EngineCommandError,
    invalidates_process: bool,
}

impl EngineExchangeError {
    pub(crate) fn invalidating(command_error: EngineCommandError) -> Self {
        Self {
            command_error,
            invalidates_process: true,
        }
    }

    pub(crate) fn recoverable(command_error: EngineCommandError) -> Self {
        Self {
            command_error,
            invalidates_process: false,
        }
    }

    pub(crate) fn invalidates_process(&self) -> bool {
        self.invalidates_process
    }

    pub(crate) fn into_command_error(self) -> EngineCommandError {
        self.command_error
    }
}
