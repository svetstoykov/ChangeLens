import { invoke } from "@tauri-apps/api/core";
import { normalizeActionError } from "../../Actions/Services/normalizeActionError";
import type { ComparisonClient } from "../Interfaces/ComparisonClient";
import type { ComparisonFreshness } from "../Models/ComparisonFreshness";
import type { ComparisonTargetPage } from "../Models/ComparisonTargetPage";
import type { PreparedComparison } from "../Models/PreparedComparison";

export class TauriComparisonClient implements ComparisonClient {
  async listTargets(request: {
    readonly path: string;
    readonly query?: string;
    readonly after?: string;
    readonly targetSetToken?: string;
  }): Promise<ComparisonTargetPage> {
    const { path, query, after, targetSetToken } = request;
    const arguments_ = {
      path,
      ...(query === undefined ? {} : { query }),
      ...(after === undefined ? {} : { after }),
      ...(targetSetToken === undefined ? {} : { targetSetToken }),
    };
    return invoke<ComparisonTargetPage>(
      "comparison_list_targets",
      arguments_,
    ).catch((error: unknown) => {
      throw normalizeActionError(error);
    });
  }

  async prepare(request: {
    readonly path: string;
    readonly target: string;
  }): Promise<PreparedComparison> {
    return invoke<PreparedComparison>("comparison_prepare", request).catch(
      (error: unknown) => {
        throw normalizeActionError(error);
      },
    );
  }

  async checkFreshness(request: {
    readonly path: string;
    readonly target: string;
    readonly freshnessToken: string;
  }): Promise<ComparisonFreshness> {
    return invoke<ComparisonFreshness>(
      "comparison_check_freshness",
      request,
    ).catch((error: unknown) => {
      throw normalizeActionError(error);
    });
  }
}
