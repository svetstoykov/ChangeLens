import type { ReadingSide } from "./ReadingSide";

export interface ReadingEvidence {
  readonly nodeId: string;
  readonly path: string;
  readonly side: ReadingSide;
  readonly startLine: number;
  readonly endLine: number;
  readonly isChangedFile: boolean;
  readonly isRedacted: boolean;
  readonly isTruncated: boolean;
  readonly text: string;
}
