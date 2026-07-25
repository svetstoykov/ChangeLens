import type { ReactNode } from "react";

export type IconName =
  | "branch"
  | "check"
  | "chevronDown"
  | "commit"
  | "compare"
  | "conflict"
  | "currentChange"
  | "detached"
  | "file"
  | "folder"
  | "info"
  | "logo"
  | "refresh"
  | "shield"
  | "warning";

interface IconProps {
  readonly name: IconName;
  readonly className?: string;
}

export function Icon({ name, className }: IconProps) {
  const classes = className ? `icon ${className}` : "icon";

  return (
    <svg
      className={classes}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.8"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
      focusable="false"
    >
      <IconGlyph name={name} />
    </svg>
  );
}

function IconGlyph({ name }: { readonly name: IconName }): ReactNode {
  switch (name) {
    case "branch":
      return (
        <>
          <circle cx="6" cy="5" r="2" />
          <circle cx="18" cy="7" r="2" />
          <circle cx="6" cy="19" r="2" />
          <path d="M6 7v10M8 10h4a6 6 0 0 0 6-6" />
        </>
      );
    case "check":
      return (
        <>
          <circle cx="12" cy="12" r="9" />
          <path d="m8 12 2.5 2.5L16.5 8.5" />
        </>
      );
    case "chevronDown":
      return <path d="m6 9 6 6 6-6" />;
    case "commit":
      return (
        <>
          <path d="M3 12h5M16 12h5" />
          <circle cx="12" cy="12" r="4" />
        </>
      );
    case "compare":
      return (
        <>
          <path d="M7 4v13M4 14l3 3 3-3M17 20V7M14 10l3-3 3 3" />
        </>
      );
    case "conflict":
      return (
        <>
          <path d="M7 3v5a4 4 0 0 0 4 4h6" />
          <path d="m14 9 3 3-3 3M7 21v-5a4 4 0 0 1 4-4" />
          <circle cx="7" cy="3" r="2" />
          <circle cx="7" cy="21" r="2" />
        </>
      );
    case "currentChange":
      return (
        <>
          <rect x="3" y="4" width="7" height="6" rx="1" />
          <rect x="14" y="14" width="7" height="6" rx="1" />
          <path d="M10 7h4v10M14 17h-4" />
        </>
      );
    case "detached":
      return (
        <>
          <circle cx="6" cy="6" r="2" />
          <circle cx="18" cy="18" r="2" />
          <path d="M7.5 7.5 16.5 16.5" strokeDasharray="2.5 2.5" />
          <path d="m15 6 3-3 3 3M18 3v7" />
        </>
      );
    case "file":
      return (
        <>
          <path d="M6 3h8l4 4v14H6z" />
          <path d="M14 3v5h5M9 13h6M9 17h6" />
        </>
      );
    case "folder":
      return (
        <>
          <path d="M3.5 6.5h6l2 2h9v9.5a2 2 0 0 1-2 2h-13a2 2 0 0 1-2-2V6.5Z" />
          <path d="M3.5 10h17" />
        </>
      );
    case "info":
      return (
        <>
          <circle cx="12" cy="12" r="9" />
          <path d="M12 11v5M12 8h.01" />
        </>
      );
    case "logo":
      return (
        <>
          <circle cx="10.5" cy="10.5" r="6.5" />
          <path d="m15.3 15.3 4.2 4.2M8 10.5h5M10.5 8v5" />
        </>
      );
    case "refresh":
      return (
        <>
          <path d="M20 11a8 8 0 0 0-14.7-4.4L3 10" />
          <path d="M3 5v5h5M4 13a8 8 0 0 0 14.7 4.4L21 14" />
          <path d="M21 19v-5h-5" />
        </>
      );
    case "shield":
      return (
        <>
          <path d="M12 3 5 6v5c0 4.6 2.8 8.2 7 10 4.2-1.8 7-5.4 7-10V6l-7-3Z" />
          <path d="m9 12 2 2 4-4" />
        </>
      );
    case "warning":
      return (
        <>
          <path d="m10.3 4.7-7.5 13A2 2 0 0 0 4.5 21h15a2 2 0 0 0 1.7-3.3l-7.5-13a2 2 0 0 0-3.4 0Z" />
          <path d="M12 9v4M12 17h.01" />
        </>
      );
  }
}
