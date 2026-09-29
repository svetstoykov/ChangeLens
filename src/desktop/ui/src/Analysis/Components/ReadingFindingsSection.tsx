import type { ReadingAssuranceKind } from "../Models/ReadingAssuranceKind";
import type { ReadingFindingSeverity } from "../Models/ReadingFindingSeverity";
import type { ReadingModel } from "../Models/ReadingModel";
import type { ReadingReviewRecommendation } from "../Models/ReadingReviewRecommendation";
import type { ReadingReviewStatus } from "../Models/ReadingReviewStatus";
import { describeFindingSeverity } from "./describeFindingSeverity";
import { ReadingFindingItem } from "./ReadingFindingItem";

export interface ReadingFindingsSectionProps {
  readonly model: ReadingModel;
}

const severityOrder: readonly ReadingFindingSeverity[] = [
  "critical",
  "warning",
  "info",
];

const acrossTheChange = "Across the change";

export function ReadingFindingsSection({ model }: ReadingFindingsSectionProps) {
  const { review, findings } = model;
  const recommendation =
    review.status === "ran" && review.recommendation !== null
      ? review.recommendation
      : null;
  const areaNamesById = new Map(
    model.areas.map((area) => [area.id, area.title] as const),
  );
  const nameForArea = (areaId: string | null): string =>
    areaId === null ? acrossTheChange : (areaNamesById.get(areaId) ?? areaId);
  const groups = severityOrder.flatMap((severity) => {
    const inGroup = findings.filter((finding) => finding.severity === severity);
    return inGroup.length > 0 ? [{ severity, findings: inGroup }] : [];
  });

  return (
    <section
      className="reading-findings"
      aria-labelledby="reading-findings-heading"
    >
      <h3 id="reading-findings-heading">Findings</h3>
      {recommendation !== null ? (
        <div className="reading-findings-summary">
          <p
            className="reading-findings-recommendation"
            data-recommendation={recommendation}
          >
            {describeRecommendation(recommendation)}
          </p>
          <p className="reading-findings-note">
            A clean review isn&apos;t proof that there are no defects.
          </p>
          {review.withheldCount > 0 ? (
            <p className="reading-findings-note">
              {describeWithheld(review.withheldCount)}
            </p>
          ) : null}
        </div>
      ) : (
        <div className="reading-findings-summary">
          <p
            className="reading-findings-recommendation"
            data-recommendation="unavailable"
          >
            Findings unavailable
          </p>
          <p className="reading-findings-note">
            {unavailableReason(model, review.status)}
          </p>
        </div>
      )}
      {groups.map((group) => (
        <section
          key={group.severity}
          className="reading-finding-group"
          aria-labelledby={`reading-findings-${group.severity}-heading`}
        >
          <h4 id={`reading-findings-${group.severity}-heading`}>
            {describeFindingSeverity(group.severity)} ({group.findings.length})
          </h4>
          <ul className="reading-finding-list">
            {group.findings.map((finding) => (
              <ReadingFindingItem
                key={finding.id}
                finding={finding}
                areaName={nameForArea(finding.areaId)}
                citations={model.citations}
                evidence={model.evidence}
              />
            ))}
          </ul>
        </section>
      ))}
    </section>
  );
}

function describeRecommendation(
  recommendation: ReadingReviewRecommendation,
): string {
  switch (recommendation) {
    case "defectsToFix":
      return "Defects to fix";
    case "issuesWorthAddressing":
      return "Issues worth addressing";
    case "noDefectsFound":
      return "No defects found";
    case "noDefectsConfirmed":
      return "No defects confirmed; this review is incomplete.";
  }
}

function describeWithheld(count: number): string {
  return count === 1
    ? "1 finding was withheld because it did not meet publication requirements."
    : `${count} findings were withheld because they did not meet publication requirements.`;
}

function unavailableReason(
  model: ReadingModel,
  status: ReadingReviewStatus,
): string {
  const kind = unavailableAssuranceKind(status);
  const assurance =
    kind === null
      ? undefined
      : model.assurances.find((candidate) => candidate.kind === kind);
  return assurance?.detail ?? "The review did not produce a result.";
}

function unavailableAssuranceKind(
  status: ReadingReviewStatus,
): ReadingAssuranceKind | null {
  switch (status) {
    case "notRun":
      return "reviewNotRun";
    case "failed":
      return "reviewFailed";
    case "tooLarge":
      return "reviewTooLarge";
    case "ran":
      return null;
  }
}
