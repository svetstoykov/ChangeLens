---
name: ChangeLens Light
colors:
  surface: '#f7f9fb'
  surface-dim: '#d9e0e6'
  surface-bright: '#ffffff'
  surface-container-lowest: '#ffffff'
  surface-container-low: '#f3f6f8'
  surface-container: '#edf1f4'
  surface-container-high: '#e5eaee'
  surface-container-highest: '#dbe2e7'
  on-surface: '#182026'
  on-surface-variant: '#4e5964'
  inverse-surface: '#293138'
  inverse-on-surface: '#f4f7f9'
  outline: '#697580'
  outline-variant: '#c7d0d8'
  surface-tint: '#2f63d7'
  primary: '#2f63d7'
  on-primary: '#ffffff'
  primary-container: '#dce7ff'
  on-primary-container: '#173e88'
  inverse-primary: '#adc6ff'
  secondary: '#526170'
  on-secondary: '#ffffff'
  secondary-container: '#e0e7ef'
  on-secondary-container: '#33414f'
  tertiary: '#5f6d79'
  on-tertiary: '#ffffff'
  tertiary-container: '#e1e8ee'
  on-tertiary-container: '#35434e'
  error: '#b42318'
  on-error: '#ffffff'
  error-container: '#ffdad6'
  on-error-container: '#7f120c'
  primary-fixed: '#dce7ff'
  primary-fixed-dim: '#b5ccff'
  on-primary-fixed: '#001b3f'
  on-primary-fixed-variant: '#17468f'
  secondary-fixed: '#e0e7ef'
  secondary-fixed-dim: '#c3ccd5'
  on-secondary-fixed: '#17212b'
  on-secondary-fixed-variant: '#3d4a57'
  tertiary-fixed: '#e1e8ee'
  tertiary-fixed-dim: '#c5cdd4'
  on-tertiary-fixed: '#172129'
  on-tertiary-fixed-variant: '#3e4a54'
  background: '#f7f9fb'
  on-background: '#182026'
  surface-variant: '#dfe5ea'
  verified: '#087f70'
  on-verified: '#ffffff'
  verified-container: '#d7f3ec'
  on-verified-container: '#14564d'
  inference: '#6d4cc5'
  on-inference: '#ffffff'
  inference-container: '#eee7ff'
  on-inference-container: '#3d267e'
  warning: '#9a5b00'
  on-warning: '#ffffff'
  warning-container: '#fff1cf'
  on-warning-container: '#5b3500'
  unknown: '#64717d'
  unknown-container: '#e5eaef'
  on-unknown-container: '#35404a'
  canvas: '#F7F9FB'
  surface-primary: '#F3F6F8'
  surface-elevated: '#FFFFFF'
  border-structural: '#C7D0D8'
typography:
  display-lg:
    fontFamily: IBM Plex Sans
    fontSize: 32px
    fontWeight: '600'
    lineHeight: 40px
    letterSpacing: -0.02em
  headline-md:
    fontFamily: IBM Plex Sans
    fontSize: 20px
    fontWeight: '600'
    lineHeight: 28px
  body-base:
    fontFamily: IBM Plex Sans
    fontSize: 14px
    fontWeight: '400'
    lineHeight: 20px
  body-sm:
    fontFamily: IBM Plex Sans
    fontSize: 12px
    fontWeight: '400'
    lineHeight: 18px
  code-base:
    fontFamily: IBM Plex Mono
    fontSize: 13px
    fontWeight: '400'
    lineHeight: 20px
  code-sm:
    fontFamily: IBM Plex Mono
    fontSize: 11px
    fontWeight: '400'
    lineHeight: 16px
  label-caps:
    fontFamily: IBM Plex Mono
    fontSize: 10px
    fontWeight: '600'
    lineHeight: 12px
    letterSpacing: 0.05em
rounded:
  sm: 0.25rem
  DEFAULT: 0.5rem
  md: 0.75rem
  lg: 1rem
  xl: 1.5rem
  full: 9999px
spacing:
  base: 8px
  xs: 4px
  sm: 8px
  md: 16px
  lg: 24px
  xl: 32px
  gutter: 16px
  margin: 24px
---

## Brand & Style
This light design system is engineered for **ChangeLens**, a change-intelligence application for developers. The personality remains clinical, focused and intellectually calm.
The interface uses cool, paper-like surfaces instead of pure white. This preserves the precision of the dark theme while reducing glare and maintaining enough tonal separation for long review sessions.
The aesthetic follows a **Modern Minimalist** direction with a restrained code-native influence. It should feel like a precise engineering instrument rather than a conventional web dashboard.
## Colors
The palette is anchored in a cool off-white application canvas (`#F7F9FB`). White is reserved for the most elevated working surfaces, including cards, inspectors, code panels and modal dialogs.
- **Application canvas:** `#F7F9FB`
- **Primary surfaces:** `#F3F6F8`
- **Elevated surfaces:** `#FFFFFF`
- **Structural borders:** `#C7D0D8`
- **Primary text:** `#182026`
- **Secondary text:** `#4E5964`
- **Primary accent:** `#2F63D7`
The light theme uses a darker version of the ChangeLens blue so that actions and selected states remain accessible against pale surfaces.
### Semantic logic
Semantic colours are reserved for provenance, uncertainty and findings:
- **Verified Fact — Teal:** Repository-backed or deterministically verified information.
- **AI Inference — Violet:** A synthesized conclusion that depends on reasoning.
- **Warning — Amber:** A meaningful concern requiring review.
- **Critical — Red:** A confirmed or strongly evidenced high-impact finding.
- **Unknown — Slate:** Evidence that was unavailable or could not be verified.
Large surfaces should use the corresponding pale container colour. Strong semantic colours should appear primarily in text, icons and narrow accent borders.
Severity colours must not be used decoratively for ordinary change areas.
## Typography
The system uses the open-source IBM Plex family throughout.
- **IBM Plex Sans** is used for navigation, explanations, headings, controls and behavioural narratives.
- **IBM Plex Mono** is used for code, file paths, symbols, branches, commits, evidence references and compact technical metadata.
IBM Plex Sans provides the editorial clarity needed for behavioural explanations, while IBM Plex Mono gives repository evidence a distinct technical voice.
### Usage rules
- Use `body-base` for explanations and behavioural stories.
- Use `code-base` for code and prominent repository-sourced values.
- Use `code-sm` for paths, symbols, evidence citations and metadata.
- Use `label-caps` sparingly for short structural labels.
- Do not render entire explanations or navigation sections in monospace.
- Maintain strong contrast for secondary text; avoid excessively pale gray labels.
## Layout & Spacing
The layout follows a strict **8px spacing hierarchy**.
- **Grid model:** Fluid navigation rail with flexible workspace and optional inspector.
- **Density:** Use 8px internally for compact controls and 16px between related components.
- **Workspace margins:** Use 24px around overview content and 16px in denser code workspaces.
- **Connectors:** Behavioural-flow connectors use `#C7D0D8`.
- **Selection:** Selected areas use the primary blue border with a pale blue background.
- **Responsive behaviour:** Flow maps must adapt to the available width and must never depend on fixed absolute coordinates.
Whitespace should separate ideas without making the application feel like a low-density consumer product.
## Elevation & Depth
Depth is communicated mainly through tonal layering and fine outlines.
- **Level 0 — Canvas:** `#F7F9FB`
- **Level 1 — Navigation and grouped surfaces:** `#F3F6F8`
- **Level 2 — Cards and working panels:** `#FFFFFF`
- **Level 3 — Selected or emphasized surfaces:** `#EDF3FF`
Primary panels use a 1px `#C7D0D8` border. Shadows should be avoided on ordinary cards. Floating menus, dialogs and temporary overlays may use a subtle cool-gray shadow.
## Shapes
The shape system remains consistent with the dark theme.
- **Cards and nodes:** 8px radius.
- **Large panels and dialogs:** 12px radius.
- **Buttons and inputs:** 8px radius.
- **Compact metadata chips:** 4px radius.
- **Snapshot and read-only-state pills:** Fully rounded.
Rounded shapes should remain restrained. Change-area cards should feel structural rather than playful.
## Components
### Change-Area Nodes
Change-area nodes use a white surface, a `#C7D0D8` border and a 4px semantic left accent.
A normal behavioural area uses the primary blue accent. Supporting areas use slate. Areas containing warnings or unknowns may use amber, but only when the finding applies directly to that area.
Selected nodes use:
- Border: `#2F63D7`
- Background: `#EDF3FF`
- Text: `#182026`
Each node should show a short behavioural explanation, relevant file count, test coverage and contextual finding summary.
### Badges and Chips
- **Verified:** Teal text and border on `#D7F3EC`.
- **Inferred:** Violet text and border on `#EEE7FF`.
- **Unknown:** Slate text on `#E5EAEF`.
- **Warning:** Amber text and border on `#FFF1CF`.
- **Evidence citations:** IBM Plex Mono on a white or pale-gray background with a `#C7D0D8` border.
- **Snapshot pills:** `#E5EAEF` with dark text and a subtle outline.
Badges should communicate information that cannot be understood from context. Routine verified explanations should remain visually calm.
### Buttons
- **Primary:** Solid `#2F63D7` with white text.
- **Primary hover:** `#2454BC`.
- **Secondary:** White surface with a `#AEB9C3` border.
- **Ghost:** Transparent until hover, using `#E9EEF2` as the hover surface.
- **Destructive:** White or pale-red surface with `#B42318` text and border.
Only one visually dominant primary action should appear within a screen region.
### Inputs
Inputs use a white background with a `#AEB9C3` border.
The focus state uses a 2px outer ring derived from `#2F63D7`. Placeholder text uses `#697580` and must remain readable.
Repository paths, branches and revision values use IBM Plex Mono. Change-context prose uses IBM Plex Sans.
### Code and Diff Viewer
The light code viewer uses:
- Background: `#FCFDFE`
- Gutter: `#F1F4F7`
- Divider: `#C7D0D8`
- Current-line highlight: `#EDF3FF`
- Added-line background: `#E1F5EA`
- Removed-line background: `#FDE7E4`
- Added text or marker: `#167647`
- Removed text or marker: `#B42318`
Code remains set in IBM Plex Mono. Syntax highlighting should use muted, accessible colours and avoid excessive saturation.
### Stale-Analysis Banners
Stale analysis uses a pale amber background (`#FFF4D6`) with a `#B26A00` leading border.
The banner should state that repository evidence may no longer match and provide one clear **Run analysis again** action. It should feel cautionary rather than alarming.
### Progress Stages
Analysis progress uses calm vertical stages on the application canvas.
Completed stages use teal confirmation. The active stage uses the primary blue with subtle motion. Future stages remain slate and visually muted.
A secondary **Discovered so far** region uses white fact cards with restrained icons. It must not display terminal logs, fake percentages, unreliable time estimates or vague “AI activity” metrics.