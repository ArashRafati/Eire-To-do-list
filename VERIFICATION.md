# Build and verification evidence

Current delivery: version **2.1.0**, including mind-map/WBS modules, five layouts,
CSV/MSPDI/P6 PMXML/PDF exports, saved charts, the custom Windows app icon,
larger typography and high-contrast window/control styles.
The two user-supplied Windows screenshots demonstrate 1.0 launching and
rendering, and identify readability/label defects; they do not verify all
Windows acceptance checks or the updated 2.1 interface.

Executed in the Linux x86_64 cloud workspace using the checksum-verified
Microsoft .NET SDK 10.0.401, under user ID 1000 (not root).

| Check | Outcome |
| --- | --- |
| WPF source build | Passed; initial build reported 0 warnings and 0 errors |
| Final saved Linux setup script | Executed successfully, including locked restores, all functional checks and Windows publish |
| Windows x64 self-contained publish | Passed; `artifacts/windows-x64/EireTodo.exe` produced |
| Task creation/editing/completion/reopening/deletion and restart | Passed automated core checks |
| Project creation/rename/archive/unarchive and assignment rules | Passed automated core checks |
| Filters for every column, combined AND behaviour, blank fields, open/inclusive bounds, hide completed | Passed automated core checks |
| Australian date input/output and reversed/invalid date rejection | Passed automated core checks |
| Window geometry, opacity, topmost and column widths serialized/reloaded/restored | Passed automated core checks |
| Full-data export/restore, invalid restore rejection and pre-restore safety copy | Passed automated core checks |
| Failed-save rollback, previous-save recovery and single-profile lock | Passed automated core checks |
| Chart creation, sibling/child order, permanent IDs, moves, indent/outdent and branch deletion | Passed automated core checks |
| Circular/missing parents, duplicate IDs, invalid chart dates and depth | Rejected by automated validation checks |
| Five layouts, uneven subtrees, multiple roots and folded/full views | Passed automated geometry and non-overlap checks |
| Dynamic branch placement, stable sides, folding, side persistence and manual rebalancing | Passed automated checks |
| Successive sibling/child additions to 1,000 nodes | Passed deterministic placement and collision checks; all five layouts checked at the limit |
| Chart edits, module, selected diagram, zoom and backup/restore after restart | Passed automated core checks |
| Invalid/failed chart commits and invalid restores | Passed rollback and preservation checks |
| Planning date defaults, finish-only dates, duration and summary rollup | Passed automated checks |
| CSV quoting, BOM/UTF-8, multiline notes, all rows and parent IDs | Passed core checks and independent Python CSV parser |
| MSPDI and P6 PMXML contents | Passed core checks and independent MPXJ 16.10.0 reads: hierarchy, permanent IDs, dates, 24-hour sample duration, 100% completion and notes; P6 namespaces 18.8/23.12/24.12/25.12 |
| PDF tiling/detail pages, embedded font, full labels/notes and Unicode | Opened with PDFsharp, Poppler `pdfinfo` / `pdftotext`; sample chart page visually inspected |
| Native Microsoft Project and P6 imports | **Not run**; import guide and acceptance steps included |
| Packaged executable's PE architecture and GUI subsystem | Inspected: Windows x64 GUI |
| Packaged executable's embedded privilege manifest | Inspected: `asInvoker`, `uiAccess=false` |
| Packaged executable's icon | Passed: all nine embedded 16–256 px frames match the source ICO byte-for-byte; window/header assets are bundled. Windows Explorer/taskbar rendering not run |
| Packaged executable's embedded DPI manifest | Inspected: `PerMonitorV2` |
| Single-file runtime contents | Inspected: 395 entries, WPF assemblies/native libraries, embedded CLR/JIT host, included .NET and WindowsDesktop 10.0.12 frameworks; no installed-framework reference |
| Interactive Windows desktop and standard-user Windows launch | **Not run**; Linux cannot execute WPF |
| Real display scaling, dragging/resizing, minimise/close, topmost and opacity | **Not run**; Windows acceptance steps provided |
| Windows PowerShell SDK bootstrap/build script | **Not run**; exact commands and checksum-verified portable SDK bootstrap provided |
| Company allowlisting, SmartScreen, AppLocker or other endpoint restrictions | **Not tested**; unsigned app respects existing policy |

The functional runner reports **23 passed; 0 failed**. These are twenty-three named groups
of assertions, not interactive UI tests. No tests were skipped. Settings
checks validate storage and restoration, not native window behaviour.

Reproduce current checks:

```bash
bash scripts/setup-cloud.sh
python3 scripts/inspect-build.py artifacts/windows-x64/EireTodo.exe
# Optional independent export checks (Java + Poppler required):
python3 -m pip install --target /tmp/eire-export-tools mpxj==16.10.0 JPype1==1.7.1
PYTHONPATH=/tmp/eire-export-tools python3 scripts/verify-chart-exports.py
```

The release ZIP contains `EireTodo.exe`, `QUICKSTART.txt`,
`WINDOWS-VERIFICATION.md`, `PLANNING-EXPORTS.md` and `THIRD-PARTY-NOTICES.txt`.
Its PDF library and font are embedded; no separately installed runtime or
printer driver is needed.
Artifacts are unsigned. See `WINDOWS-VERIFICATION.md` for the remaining standard-
account desktop checks. Source and portable ZIPs are delivered on the GitHub branch
`delivery/windows-v1` under `downloads/`. They are repository downloads, not
a GitHub Release or a cloud-environment publication.
