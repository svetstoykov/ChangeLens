import { useEffect, useLayoutEffect, useState } from "react";
import { darkColorSchemeMediaQuery } from "../Constants/colorThemeConstants";
import type { ColorTheme } from "../Models/ColorTheme";
import {
  applyColorTheme,
  getSystemColorTheme,
  readColorThemePreference,
  writeColorThemePreference,
} from "../Services/colorThemePreference";

interface ColorThemeController {
  readonly colorTheme: ColorTheme;
  readonly toggleColorTheme: () => void;
}

export function useColorTheme(): ColorThemeController {
  const [preference, setPreference] = useState<ColorTheme | null>(
    readColorThemePreference,
  );
  const [systemTheme, setSystemTheme] =
    useState<ColorTheme>(getSystemColorTheme);
  const colorTheme = preference ?? systemTheme;

  useLayoutEffect(() => {
    applyColorTheme(colorTheme);
  }, [colorTheme]);

  useEffect(() => {
    if (preference !== null) {
      return;
    }

    const systemPreference = window.matchMedia(darkColorSchemeMediaQuery);
    const handleChange = (event: MediaQueryListEvent) => {
      setSystemTheme(event.matches ? "dark" : "light");
    };
    systemPreference.addEventListener("change", handleChange);
    return () => systemPreference.removeEventListener("change", handleChange);
  }, [preference]);

  function toggleColorTheme() {
    const nextTheme = colorTheme === "light" ? "dark" : "light";
    writeColorThemePreference(nextTheme);
    setPreference(nextTheme);
  }

  return { colorTheme, toggleColorTheme };
}
