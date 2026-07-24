import type { CSSProperties } from "react";

interface LocalIconProps {
  readonly source: string;
  readonly className?: string;
}

type LocalIconStyle = CSSProperties & {
  readonly "--icon-source": string;
};

export function LocalIcon({ source, className }: LocalIconProps) {
  const style: LocalIconStyle = {
    "--icon-source": `url("${source}")`,
  };
  const classes = className ? `local-icon ${className}` : "local-icon";

  return <span className={classes} style={style} aria-hidden="true" />;
}
