import type { ReadingFocusRange } from "../Models/ReadingFocusRange";

export interface QuoteFocusSegment {
  readonly key: string;
  readonly text: string;
  readonly focused: boolean;
}

interface ResolvedRange {
  readonly start: number;
  readonly end: number;
}

export function splitQuoteByFocus(
  text: string,
  startLine: number,
  nodeId: string,
  focus: readonly ReadingFocusRange[],
): readonly QuoteFocusSegment[] {
  const lines = text.split("\n");
  const lineOffsets: number[] = [];
  let offset = 0;
  for (const line of lines) {
    lineOffsets.push(offset);
    offset += line.length + 1;
  }

  const lastLineIndex = lines.length - 1;
  const ranges = resolveRanges(focus, nodeId, startLine, lastLineIndex);

  if (ranges.length === 0) {
    return [{ key: "plain-0", text, focused: false }];
  }

  const segments: QuoteFocusSegment[] = [];
  let cursor = 0;

  ranges.forEach((range, index) => {
    const startOffset = lineOffsets[range.start] ?? text.length;
    const endOffset =
      range.end === lastLineIndex
        ? text.length
        : (lineOffsets[range.end + 1] ?? text.length + 1) - 1;

    if (startOffset > cursor) {
      segments.push({
        key: `plain-${index}`,
        text: text.slice(cursor, startOffset),
        focused: false,
      });
    }

    segments.push({
      key: `focus-${range.start}-${range.end}`,
      text: text.slice(startOffset, endOffset),
      focused: true,
    });

    cursor = endOffset;
  });

  if (cursor < text.length) {
    segments.push({
      key: "plain-tail",
      text: text.slice(cursor),
      focused: false,
    });
  }

  return segments;
}

function resolveRanges(
  focus: readonly ReadingFocusRange[],
  nodeId: string,
  startLine: number,
  lastLineIndex: number,
): readonly ResolvedRange[] {
  const candidates = focus
    .filter((range) => range.nodeId === nodeId)
    .map((range) => ({
      start: range.startLine - startLine,
      end: range.endLine - startLine,
    }))
    .filter((range) => range.end >= 0 && range.start <= lastLineIndex)
    .sort((left, right) => left.start - right.start);

  const ranges: ResolvedRange[] = [];
  let previousEnd = -1;

  for (const candidate of candidates) {
    const start = Math.max(0, candidate.start, previousEnd + 1);
    const end = Math.min(lastLineIndex, candidate.end);
    if (start > end) continue;
    ranges.push({ start, end });
    previousEnd = end;
  }

  return ranges;
}
