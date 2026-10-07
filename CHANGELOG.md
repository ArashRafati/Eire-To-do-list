# Changes

## 3.0.0 — ribbon, direct editing and connection diagrams

- Preserve the existing dark/yellow palette; add Home/Insert/Format/View ribbon tabs.
- Add inline autosaved titles, Enter/Insert continuation, Ctrl+Enter/right-click details, and WBS-code-only headers.
- Add branch drag/drop before, after or inside a node, with cycle prevention and undo.
- Save font family/size, bold/italic/underline, alignment and format-painter changes; expand hierarchy cells/spacing for large text.
- Add a local header clock, red overdue items and a hideable persistent overdue history retaining resolved episodes.
- Add a separate Connections module with automatic initial placement, free dragging, multiple directional leads, double arrowheads, curves/sharp bends and editable labels.
- Export connection CSV/XML/PDF separately from planning formats; preserve lead directions and full notes/labels.
- Bundle regular/bold/oblique/bold-oblique PDF fonts and add routing, graph, formatting, overdue, persistence and rollback checks.

## 2.1.0 — dynamic mind-map placement

- Place new main branches according to available space on each side, using
  full subtree footprints rather than alternating branch counts.
- Preserve existing branch sides across insertion, reorder, fold and restart.
- Recalculate node positions and parent centring after hierarchy changes;
  cache subtree measurements to avoid repeated traversal during layout.
- Add an undoable Balance branches action for existing two-sided mind maps.
- Keep viewport context during reflow and bring new cells into view after
  WPF layout has finished; avoid duplicate redraws during node creation.
- Include side preferences in autosave and backup/restore; accept old diagrams.
- Add dynamic growth, balancing, side persistence and 1,000-node checks.


## 2.0.0 — mind maps, WBS and planning exports

- Added a title dropdown for To-do, Mind map and WBS modules.
- Added saved diagrams with sibling (Enter) and child (Insert) creation, node
  editing, multiline notes, dates, duration, completion and parent selection.
- Added five layouts, automatic non-overlapping subtree placement, permanent
  node identifiers, numbering, scrolling, zoom/fit, folding and reordering.
- Added indent/outdent, confirmed branch/diagram deletion and session undo/redo.
- Added optional project associations and copying project tasks to diagrams.
- Added three CSV profiles, MSPDI XML and P6 PMXML (18.8/23.12/24.12/25.12).
- Added direct PDF chart tiling and full-text detail pages with an embedded font.
- Added background export generation, atomic export replacement and import guides.
- Added a header gear for window settings in every module.
- Included diagrams and module/selection/zoom preferences in existing autosave
  and backup/restore. Old schema-1 data loads with an empty diagram list.
- Added chart hierarchy, layout, persistence, failure, scheduling and export
  checks, plus optional independent CSV/XML/PDF verification.


## 1.2.0 — app icon

- Added a custom golden-yellow tile with the black three-bar Eire mark.
- Embedded a multi-resolution Windows ICO (16–256 px) in the executable.
- Applied the icon to the main window, task/project editors, taskbar and
  window switcher, and used matching artwork in the widget header.
- Icon files are bundled; the portable app still requires only its executable.
- Existing data and preferences use the same local profile folder.

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
