import type { ReadingReviewRecommendation } from "./ReadingReviewRecommendation";
import type { ReadingReviewStatus } from "./ReadingReviewStatus";

export interface ReadingReview {
  readonly status: ReadingReviewStatus;
  readonly recommendation: ReadingReviewRecommendation | null;
  readonly withheldCount: number;
}
