# Windows desktop acceptance checks

These checks are **not executed** in the Linux cloud environment. Use a standard,
non-administrator Windows 10 22H2 / Windows 11 x64 account. Do not elevate, disable
security controls, change execution policy, or bypass an organisation's rules.
If the unsigned app is blocked, seek approval through the normal IT process.
Back up existing app data first if you have used it before.

0. **1.2 icon.** Verify the executable's icon in Explorer, a normal desktop
   shortcut, the taskbar and Alt+Tab. Check task/project editor title bars and
   the widget header use the same mark. Test 100%, 150% and 200% display scaling.
   Existing pinned shortcuts may need unpinning and re-pinning to refresh their
   cached icon; use normal Windows menus without changing security settings.
0. **1.1 readability regression.** Confirm the main window, task editor and
   project editor have dark backgrounds with bright labels. Check task text
   is visibly larger, even with the previous saved window size. Project and
   category filters must display names (for example All projects), never
   `ProjectChoice` / `CategoryChoice`. Disabled date inputs must stay dark.
   Expand filters, scroll the control panel at a compact size and confirm the
   table/footer stay reachable. Test slider and both scrollbar directions.
1. **Portable launch and offline use.** Unzip the release in Documents. On a PC
   without a separately installed .NET runtime, double-click `EireTodo.exe`.
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
   Hide completed; clear filters to show them again. Cancel deletion once,
   then confirm deletion of the disposable second task.
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
    MIND MAP and create a diagram. Click its root, press Insert and name a child;
    press Enter for a sibling. Verify roots can also have siblings. Use F2 and
    double-click to edit full notes; Enter inside notes must add a line and
    Ctrl+Enter must save. Test date validation, parent moves, completion,
    reorder, indent/outdent, branch folding, Undo/Redo and confirmed deletion.
    Confirm a folded parent opens when adding/moving a child into it. Try all
    five layouts with uneven-depth branches and multiple roots. Zoom, fit,
    scroll both ways and switch to WBS then back to To-do. In a compact window,
    scroll the chart toolbar and ensure all controls stay reachable. The title
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
