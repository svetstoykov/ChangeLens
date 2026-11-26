import type { InvokeArgs } from "@tauri-apps/api/core";
import type { AnalysisGetActiveResult } from "../../Analysis/Models/AnalysisGetActiveResult";
import {
  ANALYSIS_ACTIVE_STAGE_ORDER,
  type AnalysisRunState,
} from "../../Analysis/Models/AnalysisRunState";
import type { AnalysisRunSummary } from "../../Analysis/Models/AnalysisRunSummary";
import type { AnalysisStartResult } from "../../Analysis/Models/AnalysisStartResult";
import type { ComparisonTargetPage } from "../../Comparisons/Models/ComparisonTargetPage";
import type { PreparedComparison } from "../../Comparisons/Models/PreparedComparison";
import type { RemoteBaselineState } from "../../Comparisons/Models/RemoteBaselineState";
import type { OpenedRepository } from "../../Repositories/Models/OpenedRepository";
import type { RepositoryHistory } from "../../Repositories/Models/RepositoryHistory";
import type { RepositoryRestoreResult } from "../../Repositories/Models/RepositoryRestoreResult";
import type { ColorTheme } from "../../Visuals/Models/ColorTheme";
import {
  fakeReadingModel,
  fakeRepository,
  fakeRepositoryId,
  fakeTargets,
  prepareFakeComparison,
} from "./fakeSampleData";

const stageDurationMs = 1500;

interface FakeRun {
  readonly runId: string;
  readonly path: string;
  readonly prepared: PreparedComparison;
  readonly requestedAt: number;
  cancelledAt: number | null;
}

/**
 * In-memory stand-in for the Tauri commands the UI invokes, used when the UI
 * runs in a plain browser. Every command named in `failingCommands` rejects
 * with an operation error shaped like the one the Rust shell sends.
 */
export class FakeEngine {
  private readonly failingCommands: ReadonlySet<string>;
  private readonly runs = new Map<string, FakeRun>();
  private colorTheme: ColorTheme | null = null;
  private isInHistory = false;
  private lastOpenedAt = 0;
  private nextRunNumber = 1;

  constructor(failingCommands: ReadonlySet<string>) {
    this.failingCommands = failingCommands;
  }

  handle(command: string, args: InvokeArgs | undefined): unknown {
    if (this.failingCommands.has(command)) {
      throw {
        kind: "operation",
        requestId: `fake-request-${command}`,
        errors: [
          {
            type: "ExternalDependencyFailure",
            code: "fake.injectedFailure",
            message: `The fake engine failed ${command} on request.`,
          },
        ],
      };
    }

    const values = (args ?? {}) as Readonly<Record<string, unknown>>;

    switch (command) {
      case "engine_check_status":
        return null;
      case "preference_get_color_theme":
        return { colorTheme: this.colorTheme };
      case "preference_set_color_theme":
        this.colorTheme = values.colorTheme as ColorTheme;
        return null;
      case "select_repository_folder":
        return fakeRepository.canonicalPath;
      case "repository_open":
        return this.openRepository();
      case "repository_restore_last":
        return this.restoreLast();
      case "repository_list_recent":
        return this.listRecent();
      case "repository_remove_recent":
        this.isInHistory = false;
        return null;
      case "comparison_list_targets":
        return this.listTargets(values.query as string | undefined);
      case "comparison_prepare":
        return prepareFakeComparison(this.findTarget(values.target as string));
      case "comparison_check_freshness":
        return { state: "current" };
      case "comparison_check_remote_baseline":
        return this.checkRemoteBaseline(values.target as string);
      case "comparison_refresh_remote_baseline":
        return {
          remoteRevision: this.findTarget(values.target as string).revision,
        };
      case "analysis_start":
        return this.startAnalysis(
          values.path as string,
          values.target as string,
        );
      case "analysis_get_active":
        return this.getActive(values.path as string);
      case "analysis_poll_run":
        return this.summarize(this.findRun(values.runId as string));
      case "analysis_cancel":
        this.findRun(values.runId as string).cancelledAt ??= Date.now();
        return null;
      default:
        throw new Error(`The fake engine does not implement ${command}.`);
    }
  }

  private openRepository(): OpenedRepository {
    this.isInHistory = true;
    this.lastOpenedAt = Date.now();
    return {
      repositoryId: fakeRepositoryId,
      repository: fakeRepository,
      preferredTarget: null,
    };
  }

  private restoreLast(): RepositoryRestoreResult {
    return this.isInHistory
      ? {
          state: "restored",
          repositoryId: fakeRepositoryId,
          repository: fakeRepository,
          preferredTarget: null,
        }
      : { state: "none" };
  }

  private listRecent(): RepositoryHistory {
    return this.isInHistory
      ? {
          lastRepositoryId: fakeRepositoryId,
          repositories: [
            {
              repositoryId: fakeRepositoryId,
              name: fakeRepository.name,
              canonicalPath: fakeRepository.canonicalPath,
              lastOpenedAtUnixMilliseconds: this.lastOpenedAt,
              preferredTarget: null,
            },
          ],
        }
      : { lastRepositoryId: null, repositories: [] };
  }

  private listTargets(query: string | undefined): ComparisonTargetPage {
    const needle = query?.trim().toLowerCase() ?? "";
    const main = fakeTargets[0] ?? null;
    return {
      targets: fakeTargets.filter((target) =>
        target.name.toLowerCase().includes(needle),
      ),
      suggestedTarget: needle.length === 0 ? main : null,
      nextCursor: null,
      targetSetToken: "fake-target-set",
      unsupportedTargetCount: 0,
    };
  }

  private findTarget(fullName: string) {
    const target = fakeTargets.find((item) => item.fullName === fullName);
    if (target === undefined) {
      throw new Error(`The fake engine has no target ${fullName}.`);
    }
    return target;
  }

  private checkRemoteBaseline(fullName: string): RemoteBaselineState {
    const target = this.findTarget(fullName);
    return target.kind === "remoteTracking"
      ? { state: "current", remoteRevision: target.revision }
      : { state: "noRemote" };
  }

  private startAnalysis(path: string, fullName: string): AnalysisStartResult {
    const active = this.findActiveRun(path);
    if (active !== undefined) {
      return { state: "rejectedActive", activeRunId: active.runId };
    }

    const run: FakeRun = {
      runId: `fake-run-${this.nextRunNumber++}`,
      path,
      prepared: prepareFakeComparison(this.findTarget(fullName)),
      requestedAt: Date.now(),
      cancelledAt: null,
    };
    this.runs.set(run.runId, run);
    return {
      state: "accepted",
      runId: run.runId,
      requestedAt: run.requestedAt,
    };
  }

  private getActive(path: string): AnalysisGetActiveResult {
    const active = this.findActiveRun(path);
    return active === undefined
      ? { state: "none" }
      : { state: "active", run: this.summarize(active) };
  }

  private findActiveRun(path: string): FakeRun | undefined {
    return [...this.runs.values()].find(
      (run) => run.path === path && !this.isTerminal(run),
    );
  }

  private findRun(runId: string): FakeRun {
    const run = this.runs.get(runId);
    if (run === undefined) {
      throw new Error(`The fake engine has no run ${runId}.`);
    }
    return run;
  }

  private isTerminal(run: FakeRun): boolean {
    const state = this.stateOf(run);
    return state === "completed" || state === "cancelled";
  }

  private stateOf(run: FakeRun): AnalysisRunState {
    if (run.cancelledAt !== null) {
      return "cancelled";
    }
    const stageIndex = Math.floor(
      (Date.now() - run.requestedAt) / stageDurationMs,
    );
    return ANALYSIS_ACTIVE_STAGE_ORDER[stageIndex] ?? "completed";
  }

  private summarize(run: FakeRun): AnalysisRunSummary {
    const state = this.stateOf(run);
    const captureStartedAt = run.requestedAt;
    const capturedAt =
      state === "capturing" ? null : run.requestedAt + stageDurationMs;
    const terminalAt =
      run.cancelledAt ??
      run.requestedAt + stageDurationMs * ANALYSIS_ACTIVE_STAGE_ORDER.length;

    return {
      runId: run.runId,
      state,
      repository: {
        repositoryId: fakeRepositoryId,
        displayName: fakeRepository.name,
        canonicalPath: run.path,
        head: fakeRepository.head.revision,
      },
      comparison: {
        target: run.prepared.target.fullName,
        targetRevision: run.prepared.target.revision,
        freshnessToken: run.prepared.freshnessToken,
      },
      requestedAt: run.requestedAt,
      captureStartedAt,
      capturedAt,
      snapshotId: capturedAt === null ? null : `fake-snapshot-${run.runId}`,
      cancellationRequested: run.cancelledAt !== null,
      facts:
        capturedAt === null
          ? []
          : [
              {
                kind: "changedFilesCaptured",
                count: run.prepared.changedFileTotal,
                detail: null,
              },
              {
                kind: "excludedUncommittedFiles",
                count: run.prepared.uncommittedFileTotal,
                detail: "Uncommitted work is not part of the analysis.",
              },
            ],
      terminal:
        state === "completed"
          ? { kind: "completed", terminalAt }
          : state === "cancelled"
            ? { kind: "cancelled", terminalAt }
            : null,
      interruptedAt: null,
      interruptionReason: null,
      readingModel: state === "completed" ? fakeReadingModel : null,
      validationRemovals: state === "completed" ? [] : null,
    };
  }
}
