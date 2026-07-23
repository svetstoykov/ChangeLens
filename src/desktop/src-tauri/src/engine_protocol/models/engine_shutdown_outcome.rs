/// Defines how the owned Engine process completed shutdown.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub(crate) enum EngineShutdownOutcome {
    /// The process exited after its protocol input closed.
    Graceful,

    /// The process required force termination after the grace period.
    Forced,
}
