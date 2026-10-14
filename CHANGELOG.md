# Changes

## 3.7.1 — silent startup repair and versioned portable packaging

- Remove duplicate ListBox/ListBoxItem resource registrations that could fail before the startup handler.
- Add duplicate-resource regression validation and early resource-initialisation error reporting to the user-profile folder.
- Package the complete self-contained folder to avoid single-file extraction at launch; keep installed-runtime/admin requirements absent.
- Include the version in executable and Windows/source ZIP filenames while preserving SUMAPP assembly/resource identity.
- Preserve existing data and functionality. Native Windows execution remains unavailable in this Linux environment.

## 3.7.0 — graph organisation, connections, branding and colour packages

- Group File actions at the top left, streamline Home/context menus, and correct compact Format controls and wrapping gallery labels.
- Enter creates a graph peer sharing sources/targets; Insert and explicit context commands create directed or unlinked nodes. Title-confirmation behaviour remains intact.
- Add bulk incoming/outgoing/bidirectional connections, labels, routing and optional parallel leads.
- Add Organise graph using actual node sizes, directed components, cycles and disconnected groups; preserve IDs/topology and support Undo.
- Give graph nodes one ordinary default colour and selected-node palette/hex overrides.
- Add thirteen palettes: red/orange/grey/charcoal combinations and six coordinated multi-colour packages. Total: 34 palettes, 15 designs and 10 hierarchy layouts. Multi-colour application is deliberate; new graph nodes retain the default colour.
- Add per-graph organisation PNG, independent viewport/export sizes, overlap fading and reserved PDF/print headers. Preserve original application artwork and existing profile data.
- Add thirteen automated groups (80 total), publish the Windows x64 app, and extend native Windows capture/checklists without claiming native UI execution on Linux.

## 3.6.0 — node clipboard, full notes and expanded galleries

- Add Ctrl-click/Ctrl+A multi-selection and Ctrl+C/Ctrl+V branch copying, with deduplicated descendants, fresh IDs, child-level paste and saved Undo. Graph copies preserve internal leads.
- Add persistent full-note rendering with measured wrapping in the canvas and PDF; add expand/collapse controls to hierarchy nodes.
- Add a styled Save / Save As / Cancel / Close dialog without changing live profile autosave.
- Add five layouts, five colour palettes and five node styles; keep legacy numeric values stable and preview every new option.
- Add six functional check groups for clipboard hierarchy/graph behavior, invalid-paste atomicity, persistence, full-note fitting and all layout geometry.

## 3.5.1 — authoritative logo and bundled icon startup repair

- Remove the relative BitmapImage resource address that could resolve to C:\SUMAPP;component\Assets\AppIcon.ico and prevent launch.
- Load the default icon, SUMAPP logo and Eire logo from explicit application pack resource streams; decode and freeze them before disposing streams.
- Keep the original desktop-PNG chooser and dynamic window icons, without changing profile data or settings.
- Add a compiled-resource packaging guard and extend Windows capture checks to decode default branding from an empty working folder before applying a chosen PNG.

- Embed the supplied GitHub teal-folded-hexagon PNG unchanged in both SUMAPP PNG resources; derive all nine executable ICO frames from it. Include the original asset in the source package.

## 3.5.0 — compact shared ribbon, free hierarchy placement and PC copies

- Move module selection into Home, caption controls to the far right, and the 1.2× clock to the bottom right.
- Share one compact ribbon across modules, use flat left-aligned commands, and retain per-tab command icons when collapsed.
- Add persisted Free move for hierarchy diagrams, collision spacing, frame-coalesced visual updates, Undo/cancel and position reset.
- Save complete .sumapp copies to a chosen PC folder while retaining profile autosave; restore copies with a safety backup.
- Add six functional check groups, including 1,000-node movement, signed-position PDF fitting and full-file persistence.
- Add a desktop PNG picker that stores original image bytes with data and updates header/window/taskbar branding; include a persistence/rejection check.
- Prepare exact-PNG icon packaging; latest symbol-only logo remains pending because the supplied share URL is inaccessible.

## 3.4.0 — visual diagram galleries and Eire header

- Add SmartArt-style ribbon thumbnails and expandable, keyboard-accessible galleries for ten node designs, sixteen colour combinations and five layouts.
- Add ten new diagram colour packs without changing application chrome or existing saved enum values.
- Render flat, underlined, outline, side-accent, mixed-level and capsule designs consistently on the canvas, in thumbnails and in PDF exports.
- Keep selection outlines constant to preserve measured wrapping; cap capsule corners so long titles and codes remain readable.
- Display a compact Eire emblem prepared from the attached artwork on the right header. Keep SUMAPP as the app icon and left brand.
- Add persistence/compatibility, contrast, decoration/PDF and layout checks; extend the native Windows capture command to include Format and View galleries.

## 3.3.0 — SUMAPP branding, readable surfaces, project deletion and measured nodes

- Change the supplied wordmark to SUMAPP; use it in the header and nine-resolution Windows icon. Rename the executable to SUMAPP.exe while retaining the existing data folder.
- Centralise the exact neutral/teal/yellow palette, component typography/spacing, selected/focus states and distinct red error styling. Apply the same palette to mind maps, graphs and PDFs.
- Add yellow priority badges/nodes with black text, secondary task metadata and a leading status checkbox/badge column.
- Delete projects atomically, moving their tasks to a chosen/new active project and preserving diagrams/history. Reject invalid destinations and failed saves without changing visible data.
- Measure actual WPF text with the chosen font/weight/wrapping, reserve selection borders and metadata, and reflow titles on confirmation/blur. Measure PDF font glyphs and wrap whole words.
- Recheck new graph node spacing after lead creation and final renderer measurement.
- Add isolated Windows screenshot capture and native header/tab/font-sizing checks; native execution remains unrun in Linux.
- Supply labelled design previews. Final Eire header branding awaits its accessible original asset; no replacement logo is invented.

## 3.2.0 — precise editing, graph ports, flat ribbon and paper preview

- Enter confirms an active title; the next distinct Enter creates a node. Ignore key-repeat creation.
- Capture hierarchy drags, show sibling insertion bars, interpret top-down WBS edges horizontally, and add exact Move before/after selection.
- Select/delete graph leads, provide a focused label editor, and drag either endpoint onto any side centre. Persist/export endpoint sides; keep routes stable across canvas origin shifts.
- Remove task/node deletion confirmations while retaining diagram Undo and restore safeguards.
- Keep View and the other ribbon tabs visible; remove clipping, add vector motifs and hover descriptions to flat buttons.
- Compact nodes by level, show full wrapped titles, and save four designs/six colour palettes without changing the original app chrome.
- Animate zoom through a render transform instead of remeasuring/redrawing node content; debounce and visibly validate saved zoom preferences.
- Preview every PDF page before saving from a shared vector drawing. Default to fitted A4 landscape; support portrait, A3, optional details, tiles and normal Windows printing.
- Bundle preview fonts and add input, exact WBS move, port geometry, routing stability, style persistence and paper checks.

## 3.1.0 — open canvas, merged diagrams and Graph

- Move the local date/time clock to the top right.
- Merge Mind map and WBS into one module without changing saved layouts; rename Connections to Graph and preserve old mode values/backups.
- Centre diagrams in an expanding workspace with blank-space/middle/Space dragging, wheel panning and pointer-anchored zoom.
- Permit signed Graph positions and keep camera movement separate from saved node coordinates; compact PDF exports exclude workspace margins.
- Organise ribbon groups into a consistent-height band with horizontal scrolling, compact labels and collapse/expand controls.
- Make Enter and Insert both create a linked Graph node from the selection; provide outgoing/incoming/both direction choices and explicit unlinked creation.
- Add mode compatibility, centred workspace, linked creation, signed position/persistence and compact export verification.

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
