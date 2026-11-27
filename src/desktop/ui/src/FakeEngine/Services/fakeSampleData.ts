import type { ReadingModel } from "../../Analysis/Models/ReadingModel";
import type { ComparisonTarget } from "../../Comparisons/Models/ComparisonTarget";
import type { PreparedComparison } from "../../Comparisons/Models/PreparedComparison";
import type { RepositoryDescriptor } from "../../Repositories/Models/RepositoryDescriptor";

export const fakeRepositoryId = "fake-repository-orders-service";

export const fakeRepository: RepositoryDescriptor = {
  name: "orders-service",
  canonicalPath: "/Users/demo/projects/orders-service",
  head: {
    kind: "branch",
    name: "feature/retry-payments",
    revision: "4f1c2a9d8e7b6c5a4f3e2d1c0b9a8f7e6d5c4b3a",
  },
};

export const fakeTargets: readonly ComparisonTarget[] = [
  {
    kind: "local",
    name: "main",
    fullName: "refs/heads/main",
    revision: "a1b2c3d4e5f60718293a4b5c6d7e8f9012345678",
  },
  {
    kind: "remoteTracking",
    name: "origin/main",
    fullName: "refs/remotes/origin/main",
    revision: "b2c3d4e5f60718293a4b5c6d7e8f901234567890",
  },
  {
    kind: "local",
    name: "release/1.4",
    fullName: "refs/heads/release/1.4",
    revision: "c3d4e5f60718293a4b5c6d7e8f90123456789012",
  },
  {
    kind: "local",
    name: "feature/retry-payments",
    fullName: "refs/heads/feature/retry-payments",
    revision: fakeRepository.head.revision,
  },
];

export function prepareFakeComparison(
  target: ComparisonTarget,
): PreparedComparison {
  const isSameAsHead = target.revision === fakeRepository.head.revision;
  const hasConflicts = target.name === "release/1.4";

  return {
    repository: fakeRepository,
    target,
    mergeBaseRevision: "d4e5f60718293a4b5c6d7e8f9012345678901234",
    currentWorkCommitCount: isSameAsHead ? 0 : 3,
    targetOnlyCommitCount: isSameAsHead ? 0 : hasConflicts ? 5 : 1,
    changedFileTotal: isSameAsHead ? 0 : 6,
    uncommittedFileTotal: 2,
    stagedFileCount: 1,
    unstagedFileCount: 1,
    untrackedFileCount: 0,
    readiness: isSameAsHead
      ? { state: "empty" }
      : hasConflicts
        ? { state: "conflicts", conflictedFileCount: 2 }
        : { state: "ready" },
    freshnessToken: `fake-freshness-${target.name}`,
  };
}

export const fakeReadingModel: ReadingModel = {
  thesis: {
    claimId: "claim-thesis",
    text: "Payment capture now retries transient gateway failures up to three times before marking the order as failed.",
    trust: "checked",
    evidenceNodeIds: ["node-retry-policy"],
  },
  areas: [
    {
      id: "area-retry",
      title: "Retry policy for payment capture",
      summary: {
        claimId: "claim-area-retry",
        text: "A new retry policy wraps the gateway call and backs off exponentially.",
        trust: "checked",
        evidenceNodeIds: ["node-retry-policy"],
      },
      shape: "walk",
      participants: [
        {
          id: "participant-capture",
          name: "PaymentCaptureService",
          role: "Calls the gateway and records the outcome",
          changed: true,
          evidenceNodeIds: ["node-capture-service"],
        },
        {
          id: "participant-policy",
          name: "RetryPolicy",
          role: "Decides whether and when to retry",
          changed: true,
          evidenceNodeIds: ["node-retry-policy"],
        },
      ],
      relationships: [],
      orderedSteps: [
        {
          claimId: "claim-step-1",
          text: "PaymentCaptureService asks RetryPolicy whether a failed capture may be retried.",
          trust: "checked",
          evidenceNodeIds: ["node-capture-service"],
        },
        {
          claimId: "claim-step-2",
          text: "RetryPolicy allows three attempts with a doubling delay starting at 200 ms.",
          trust: "derived",
          evidenceNodeIds: ["node-retry-policy"],
        },
      ],
      purposes: [],
    },
    {
      id: "area-config",
      title: "Configuration",
      summary: {
        claimId: "claim-area-config",
        text: "The retry limit is read from configuration with a default of three.",
        trust: "unchecked",
        evidenceNodeIds: [],
      },
      shape: "participantList",
      participants: [
        {
          id: "participant-options",
          name: "PaymentOptions",
          role: "Holds the retry limit",
          changed: true,
          evidenceNodeIds: [],
        },
      ],
      relationships: [],
      orderedSteps: [],
      purposes: [],
    },
  ],
  citations: [
    {
      claimId: "claim-thesis",
      nodeId: "node-retry-policy",
      side: "after",
      path: "src/Payments/RetryPolicy.cs",
      objectId: "e5f60718293a4b5c6d7e8f901234567890123456",
      startLine: 12,
      endLine: 20,
      focus: [{ nodeId: "node-retry-policy", startLine: 14, endLine: 16 }],
      provenance: "checked",
    },
    {
      claimId: "claim-step-1",
      nodeId: "node-capture-service",
      side: "after",
      path: "src/Payments/PaymentCaptureService.cs",
      objectId: "f60718293a4b5c6d7e8f9012345678901234567a",
      startLine: 40,
      endLine: 46,
      focus: [],
      provenance: "checked",
    },
  ],
  evidence: [
    {
      nodeId: "node-retry-policy",
      path: "src/Payments/RetryPolicy.cs",
      side: "after",
      startLine: 12,
      endLine: 20,
      isChangedFile: true,
      isRedacted: false,
      isTruncated: false,
      text: [
        "public bool ShouldRetry(int attempt, Exception error)",
        "{",
        "    if (attempt >= _options.MaxAttempts)",
        "        return false;",
        "    return error is GatewayTimeoutException;",
        "}",
        "",
        "public TimeSpan DelayFor(int attempt) =>",
        "    TimeSpan.FromMilliseconds(200 * Math.Pow(2, attempt));",
      ].join("\n"),
    },
    {
      nodeId: "node-capture-service",
      path: "src/Payments/PaymentCaptureService.cs",
      side: "after",
      startLine: 40,
      endLine: 46,
      isChangedFile: true,
      isRedacted: false,
      isTruncated: false,
      text: [
        "catch (Exception error) when (_retryPolicy.ShouldRetry(attempt, error))",
        "{",
        "    await Task.Delay(_retryPolicy.DelayFor(attempt), cancellationToken);",
        "    attempt++;",
        "}",
      ].join("\n"),
    },
  ],
  limitations: [
    {
      kind: "uncommittedWorkExcluded",
      path: null,
      detail: "2 uncommitted files were not part of this analysis.",
    },
  ],
  omissionSummaries: [],
  assurances: [
    {
      kind: "testsNotExecuted",
      detail: "ChangeLens does not run the repository's tests.",
      claimId: null,
    },
  ],
  findings: [
    {
      id: "finding-1",
      severity: "warning",
      title: "Retries ignore the cancellation of the order",
      trigger: "The customer cancels the order while a retry delay is pending.",
      impact: "The payment can still be captured after cancellation.",
      fix: "Check the order state before each retry attempt.",
      areaId: "area-retry",
    },
    {
      id: "finding-2",
      severity: "info",
      title: "Retry limit has no upper bound",
      trigger: "Configuration sets MaxAttempts to a large value.",
      impact: "A failing gateway is called many times per order.",
      fix: "Clamp MaxAttempts when the options are validated.",
      areaId: "area-config",
    },
  ],
  review: {
    status: "ran",
    recommendation: "issuesWorthAddressing",
    withheldCount: 0,
  },
};
