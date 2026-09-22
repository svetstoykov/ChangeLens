"""Exception types that separate harness faults from product observations."""


class HarnessError(Exception):
    """The review harness failed, so the product was not observed."""


class CaseInterrupted(Exception):
    """A product observation stopped a case early; its expectations are still evaluated."""


class SpecError(Exception):
    """A plan, fixture, or script definition is invalid."""

    def __init__(self, issues: list[str]) -> None:
        super().__init__("\n".join(issues))
        self.issues = issues
