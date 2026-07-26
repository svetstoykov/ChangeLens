import type { ColorTheme } from "../Models/ColorTheme";

export interface ColorThemePreferenceClient {
  getColorTheme(): Promise<ColorTheme | null>;
  setColorTheme(colorTheme: ColorTheme): Promise<void>;
}
