# Build and verification evidence

Current delivery: version **3.5.0**, including the combined mind-map/WBS module, five layouts,
CSV/MSPDI/P6 PMXML/PDF exports, inline titles, a grouped ribbon, branch drag/drop,
text formatting, overdue history, a bottom-right clock and the Graph module, the custom Windows app icon,
larger typography and high-contrast window/control styles.
The two user-supplied Windows screenshots demonstrate 1.0 launching and
rendering, and identify readability/label defects; they do not verify all
Windows acceptance checks or the updated 3.5 interface.

Version 3.3 also adds the corrected SUMAPP wordmark/icon, exact shared brand
palette, light surfaces, priority states, project deletion and real font-based
node sizing. The Eire header now uses a compact prepared rendition of the supplied emblem. The
provided design previews are SVG renders of shared Core geometry/theme tokens;
they are not native WPF captures. Windows hover/focus/resizing/logo rendering,
Explorer/taskbar/shortcut icon behaviour and non-elevated launch remain unrun.

Version 3.4 adds visual galleries and ten new colour packs, compatibility/backup
checks, flat/underline PDF decoration checks and style-independent layout checks.
An independent PyMuPDF reader opened all ten node-design PDFs and confirmed
the full sample titles and fitted A4 landscape pages. All CSV/MSPDI/P6 and graph
exports were also rechecked with the independent parsers documented below.

New executed checks cover atomic project deletion/reassignment, empty-project
removal, save-failure rollback, glyph widths, font sizing, metadata allowance,
renderer-supplied geometry, all node-state contrasts and priority persistence.
All normal node states meet 4.5:1 text contrast, including root, child, priority
and overdue surfaces, across all sixteen palette variants and ten node designs.

The Windows build embeds the SUMAPP artwork, the matching nine ICO frames,
standard-user manifest and self-contained runtime. `--capture-previews` provides
actual Windows screenshots and native long-title/resizing checks with disposable
sample data. It has compiled successfully but has **not run** in Linux.

Executed in the Linux x86_64 cloud workspace using the checksum-verified
Microsoft .NET SDK 10.0.401, under user ID 1000 (not root).

| Check | Outcome |
| --- | --- |
| WPF source build | Passed; initial build reported 0 warnings and 0 errors |
| Final saved Linux setup script | Executed successfully, including locked restores, all functional checks and Windows publish |
| Windows x64 self-contained publish | Passed; `artifacts/windows-x64/SUMAPP.exe` produced |
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
| PDF tiling/detail pages, embedded font, full labels/notes and Unicode | Opened with PDFsharp, Poppler `pdfinfo` / `pdftotext`; sample chart page visually inspected; new fitted landscape/portrait samples also reviewed |
| Branch drag destinations, identity/order/code preservation and circular-drop rejection | Passed core checks; actual mouse dragging requires Windows |
| Mixed font sizes 12–48 across all hierarchy layouts | Passed non-overlap checks |
| Graph automatic placement, stable manual positions, multiple leads, cycles and endpoint cleanup | Passed core checks |
| Lead routes, cell-edge arrows, upright labels and intervening-cell avoidance | Passed geometry checks; PDF chart visually reviewed |
| Overdue local-date rules, resolved episodes, restart and backup/restore | Passed core checks |
| New text formats, graph data, module/log preferences and failed-save rollback | Passed core checks |
| Connection CSV/XML/PDF | Passed independent Python parsers and Poppler text extraction, preserving node/lead data, positions, directions, formats, full Unicode notes and labels |
| Legacy WBS/Mind map/Connections modes and saved layouts | Passed numeric compatibility and restart checks |
| Centred initial view, open margins and viewport origin changes | Passed pure coordinate checks at multiple zooms/view sizes; actual panning requires Windows |
| Graph linked node creation and outgoing/incoming/both/unlinked options | Passed core checks; Enter/Insert UI event dispatch requires Windows |
| Signed graph positions and compact PDF bounds | Passed restart/backup/export checks; independent CSV/XML checks include signed positions |
| Enter editing/creation and repeat policy | Passed core input policy; native keyboard dispatch unrun |
| Exact 1.6.1 promotion between 1.5 and 1.6 | Passed branch/ID/descendant preservation, gap target and renumbering checks |
| Compact levels and full title wrapping | Passed mixed-size collision checks and full title text in chart drawing |
| Manual graph ports | Passed all top/right/bottom/left combinations for curves and orthogonal bends, with upright labels |
| Canvas-origin routing stability | Passed signed-coordinate translation and obstacle-route invariance checks |
| Labels, port sides, designs and palettes | Passed restart, backup/restore, CSV/XML and deletion checks |
| PDF preview drawing and page options | Same vector plan drives preview/PDF; A4/A3, landscape/portrait, fit/tiles and detail pagination passed core checks; independent A4 dimensions checked |
| Ribbon vector motifs, unclipped bands, endpoint mouse dragging, zoom animation and printing | Compiled; **native interaction not run** on Linux |
| Native Microsoft Project and P6 imports | **Not run**; import guide and acceptance steps included |
| Packaged executable's PE architecture and GUI subsystem | Inspected: Windows x64 GUI |
| Packaged executable's embedded privilege manifest | Inspected: `asInvoker`, `uiAccess=false` |
| Packaged executable's icon | Passed: all nine embedded 16–256 px frames match the source ICO byte-for-byte; window/header assets are bundled. Windows Explorer/taskbar rendering not run |
| Packaged executable's embedded DPI manifest | Inspected: `PerMonitorV2` |
| Single-file runtime contents | Inspected: 395 entries, WPF assemblies/native libraries, embedded CLR/JIT host, included .NET and WindowsDesktop 10.0.12 frameworks; no installed-framework reference |
| Interactive Windows desktop and standard-user Windows launch | **Not run**; Linux cannot execute WPF |
| Real display scaling, ribbon/inline editing, node dragging, window controls, topmost and opacity | **Not run**; Windows acceptance steps provided |
| Windows PowerShell SDK bootstrap/build script | **Not run**; exact commands and checksum-verified portable SDK bootstrap provided |
| Company allowlisting, SmartScreen, AppLocker or other endpoint restrictions | **Not tested**; unsigned app respects existing policy |

The functional runner reports **61 passed; 0 failed**. These are sixty-one named groups
of assertions, not interactive UI tests. No tests were skipped. Settings
checks validate storage and restoration, not native window behaviour.

Reproduce current checks:

```bash
bash scripts/setup-cloud.sh
python3 scripts/inspect-build.py artifacts/windows-x64/SUMAPP.exe
# Optional independent export checks (Java + Poppler required):
python3 -m pip install --target /tmp/eire-export-tools mpxj==16.10.0 JPype1==1.7.1
PYTHONPATH=/tmp/eire-export-tools python3 scripts/verify-chart-exports.py
```

The release ZIP contains `SUMAPP.exe`, `QUICKSTART.txt`,
`WINDOWS-VERIFICATION.md`, `PLANNING-EXPORTS.md` and `THIRD-PARTY-NOTICES.txt`.
Its PDF library and four regular/bold/oblique/bold-oblique font faces are embedded; no separately installed runtime or
printer driver is needed.
Artifacts are unsigned. See `WINDOWS-VERIFICATION.md` for the remaining standard-
account desktop checks. Source and portable ZIPs are delivered on the GitHub branch
`delivery/windows-v1` under `downloads/`. They are repository downloads, not
a GitHub Release or a cloud-environment publication.

Version 3.5 executed six new functional groups: collision-free free placement with
exact pinned positions and unchanged hierarchy/numbering; all layouts with long
titles, fonts, insertion and folding; live profile restart plus .sumapp snapshot
restore; negative hierarchy PDF compaction and portrait/landscape fitting; and
deterministic 1,000-node movement. Placement alone took 2 ms in the last measured
Linux sample. This is not a measurement of native mouse/UI smoothness. Shared
ribbon, caption edge, footer clock and frame-coalesced drag code compile; actual
Windows events, resizing and smoothness remain unrun. The exact latest PNG icon
replacement is blocked on access to the original artwork; previous assets remain.

The sixth new group verifies PNG-byte persistence across live data/restart/copy/
restore, legacy documents without a logo, and rejection of damaged/oversized PNGs
without data mutation. The desktop PNG picker and dynamic WPF icon resource
compile; native decoder/rendering/taskbar checks have not executed on Linux.
