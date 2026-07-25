import {
  colorThemeStorageKey,
  darkColorSchemeMediaQuery,
} from "../Constants/colorThemeConstants";
import type { ColorTheme } from "../Models/ColorTheme";

export function initializeColorTheme(): void {
  applyColorTheme(readColorThemePreference() ?? getSystemColorTheme());
}

export function readColorThemePreference(): ColorTheme | null {
  try {
    const value = window.localStorage.getItem(colorThemeStorageKey);
    return value === "light" || value === "dark" ? value : null;
  } catch {
    return null;
  }
}

export function writeColorThemePreference(colorTheme: ColorTheme): void {
  try {
    window.localStorage.setItem(colorThemeStorageKey, colorTheme);
  } catch {
    // The theme still applies for this session when storage is unavailable.
  }
}

export function getSystemColorTheme(): ColorTheme {
  return window.matchMedia(darkColorSchemeMediaQuery).matches
    ? "dark"
    : "light";
}

export function applyColorTheme(colorTheme: ColorTheme): void {
  document.documentElement.dataset.theme = colorTheme;
}
