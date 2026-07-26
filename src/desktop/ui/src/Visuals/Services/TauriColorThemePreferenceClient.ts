import { invoke } from "@tauri-apps/api/core";
import { normalizeActionError } from "../../Actions/Services/normalizeActionError";
import type { ColorThemePreferenceClient } from "../Interfaces/ColorThemePreferenceClient";
import type { ColorTheme } from "../Models/ColorTheme";

interface ColorThemePreferenceResult {
  readonly colorTheme: ColorTheme | null;
}

export class TauriColorThemePreferenceClient implements ColorThemePreferenceClient {
  async getColorTheme(): Promise<ColorTheme | null> {
    try {
      const result = await invoke<ColorThemePreferenceResult>(
        "preference_get_color_theme",
      );
      return result.colorTheme;
    } catch (error: unknown) {
      throw normalizeActionError(error);
    }
  }

  async setColorTheme(colorTheme: ColorTheme): Promise<void> {
    try {
      await invoke("preference_set_color_theme", { colorTheme });
    } catch (error: unknown) {
      throw normalizeActionError(error);
    }
  }
}
