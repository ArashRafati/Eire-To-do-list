# Changes

## 1.1.0 — readable dark interface

- Increased main text and task cells to 17 px, button/header text to 16 px,
  labels to 15 px, and the title to 24 px. Task rows are 46 px high.
- Applied the dark window style explicitly to every custom window, fixing
  the white background visible in the first Windows screenshots.
- Replaced faint labels and opacity-based disabled states with high-contrast
  white text and dark input fields, including task/project editors.
- Introduced a sharper black/slate interface with angular panels, yellow
  focus indicators, solid control borders and dark scrollbars.
- Corrected selected project/category labels with explicit item templates;
  internal record names no longer appear as dropdown labels.
- Kept the table accessible in compact windows by allowing the upper controls
  to scroll, and widened date columns enough for the larger type.
- Limited editor windows to available display space; their content scrolls.
- Task/project data format and data folder are unchanged. Existing data and
  saved window preferences are reused without a migration.

The updated build and automated core checks passed in Linux. Native Windows
rendering and interaction still require a Windows desktop check.
