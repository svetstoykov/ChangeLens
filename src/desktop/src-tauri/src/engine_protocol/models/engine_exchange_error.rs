use crate::engine_protocol::EngineActionError;

#[derive(Debug)]
pub(crate) struct EngineExchangeError {
    action_error: EngineActionError,
    invalidates_process: bool,
}

impl EngineExchangeError {
    pub(crate) fn invalidating(action_error: EngineActionError) -> Self {
        Self {
            action_error,
            invalidates_process: true,
        }
    }

    pub(crate) fn recoverable(action_error: EngineActionError) -> Self {
        Self {
            action_error,
            invalidates_process: false,
        }
    }

    pub(crate) fn invalidates_process(&self) -> bool {
        self.invalidates_process
    }

    pub(crate) fn into_action_error(self) -> EngineActionError {
        self.action_error
    }
}
