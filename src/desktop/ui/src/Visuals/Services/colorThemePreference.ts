import { darkColorSchemeMediaQuery } from "../Constants/colorThemeConstants";
import type { ColorTheme } from "../Models/ColorTheme";

export function initializeColorTheme(): void {
  applyColorTheme(getSystemColorTheme());
}

export function getSystemColorTheme(): ColorTheme {
  return window.matchMedia(darkColorSchemeMediaQuery).matches
    ? "dark"
    : "light";
}

export function applyColorTheme(colorTheme: ColorTheme): void {
  document.documentElement.dataset.theme = colorTheme;
}
