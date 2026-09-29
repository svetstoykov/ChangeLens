import type { ReadingFindingSeverity } from "../Models/ReadingFindingSeverity";

export function describeFindingSeverity(
  severity: ReadingFindingSeverity,
): string {
  switch (severity) {
    case "critical":
      return "Critical";
    case "warning":
      return "Warning";
    case "info":
      return "Info";
  }
}
