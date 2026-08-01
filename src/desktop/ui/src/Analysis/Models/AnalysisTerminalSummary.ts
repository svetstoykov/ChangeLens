export type AnalysisTerminalSummary =
  | { readonly kind: "completed"; readonly terminalAt: number }
  | {
      readonly kind: "completedWithLimitations";
      readonly terminalAt: number;
      readonly limitationCount: number;
    }
  | { readonly kind: "cancelled"; readonly terminalAt: number }
  | {
      readonly kind: "failed";
      readonly terminalAt: number;
      readonly failureCode: string;
    };
