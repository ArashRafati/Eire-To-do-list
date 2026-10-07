# SUMAPP

A portable, offline Windows desktop to-do, mind-map/WBS and graph widget built with WPF and .NET 10.
The Windows x64 release bundles its runtime in `SUMAPP.exe`; users unzip and
double-click it under a standard account. It uses no installer, services,
accounts, elevated privileges, or network requests.

## SUMAPP branding, project deletion and measured nodes — version 3.3

The supplied SAMAP wordmark was changed to **SUMAPP**, as requested, preserving
its teal ribbon symbol. The corrected transparent artwork is used in the header
and nine-resolution executable/window icon. The header has a small neutral logo
backing, a black background, thin Eire yellow top strip and top-right local clock.
Shared component resources use the requested neutral/teal palette with readable
ink text, dark-teal primary actions and restrained yellow priority badges.

**Eire branding is pending:** the inline Eire logo's source bytes are not accessible
in the workspace. The right-hand header labels the missing asset explicitly;
no recreated Eire logo is substituted. Supply its original PNG/SVG before
finalising the brand. See [BRANDING.md](BRANDING.md).

Projects now have **Delete**. Empty projects are removed immediately; for projects
with tasks, choose an active destination or create one. Deletion and reassignment
save atomically. Task IDs, creation times, dates, notes, status, formatting and
history stay intact; diagrams move to the destination or lose only their optional
project association. Save failures leave the original project and tasks intact.

Node widths adapt to title length and hierarchy level. Heights use real WPF
TextBlock measurements for the selected font, weight and wrapping; selection
border space is reserved. PDF exports measure their bundled fonts and wrap at
word boundaries. Layouts reserve actual node dimensions, including large fonts
and notes/date previews, and reflow after title confirmation or blur. New graph
nodes reserve the renderer's final size after their lead changes the graph levels.
Priority can be set in task/node details; status checkboxes remain accessible as
the first table column. Existing filters, graph leads, drag/drop and exports remain.

The executable is now **SUMAPP.exe**. The existing `%LOCALAPPDATA%\EireTodo`
profile location is deliberately retained so previous tasks/settings still load.
Close the previous version before running SUMAPP. Avoid editing updated data
with older executables, which cannot preserve the new priority flag.

[To-do design preview](previews/SUMAPP-to-do-design.png) ·
[Mind-map design preview](previews/SUMAPP-mind-map-design.png).
These are labelled vector design previews, **not native Windows screenshots**.
The Linux environment cannot run WPF. On Windows, the real app can capture both
views and check header/tab resizing and long-title sizing in isolated sample data:

```powershell
.\SUMAPP.exe --capture-previews "$env:USERPROFILE\Pictures\SUMAPP-previews"
```

## Editing, lead handles and paper preview — version 3.2

**Enter while editing a title confirms it and returns focus to the canvas. The
next distinct Enter creates a sibling (or a linked Graph node).** Holding the
key does not create a stream of nodes. Insert still creates a child/linked node.

Hierarchy drags capture the mouse and show insertion bars in the gaps between
siblings. In a top-down WBS, left/right edges mean before/after; the middle
means child. Other layouts use top/bottom edges. You can also right-click a
node and choose **Move before / after**. Moving `1.6.1` before `1.6` promotes it
to `1.6`, shifts the previous `1.6` to `1.7`, and preserves IDs and descendants.

Click a Graph lead to select it, then **Delete** removes it or **F2 / double-click /
Insert → Lead label** edits its label. The two endpoint handles can be dragged to
any node's highlighted top/right/bottom/left centre. Side attachments, labels,
direction and routing save with the diagram and export in Graph CSV/XML/PDF.
Task and node deletion is immediate; diagram edits support Undo.

The ribbon keeps all four tabs visible, uses flat vector buttons and explanatory
hover tips, and reserves sufficient room for the font/style controls. Nodes are
compact and sized by level; long titles wrap fully. **Format → Diagram style**
offers Tiered, Cards, Rounded and Minimal designs plus Eire (the original),
Ocean, Forest, Violet, Copper and Monochrome palettes. The app chrome retains
its original colours. Zoom uses an animated render transform with delayed
preference saving; it does not replace or rebuild the node views.

**PDF opens a paper preview before saving.** A4 landscape with fit-to-paper is
the default; choose portrait, A3, tiled output or the full-details appendix.
Every page can be previewed. The preview and PDF render the same vector drawing
with bundled fonts. Print opens the normal Windows printer selection dialog;
PDF export itself needs no printer or external viewer.

## Open workspace and organised ribbon — version 3.1

The title menu now has **TO-DO**, **MIND MAP / WBS** and **GRAPH**. The combined
hierarchy module retains all five layouts under **View → Diagram layout**;
switching modules no longer converts a saved layout. Existing WBS, mind-map
and Connections profiles/backups open through the new menu automatically.

The clock is in the **top right**. Each ribbon tab has a consistent command
band, named groups and compact labels; groups scroll horizontally rather than
stacking over the canvas. Double-click a tab or use its arrow to collapse or
expand it. The chart ribbon starts collapsed in a very small workspace.

Diagrams open **centred in an open canvas**, with space on every side. Drag empty
space, middle-drag, or hold Space and drag to pan; wheel pans vertically and
Shift+wheel horizontally. Ctrl+wheel zooms around the pointer. View → Fit / centre
fits and centres the diagram. The workspace expands as you pan, and Graph
nodes can cross either side of their original position, including negative
coordinates. Panning affects the camera rather than saved node positions.
PDF export includes the diagram, not the large virtual workspace margins.

In **GRAPH**, both **Enter and Insert** create a new node with an outgoing lead
from the selection, then enable its inline title. **Insert → New lead style**
lets you choose outgoing, incoming or both directions and curved/sharp-bend
routing. Existing leads remain editable by right-click/double-click. **Unlinked
node** explicitly creates an independent cell; with no selection a new cell is
also unlinked. The existing black/slate/yellow palette remains unchanged.

## Editing and graphs

The existing black/slate/yellow palette is preserved. The top ribbon has
**Home**, **Insert**, **Format** and **View** tabs with grouped commands. The
header displays the computer's current local date/time in Australian format.

- In **MIND MAP / WBS**, **Enter / Insert** creates a sibling / child with its title editable directly
  in the cell. Enter while typing confirms that title; the next Enter creates a sibling;
  Insert saves and creates a child. **F2 / double-click** edits the title;
  **Ctrl+Enter** or **right-click → Node details** opens full notes and dates.
  The cell header shows only its WBS code. Title changes autosave after a short
  pause and are flushed before changing diagram/module, exporting or closing.
- **Drag a branch** onto a cell's top edge to put it before that cell, bottom
  edge to put it after, or centre to make it a child. The highlighted border
  shows the destination. Descendants move with their branch, WBS codes update,
  and permanent IDs stay fixed. Circular moves are rejected; Undo is available.
- **Format** changes the selected task description or node title: installed
  font family, size 12–48, bold, italic, underline and left/centre/right
  alignment. Format painter copies a selection's style to the next clicked
  task/node, including across modules. New cells inherit the selection's style.
  Hierarchy spacing expands for larger fonts.
- **Show overdue log** toggles an autosaved history panel. Unfinished items with
  an explicitly entered finish date before today turn red. Today and blank
  finish dates are not overdue. The log retains resolved episodes after
  completion, deletion or deadline changes, and notices newly overdue items
  on restart and each minute while the app is open. Its visibility persists.
- **GRAPH** is a separate module. Enter or Insert creates an
  automatically positioned node connected from the selection. Choose **Insert
  → Connect nodes**, then click the target to add more leads. Any cell may have
  multiple incoming/outgoing leads, including cycles. Drag cells freely; their
  leads follow. Choose curved or sharp-bend leads and outgoing, incoming or both directions.
  Right-click/double-click a lead to describe, reverse, reshape or delete it.
  Long leads route around intervening cells; labels follow the lead direction
  and show a shortened preview with full text in the tooltip and PDF appendix.

Connection diagrams export their own CSV/XML plus PDF; WBS/mind-map diagrams
retain the Microsoft Project / P6 planning exports. Free-form relationships are
not interpreted as scheduling dependencies. PDF uses bundled DejaVu fonts for
portable output and preserves size, weight, italic, underline and alignment;
Windows font families use this export fallback. All new data is included in
JSON backup/restore. **Back up before updating and continue using version 3.3;
older executables cannot preserve fields added by this release.**

## Dynamic placement — version 2.1

Mind maps now place new main branches on the less crowded side using the space
occupied by their full subtrees. Existing branches keep their side when siblings
are inserted or reordered. Child nodes follow their parent branch; the chart
recalculates subtree spacing and centres parents as branches grow, move, fold
or are deleted. **Balance branches** can redistribute existing two-sided branches
by size. Side choices are saved with the diagram and included in backups.

The viewport retains its context during rearrangement, and new cells scroll into
view after WPF finishes layout. Cached subtree measurements keep rearrangement
practical as charts grow. Automated checks exercise successive sibling/child
creation to the 1,000-node limit and verify all five layouts remain collision-free.

## Mind maps and WBS — version 2.0

The yellow **TO-DO** title is a module dropdown: choose **MIND MAP / WBS**
for either hierarchy style. Create saved diagrams, select cells and press **Enter** for a sibling
or **Insert** for a child. Five layouts are available: two-sided mind map, right
tree, top-down WBS, left-to-right WBS and numbered outline. Edit full notes,
parent, dates, duration and completion; reorder, indent/outdent, fold, zoom,
fit and undo/redo. Copy a project's existing tasks into a diagram if useful.

Export CSV, Microsoft Project XML (MSPDI), Primavera P6 XML (PMXML, target versions
18.8 / 23.12 / 24.12 / 25.12), and directly generated PDF chart/detail pages.
[PLANNING-EXPORTS.md](PLANNING-EXPORTS.md) explains scheduling defaults and exact
import/mapping routes. P6 CSV is spreadsheet mapping material: its native
spreadsheet importer normally requires a P6-exported XLSX template. XML is the
preferred route for carrying hierarchy. Native MSP/P6 import has not been run;
exported XML is independently parsed and checked with MPXJ.

Diagrams and view preferences autosave in the existing user data file and are
included in full-data backup/restore. Back up before updating, and use the new
executable for subsequent changes; older app versions do not understand diagrams.

## App icon update 1.2

Historically, version 1.2 embedded a black-and-yellow Eire icon, now superseded by SUMAPP. That icon was embedded in the Windows executable
and applied to all app windows. The icon appears in Explorer, desktop shortcuts,
the taskbar and the window switcher. The header uses the same artwork. The
Windows ICO contains 16, 20, 24, 32, 40, 48, 64, 128 and 256 px sizes.

The icon is embedded in the self-contained app; no external image file or
installation step is needed when running the portable executable.

## Interface update 1.1

Historically, version 1.1 used 17 px body/table text, bright white labels, dark black/slate
panels and yellow focus indicators. Explicit window styling fixes the white
background shown in the first Windows screenshots; explicit dropdown templates
show project and category names. See [CHANGELOG.md](CHANGELOG.md).

To update, close the running app, download and extract the new ZIP, then replace
or run the new `SUMAPP.exe`. The app reads the same local data folder and schema;
there is no data migration or separate runtime installation.

## Run and data

Unzip `artifacts/SUMAPP-Windows-x64.zip` into a writable folder and double-click
`SUMAPP.exe`. Supports Windows 10 22H2 / Windows 11 x64. Application data and
settings live in `%LOCALAPPDATA%\EireTodo\data.json`. Moving the executable does
not move or reset data. The app is unsigned; normal Windows/company policy may
require IT approval. The app does not bypass security controls.

[QUICKSTART.txt](QUICKSTART.txt) contains the short user guide and recovery steps.

## Features

- Draggable header, edge resizing, normal minimise/maximise/close, adjustable
  opacity (65–100%, default 97%), always on top, persisted window geometry and
  column widths, and per-monitor DPI awareness.
- Create, rename, archive/unarchive projects; All projects includes archived
  projects and their tasks. Archived projects cannot receive new tasks.
- Eight-column table, sorting by every header, horizontal/vertical scrolling,
  adjustable widths, short notes previews, and a full multiline notes editor.
- Add, edit, complete/reopen, delete tasks immediately; optional hiding of completed
  tasks. Description and project required; optional start/finish dates,
  reusable categories, and notes. Created date/time is recorded when saved and
  preserved when edited. Australian dates and 24-hour local times throughout.
- Combined project, task text, notes text/blank, category/blank, status,
  start/finish range/blank, and created date/time range filters. Inclusive bounds,
  optional open bounds, validation and a visible Clear filters button.
- Atomic autosaves with durable temporary-file flush, previous-save recovery,
  single-instance profile lock, validated full-data backup/restore, and a
  pre-restore safety copy. Save failures stay visible and reject the failed edit.

The header and application icon use the corrected SUMAPP artwork. Logo and
multi-resolution ICO assets are included under `src/EireTodo.Windows/Assets`.
The company logo remains pending its accessible original source asset.

## Build on Windows (no administrator privileges)

From the source directory in Windows PowerShell 5.1 or later:

```powershell
.\scripts\build-windows.ps1
```

The script uses SDK **10.0.401** if available, otherwise downloads the official
portable Windows x64 SDK archive to `%LOCALAPPDATA%\EireTodoBuildTools`, verifies
its pinned SHA-512 hash, and extracts it there. It does not use an installer,
change execution policy, or request elevation. Building needs HTTPS access to
`builds.dotnet.microsoft.com` and NuGet endpoints; running the resulting app is
fully offline. If policy prevents scripts or executables from running, follow
your organisation's approval process.

If you already have the pinned SDK, the exact underlying build commands are:

```powershell
dotnet restore tests/EireTodo.Checks/EireTodo.Checks.csproj --locked-mode
dotnet run --project tests/EireTodo.Checks/EireTodo.Checks.csproj -c Release --no-restore
dotnet restore src/EireTodo.Windows/EireTodo.Windows.csproj --locked-mode
dotnet publish src/EireTodo.Windows/EireTodo.Windows.csproj -c Release --no-restore -o artifacts/windows-x64
```

The project pins `win-x64`, `SelfContained=true`, `PublishSingleFile=true`,
`IncludeNativeLibrariesForSelfExtract=true` and disables trimming. The published
app needs only `SUMAPP.exe`. Its embedded manifest requests `asInvoker`, with
`uiAccess=false` and `PerMonitorV2` DPI awareness. The runtime extracts native
libraries to the current user's temporary cache; no machine-wide files are
installed. Developers need an SDK; people running the portable app do not.

## Build/check in this Linux cloud environment

```bash
bash scripts/setup-cloud.sh
```

This uses a checksum-verified portable Linux SDK in `/workspace/.tools/dotnet`,
restores locked dependencies, runs functional checks, generates export fixtures, and cross-publishes the
Windows executable. Existing checkouts are used; each cloud task is isolated,
so no additional Git worktree is needed. The Linux setup script's paths assume
the onboarding checkout `/workspace/Eire-To-do-list`.

The Windows SDK bootstrap/PowerShell script is supplied for Windows, but was
not executed here. The equivalent Linux restore/test/publish commands were
executed successfully. Linux cannot run WPF, so native window controls,
interactive desktop operations, mixed-DPI displays, security policy behaviour
and real Windows account launch remain unverified.

## Source and verification

- `src/EireTodo.Core`: models, validation, combined filtering, atomic JSON
  storage, chart hierarchies/layouts/planning exports, backup/restore and transactional operations.
- `src/EireTodo.Windows`: WPF UI, Windows manifest, window settings and profile
  data location.
- `tests/EireTodo.Checks`: executable functional checks without an external test framework,
  including disk failures and corruption recovery. A failing check exits nonzero.
- [VERIFICATION.md](VERIFICATION.md): actual build/check evidence and limits.
- [PLANNING-EXPORTS.md](PLANNING-EXPORTS.md): chart guide and MSP/P6 import routes.
- [WINDOWS-VERIFICATION.md](WINDOWS-VERIFICATION.md): exact remaining desktop
  acceptance steps to run under a standard Windows account.

Data is plain JSON with schema version 1. Internal JSON dates use ISO serialization;
only display/input uses Australian formatting. Created timestamps retain their
offset and display in the computer's current local timezone. Exported backups
contain notes and other personal data; store them where you intend.

For an optional independent planning-export check (development tools only):

```bash
python3 -m pip install --target /tmp/eire-export-tools mpxj==16.10.0 JPype1==1.7.1
PYTHONPATH=/tmp/eire-export-tools python3 scripts/verify-chart-exports.py
```

This requires Java and Poppler's `pdftotext` locally; neither is required by the
Windows app. PDF generation uses bundled PDFsharp and a DejaVu font, with no
printer/runtime installation or accounts. See [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt).
