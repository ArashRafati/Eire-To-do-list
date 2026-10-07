# Windows desktop acceptance checks

These checks are **not executed** in the Linux cloud environment. Use a standard,
non-administrator Windows 10 22H2 / Windows 11 x64 account. Do not elevate, disable
security controls, change execution policy, or bypass an organisation's rules.
If the unsigned app is blocked, seek approval through the normal IT process.
Back up existing app data first if you have used it before.

0. **3.3 branding and readability.** Check SUMAPP in the header and executable,
   editor title bars, taskbar, Alt+Tab and a normal shortcut. The icon has nine
   sizes. Header SUMAPP uses its original proportions on a neutral backing; Eire
   is on the right using the supplied compact emblem beside the Eire label.
   Verify black/yellow header, neutral surfaces, ink text, teal focus/selection,
   dark-teal/white primary buttons and black text on yellow priority badges.
   Check hover, keyboard focus, selected/disabled controls and red overdue/error
   states at 100%, 150% and 200% scaling. Do not recolour or stretch either logo.
   Existing pinned shortcuts may require normal unpin/re-pin to refresh cached icons.
0. **3.3 measured node sizing.** Try short titles, the DN100 long title, wide W/M
   glyphs, multiline notes and date previews, each in Segoe UI, Arial and Consolas,
   normal/bold/italic at 12, 17, 32 and 48 px. Confirm full titles remain visible
   after Enter, clicking away, format changes, selection-border changes, saving,
   reopening and switching layout. Check roots/branches/leaves differ in size
   and there are no hierarchy overlaps. New incoming/outgoing/double/unlinked
   graph nodes must reserve their displayed sizes without moving existing nodes.
0. **3.3 project deletion.** Delete an empty project. Delete a populated project
   by moving tasks to another active project, then by creating a new destination.
   Confirm IDs, created dates, notes, completion/priority and diagrams persist.
   Cancel leaves data intact. Blank/duplicate destinations and save failures
   must not remove projects or reassign tasks partially.
0. **Native sample screenshots.** From the extracted app folder in PowerShell:
   `.\SUMAPP.exe --capture-previews "$env:USERPROFILE\Pictures\SUMAPP-previews"`.
   Inspect all four PNGs and `visual-checks.txt`. The command uses isolated sample data
   and checks header/View visibility at 520×400, 940×620 and 1440×900, and long
   title sizing across three fonts at 12–48 px. Actual user data is not touched.
1. **Portable launch and offline use.** Unzip the release in Documents. On a PC
   without a separately installed .NET runtime, double-click `SUMAPP.exe`.
   Confirm no runtime installation or elevation is requested. Disconnect the
   network and complete the checks below. In Task Manager's Details view enable
   the Elevated column and confirm the app reads **No**.
2. **Projects.** Create `Alpha` and `Beta`; rename Alpha to `Alpha renamed`.
   Create a task in each. Archive Alpha; confirm its existing task remains in
   All projects and its project view, but it is unavailable for new assignment.
   Edit that archived task's notes; then unarchive Alpha and assign a new task.
   Duplicate and blank project names must be rejected without changing data.
3. **Editing.** Add a task with description, start `07/10/2026`, finish
   `09/10/2026`, category `Work` and multiline notes. Reuse `Work` for a second
   task. Record the first task's created time, edit every editable field, and
   confirm created time stays unchanged. Complete/reopen using its checkbox.
   Hide completed; clear filters to show them again. Delete the disposable second task and confirm there is no confirmation prompt.
4. **Date validation.** Blank dates must save. `31/02/2026`, `2026-10-07`, and a
   finish earlier than start must show errors and keep the editor open. Equal
   start and finish dates must save. Created times must use `dd/MM/yyyy HH:mm`
   and remain read-only; notes must accept line breaks.
5. **Every filter combined.** Select Alpha, search for part of the task name,
   set category Work, notes text, status To do, start and finish ranges, and a
   created range around the recorded time. Confirm only matching tasks appear.
   Change any one condition to exclude the task. Test blank start/finish,
   category and notes using a separate task with empty optional fields. Test
   open-ended date bounds and an inclusive created end minute. Reversed and
   malformed filter bounds must display an error and keep the last valid
   results. Clear filters resets every control and Hide completed.
6. **Table.** Click every column header twice and verify ascending/descending
   sorting. Resize columns and scroll both ways. Full notes must be editable
   with Edit / notes or double-click, while the table stays a short preview.
7. **Window.** Drag the header; resize all edges/corners. Toggle Always on top,
   switch to another application and confirm the effect. Adjust opacity and
   check text stays readable at the default. Minimise/restore from taskbar,
   maximise/restore, and close with both the header button and Alt+F4. Reopen;
   check position, size, opacity, topmost and column widths persist. Move between
   monitors at 100%, 150% and 200% scaling. If a monitor is disconnected, reopen
   and verify the window's header is reachable on the remaining display.
8. **Persistence and backup.** Close/reopen and verify projects, task changes,
   statuses, dates, category reuse and full notes. Export a backup; make an
   additional task/project change; restore and confirm all data/settings match
   the backup. Verify `before-restore-*.json` exists in the data folder. A
   malformed or wrong-schema JSON restore must be rejected without replacing
   current data. Launch a second instance and verify it refuses concurrent use.
9. **Saving errors.** Export a backup first. In a disposable Windows user
   profile, test with an ordinary permission/disk restriction on the app's own
   data folder. Do not change company controls or require elevation. A failed
   edit must stay open with an error, a failed status change must revert, and
   failed window settings must show the retry banner. Restore the writable
   condition and retry. Confirm previously saved data remains intact.

Report Windows version, display scaling, standard-account status, outcomes,
and any security policy block. Automated core checks already cover editing,
project operations, combined filters, dates, disk persistence, backup/restore,
settings serialization, corruption recovery and failed-save rollback; they do
not establish native UI or Windows launch behaviour.

10. **2.0 module switcher and chart editing.** Open the TO-DO dropdown, select
    MIND MAP / WBS and create a diagram. Click its root, press Insert and name a child;
    press Enter for a sibling. Verify roots can also have siblings. Use Ctrl+Enter or
    right-click > Node details to edit full notes; Enter inside notes must add a line and
    Ctrl+Enter must save. Test date validation, parent moves, completion,
    reorder, indent/outdent, branch folding, Undo/Redo and immediate deletion.
    Confirm a folded parent opens when adding/moving a child into it. Try all
    five layouts with uneven-depth branches and multiple roots. Zoom, fit,
    scroll both ways and choose a WBS layout then switch back to To-do. In a compact window,
    scroll the ribbon horizontally / expand its compact band and ensure all controls stay reachable. The title
    dropdown/gear must work without dragging the window accidentally.
11. **Chart persistence/recovery.** Associate a diagram with a project, set its
    scheduling start, layout and zoom, then close/reopen. Check module, selected
    diagram, node IDs/content and window settings. Copy a project with From
    to-do; verify later task/chart edits are independent. Back up, modify/remove
    a diagram, restore and check it returns. Repeat a failed-save restriction
    with a chart edit: the editor must show the failure and stay open; the saved
    hierarchy and previous data must remain intact. Retry after restoring access.
12. **Exports and native planning imports.** Follow PLANNING-EXPORTS.md. Export
    folded branches as each CSV/XML/PDF type, preserving all nodes and notes.
    Open the PDF and inspect every chart tile and detail page. Check canceled
    export dialogs leave data unchanged and unwritable targets show errors.
    In a new Microsoft Project project open MSPDI XML; compare summary levels,
    task UIDs, dates, durations, completion and notes. In a disposable P6 project
    import native P6 XML with a namespace no newer than that installation;
    verify project/WBS/activity parents, activity IDs, calendar, status and
    notebook notes. Record the tool versions and any import log warnings.
    For CSV, test the Project task mapping and the P6-exported XLSX template
    workflow described in the guide; do not treat P6 CSV as a direct import.

13. **2.1 dynamic placement.** In a two-sided map, add many children to a main
    branch, then add several siblings. New main branches should use the less
    crowded side; existing branches must not flip sides when inserting a sibling
    in the middle. Grow branches on both sides and check children extend outward,
    parents stay centred, and nodes/notes previews do not overlap. Reorder, move
    a branch to a different parent, fold/unfold and delete disposable branches.
    Check geometry adjusts after each operation. Balance branches should improve
    an uneven map; Undo restores prior sides. Reopen and restore a backup to check
    sides persist. At a scrolled/zoomed location, add a child and verify its cell
    comes into view after rearrangement. Interactive viewport behaviour remains
    untested in Linux even though geometry/persistence checks pass.

14. **3.0 ribbon and direct titles.** Confirm Home/Insert/Format/View groups are
    reachable at compact size and 100%/150%/200% scaling, with unchanged colours.
    Enter/Insert must create a title field inside a cell, without a details
    dialog. Type a title and wait: it must save; Enter confirms the title, the next Enter creates a sibling,
    Insert with a child. F2/double-click edits the title; Ctrl+Enter/right-click
    opens details. Only the WBS code appears in the cell header. Close, switch
    modules/diagrams and export immediately after typing to check pending text
    is saved. Restrict storage in a disposable profile and verify failed title
    saves remain visibly pending; retry successfully after restoring access.
15. **Branch dragging.** Drag a branch onto a cell's edges, centre and the gaps between siblings;
    verify before/after/child destinations, preserved descendants and updated
    WBS codes. Try moving into its own descendant: it must refuse. Drag sibling
    order repeatedly, fold/unfold and Undo/Redo. Reopen to verify the order.
16. **Text formatting.** Select a task/node, change family, size to 12 then 48,
    bold/italic/underline and all alignments. Large cells must reflow without
    overlaps in each hierarchy layout. Copy with Format painter, then click
    another item (also test switching modules before applying). Confirm the
    source stays unchanged. Test Reset, inherited style on new cells, undo,
    restart, backup/restore and PDF's portable font fallback.
17. **Overdue and clock.** Use yesterday/today/tomorrow/blank finish dates on
    unfinished tasks and WBS nodes. Only yesterday should be red and active
    in the log. Complete, reopen, reschedule and delete disposable items:
    resolved history must remain. Test Include resolved history and persisted
    show/hide. The bottom-right clock must follow the local computer's date/time.
    Leave open through local midnight or restart the next day to verify newly
    overdue items are recorded. Check a log save failure produces a retry banner.
18. **Graph.** Create a separate GRAPH diagram. Enter and Insert both add a
    linked cell from the selection, with direct title editing. Unlinked node
    creates an independent cell. Add multiple
    incoming/outgoing leads and a cycle; choose Curve and Sharp bends, single
    and double arrows. Drag cells freely at multiple zooms: leads should follow
    and the saved position should survive restart. Check automatic new cells
    avoid occupied space while existing cells stay put. Label/reverse/delete
    a lead by right-click/double-click; confirm full label tooltip and readable
    placement. Export connection CSV/XML/PDF, verify endpoints/directions, and
    check the PDF detail appendix preserves full labels and notes. Undo/Redo,
    delete an endpoint, restart and backup/restore must preserve the graph or
    remove its incident leads appropriately. WBS planning profiles remain in
    the combined Mind map / WBS module; free graph links must not become scheduling dependencies.

19. **3.1 open workspace and ribbon.** On creating either diagram type, the root
    must start centred with space on every side. Drag blank space, middle-drag
    and Space+drag in all directions, repeatedly passing beyond the original
    top/left region; no visible page boundary should stop panning. Hierarchy
    connectors must follow during canvas expansion. Wheel/Shift+wheel pan and
    Ctrl+wheel zooms around the pointer. Fit / centre recentres content. Pan
    while a title is pending and verify it saves first. Drag Graph cells into
    areas above/left of the original root, reopen/restore and check positions.
    Export signed positions: PDF must include all nodes without large empty
    workspace margins. Check the clock in the bottom-right corner at compact,
    maximised and mixed-DPI sizes. Ribbon groups must keep consistent heights,
    scroll sideways and collapse/expand via the arrow or double-clicked tab.
20. **3.1 merged modules and automatic leads.** The menu must contain To-do,
    Mind map / WBS and Graph. Open a backup last saved in old WBS mode: it must
    open the combined module with its top-down layout intact. Do the same for
    old Mind map and Connections data. Select a Graph node, press Enter, type
    its new title, then Insert: both new cells must have a lead from the prior
    selection. Repeat with Incoming and Both direction choices, curves and
    sharp bends. Unlinked node must create no lead. Right-click an existing
    lead to reverse or make it double-ended; reopen to verify it persists.


21. **3.2 Enter policy.** In both modules, create a node, type its title and press
    Enter once: the editor must close with no extra node. Press Enter again:
    one new node appears with its title field ready. Hold Enter and ensure no
    repeated nodes are created. Test editing existing titles, blank title errors,
    autosave, Insert, Ctrl+Enter, keyboard focus and save-error retry.
22. **Exact WBS promotion.** In Top-down layout create branches 1.1–1.7 and
    child 1.6.1. Drag 1.6.1 into the highlighted gap between 1.5 and 1.6. It must
    become 1.6, the old 1.6 becomes 1.7, and IDs/descendants stay unchanged.
    Test left/right cell edges and Move before/after from the context menu;
    repeat at zooms 20%/100%/200%, undo, restart and backup/restore.
23. **Graph leads.** Click a lead; it must highlight and expose both endpoint
    handles. Drag each handle onto every side centre, including retargeting to
    another node. The opposite end must stay attached. F2, double-click,
    right-click and Insert > Lead label must open the editor with label focus.
    Save a Unicode/multiline label; it must show on the line and survive restart.
    Test reverse and double arrows, sharp bends without diagonal segments,
    selection then Delete, undo, and CSV/XML/PDF endpoint-side preservation.
24. **Ribbon and node designs.** View must always be visible at compact size.
    In Format, font/size/B/I/U/alignment/painter/reset must not clip. Buttons
    must use flat vector motifs and descriptive hover tips. Try all sixteen colour
    packs and ten designs; node/lead colours change while app chrome stays consistent.
    Format shows miniature shape and colour previews; the down-arrow opens all
    choices. Confirm the selected option is marked and hover tips describe it.
    Tab to a tile and apply with Enter/Space; dismiss the popup with Escape or
    an outside click. Check scrolling in compact windows at 100/150/200% DPI.
    Flat nodes have no box; underlined nodes have only a bottom rule. Mixed uses
    box roots, underlined branches and flat leaves. Verify priorities/overdue stay
    distinct, leads follow each colour pack and selection never changes wrapping.
    Check all five layout previews in View. Change palette/style, Undo/Redo,
    restart and backup/restore; titles, WBS codes, graph positions and leads stay intact.
    Short root/branch/leaf nodes have different sizes. A long title such as
    'DN100 Stub Pipe vent from Drain Valve - stainless steel fabrication and
    supports' must wrap fully in every layout, including 12/48 px font sizes.
25. **Smooth zoom.** Ctrl+wheel repeatedly around a chosen pointer point and
    drag the slider. Existing nodes/leads must scale without blank frames or
    content rebuilding. Pan/drag/focus during and after zoom, then restart to
    check saved zoom. Check failed zoom saves remain visible and retryable.
26. **PDF preview and printing.** Export PDF: preview must appear before a file
    dialog, with fit-to-paper and A4 landscape selected. Inspect every page;
    compare saved PDF's nodes, ports, styles, text and page layout to preview.
    Test A4/A3 portrait/landscape, optional full details and explicit tiling.
    Cancel preview/file dialogs; no export or data change should occur. Verify
    errors are visible for unwritable targets. Print opens normal Windows
    printer selection and uses the selected orientation/paper; cancel normally.
    PDF saving must work with no printer installed or external PDF viewer.

23. **3.5 shared ribbon and placement.** At 520×400, 940×620 and 1440×900, verify
    caption controls touch the right edge, module selection is under Home, the
    footer clock is bottom-right and the full ribbon has no clipped controls.
    Collapse it, change every tab and invoke its compact command icons, diagram
    picker, module picker and gallery popups. Hover/focus every command.
    In each hierarchy layout, enable View → Placement → Free move and drag a root,
    branch and leaf into empty/occupied space and across negative world positions.
    Nearby nodes must make room while numbering stays unchanged. Test Esc, Undo,
    Redo and restart. Turn off Free move and repeat structural reparent/reorder.
    Use long titles and 48 px fonts, add children, fold/unfold and reset positions.
    Observe frame smoothness at 100/150/200% scaling and with a large diagram.
24. **PC copies.** Save a .sumapp file to Documents. Edit live tasks; verify the
    copy remains a snapshot and profile changes persist. Restore the saved copy,
    confirm replacement and check the safety backup. Cancel dialogs and try an
    unwritable target: error must be visible and live data preserved.
25. **Original PNG pending.** After installing an accessible authoritative PNG
    with package-brand.py and rebuilding, compare the header/icon resources to
    its SHA256 and verify executable/window/taskbar/shortcut icons on Windows.

26. **Desktop logo picker.** Home → Brand → Choose logo PNG: select the supplied
    transparent desktop file. Verify original proportions in the header, icon
    updates on the window/taskbar/open dialogs, and restart/backup/copy/restore.
    Cancel and damaged/oversized image selection must preserve the previous logo
    and task data. The Explorer executable icon is unchanged until a rebuild.

27. **3.5.1 startup repair.** Unzip the new portable build under a normal account.
    Launch from Explorer in its extracted folder and from PowerShell while the
    current folder is an empty directory outside the app folder. The bundled
    default icon, SUMAPP and Eire logos must render without accessing any
    C:\SUMAPP;component path or requiring an external Assets folder. Repeat
    using the existing profile and custom desktop logo. Run --capture-previews
    to check decoded default branding before the temporary chosen-PNG step.
    Existing projects/tasks/diagrams and saved window settings must remain.
