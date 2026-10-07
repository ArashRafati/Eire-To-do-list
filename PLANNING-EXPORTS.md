# Mind maps, WBS and planning exports

Use the yellow title dropdown to select **TO-DO**, **MIND MAP / WBS** or **GRAPH**.
The combined Mind map / WBS module shares saved diagrams and nodes. Module
switching preserves their layout; choose any of the five in View → Diagram layout:
two-sided mind map, right tree, top-down WBS, left-to-right WBS or numbered outline.
Changing layout keeps the same hierarchy and node IDs.

Placement updates automatically when siblings/children are added, moved or
deleted. Two-sided maps assign new main branches to the side with less occupied
subtree space. Existing branches keep their side, and children grow outward on
their parent branch. Spacing expands and parents remain centred over their
subtrees. Folding changes spacing without flipping branches. **Balance branches**
redistributes existing main branches by size if you want to compact the map;
Undo can restore the previous arrangement. Saved side choices survive restart
and backup/restore. Drawing and exporting do not modify the diagram.

## Editing

Create a diagram, select a cell, then (new cells accept their title inline):

| Action | Shortcut / control |
| --- | --- |
| Create a cell at the same level, immediately after the selection | **Enter** / + Sibling |
| Create a child below the selection | **Insert** / + Child |
| Edit the title directly in the cell | **F2** / double-click |
| Open full notes, parent, dates, duration and completion | **Ctrl+Enter** / right-click → Node details |
| Move a branch before/after/inside another cell | Drag to its top/bottom/centre |
| Move between visible nodes | Up / Down |
| Reorder among siblings | Ctrl+Up / Ctrl+Down / Move buttons |
| Make the previous sibling the parent | Indent |
| Move up one hierarchy level | Outdent |
| Hide/show a branch | Fold / unfold / Left |
| Undo / redo chart changes | Ctrl+Z / Ctrl+Y |
| Zoom | Slider / Ctrl+mouse wheel / Fit chart |

Shortcuts apply while the chart has focus; click a cell or the empty chart area.
Creating a cell saves `New node` immediately and opens its editor. Cancel keeps
that new cell; Undo removes it. Enter in the editor saves; multiline notes use
Enter for a new line and Ctrl+Enter to save. A top-level cell can have siblings.
Deleting a cell asks before removing its entire branch. Undo/redo are local to
the current session and diagram; deletion of a whole diagram is confirmed and
is recovered through your backup. Charts allow up to 1,000 nodes and 32 levels.
Labels and diagram names allow up to 100 characters for planning compatibility.

Diagram settings let you name it, associate it with a project, and choose a
scheduling start date. **From to-do** copies a project's tasks into a new diagram;
later edits to either copy are independent. Every accepted edit saves locally.
Backups include diagrams, project associations, node notes/dates, selected module,
selected diagram and zoom. The gear in the header controls opacity/topmost in
all modules. Your existing to-do data and profile folder remain in use.

## Scheduling conventions

Hierarchy links are parent/child relationships, not activity dependencies.
Exports contain no resource/cost assignments, baselines or predecessor links.
They provide a WBS/activity skeleton for further planning in MSP or P6.

- Branches become summary tasks / WBS. Their exported dates roll up from children.
- Leaf nodes become tasks / activities. Both dates supplied: duration is the
  inclusive number of days between them. Start only: finish is start + duration
  minus one day. Finish only: start is finish minus duration plus one day.
- With neither date, the diagram's saved scheduling start and duration are used.
  Export does not change the blank optional fields in the app.
- The included calendar works **seven days per week, eight hours per day**,
  08:00–12:00 and 13:00–17:00. This preserves entered dates, including weekends;
  it is not an assumption about your company's working week. Replace the
  calendar and schedule in your planning tool as needed.
- Dates shown in the app and general/MSP CSV use dd/MM/yyyy. XML uses the
  ISO date/time required by the file formats; P6 CSV uses yyyy-MM-dd.
- Permanent node numbers give MSP task UIDs and P6 activity IDs (`A00001` etc.).
  Reordering changes outline/WBS codes while preserving those identifiers.

## Export formats and import routes

Choose **Export CSV / XML / PDF** and a format. All nodes are exported, including
folded branches. Choose a location outside the live app data folder.

| Export | Intended use |
| --- | --- |
| Microsoft Project XML | MSPDI namespace `http://schemas.microsoft.com/project`; summary hierarchy, stable UIDs, dates, durations, completion, notes and calendar |
| Primavera P6 XML | Native PMXML `APIBusinessObjects`; project, calendar, WBS parents, leaf activities, completion and notebook notes |
| Complete hierarchy CSV | Full node list, parent/node GUIDs, outline codes, original optional dates, duration, completion and multiline notes |
| Microsoft Project CSV | Flat import material with Name, Start, Finish, Duration, % Complete and Notes; hierarchy/identifier columns for mapping |
| P6 CSV | Rows identified as WBS or Activity, with WBS/parent codes, IDs, planned dates, duration in **hours**, status and notes for spreadsheet mapping |
| PDF | Paper preview before saving; defaults to fitted A4 landscape. Select portrait/landscape, A4/A3, optional full details or tiles. Preview and PDF share the same drawing; PDF export needs no printer driver |

### Microsoft Project

Prefer **Microsoft Project XML**: in Project desktop, open the XML as a new
project and review the result. A project summary plus your visible root(s)
is included; compare the outline and dates before scheduling.

For CSV, open the file in Project and use its Import Wizard for task data,
comma delimiter, first-row headings and Australian dates. Map Name, Start,
Finish, Duration, % Complete and Notes to their corresponding task fields.
Do not use Unique ID as an update key unless you have established that mapping
in your target project. WBS/Outline Level are read-only or unavailable in some
wizard versions: retain the WBS code in a spare text field and rebuild indentation,
or use the XML export to carry hierarchy directly. CSV is import material and
is not guaranteed to recreate indentation through the wizard alone.

### Primavera P6

Prefer **Primavera P6 XML**. Select a target namespace version no newer than your
P6 installation: 18.8 (default), 23.12, 24.12 or 25.12. In P6 Professional use
File → Import → Primavera P6 XML; choose a new project in an EPS you can access,
review the import actions and confirm the hierarchy and activity IDs. Existing
EPS/security settings are controlled by P6. Use your organisation's normal
permissions. Older P6 installations may need their own supported format/version.

P6's spreadsheet import normally expects an **XLS/XLSX template exported by P6**;
it does not directly import arbitrary CSV. For the P6 CSV export:

1. Import the PMXML once to establish the WBS, or create the WBS hierarchy using
   rows marked WBS and their parent codes. CSV alone does not create the WBS.
2. Export an activities spreadsheet/template from that target project in P6.
   Keep its sheet names, technical column identifiers and header rows intact.
3. Open the CSV as UTF-8 in Excel. Filter Row Type to Activity. Map/copy activity
   IDs, names, WBS codes, planned dates and status into the matching editable
   columns of the P6 template. Original Duration is in hours; convert to the
   template's display unit (for example divide by eight for this calendar's days).
   Notebook notes and calculated date fields may need editing through P6 itself
   if that version's spreadsheet importer does not support them.
4. Save the target template as XLSX and import it using P6's spreadsheet route.
   Review the import log, rejected fields, dates and hierarchy before scheduling.

Native imports in **Microsoft Project and Primavera P6 have not been run** in
this Linux environment. Automated checks validate export contents and an
independent MPXJ 16.10.0 reader opens the XMLs and verifies hierarchy, dates,
durations, IDs, completion and notes. This does not substitute for your tool's
version-specific import checks. Start with a new/disposable target project.

The XML field conventions were checked against MPXJ's schema-derived readers
and writer implementation: https://github.com/joniles/mpxj . The app does not
bundle or call MPXJ; it is only an optional developer verification tool.

## Graph module and text formatting

**GRAPH** uses independent nodes and directed leads. It supports cycles,
many incoming/outgoing relationships, double arrowheads and manual positions.
Its **CSV** has Node/Lead records, endpoint IDs, directions, routing, positions,
notes, text-format JSON, endpoint sides, palette and node design. Its **XML** uses `urn:eire:connection-diagram:1`;
this is an Eire diagram interchange format, not MSPDI or PMXML. Its **PDF**
includes direction arrows, lead descriptions and full node/lead detail pages.
These files do not schedule dependencies or masquerade as native planning
imports. Switch to WBS/Mind map for the existing MSP/P6 export profiles above.
All-data JSON backup/restore is the supported import route for Eire itself.

The Format ribbon saves font family/size, bold, italic, underline and alignment.
PDF preserves text size/style/alignment using embedded DejaVu fonts as a
portable fallback for installed Windows families. Planning CSV/XML carry data
and hierarchy; they do not carry Eire visual styling. Formatting does not alter
node IDs, dates, durations or exported hierarchy.

Overdue uses explicitly entered finish dates and the computer's local date.
Summary rollup dates and undated-node scheduling defaults in planning exports
do not create overdue entries. Red chart/task styling and the persistent log
are app features; planning tools apply their own overdue formatting rules.

## Open workspace and graph creation (3.1)

All diagrams start centred in a pannable workspace that extends as you navigate.
Drag empty space / middle-drag / Space+drag, use wheel / Shift+wheel to pan,
or Ctrl+wheel to zoom. Fit / centre is in the View ribbon. Graph positions may
be negative; CSV/XML retain their world coordinates while PDF shifts the full
diagram into a compact page area without exporting empty workspace margins.
Panning itself does not move nodes or alter exports.

Enter and Insert in Graph both create a lead from the selected cell to the new
cell. Insert → New lead style changes new leads to incoming or both directions,
and selects curve or sharp bends. Unlinked node creates a separate cell. Existing
lead direction/shape/description remains editable by right-click/double-click.
Old numeric mode settings and Connections data remain compatible; existing WBS
layouts stay WBS layouts when opened in the merged module.
