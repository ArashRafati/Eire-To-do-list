# Build and verification evidence

Current delivery: version **1.1.0**, including larger typography, explicit
window styles, high-contrast control templates and corrected dropdown labels.
The two user-supplied Windows screenshots demonstrate 1.0 launching and
rendering, and identify readability/label defects; they do not verify all
Windows acceptance checks or the updated 1.1 interface.

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
| Packaged executable's PE architecture and GUI subsystem | Inspected: Windows x64 GUI |
| Packaged executable's embedded privilege manifest | Inspected: `asInvoker`, `uiAccess=false` |
| Packaged executable's embedded DPI manifest | Inspected: `PerMonitorV2` |
| Single-file runtime contents | Inspected: 384 entries, WPF assemblies/native libraries, embedded CLR/JIT host, included .NET and WindowsDesktop 10.0.12 frameworks; no installed-framework reference |
| Interactive Windows desktop and standard-user Windows launch | **Not run**; Linux cannot execute WPF |
| Real display scaling, dragging/resizing, minimise/close, topmost and opacity | **Not run**; Windows acceptance steps provided |
| Windows PowerShell SDK bootstrap/build script | **Not run**; exact commands and checksum-verified portable SDK bootstrap provided |
| Company allowlisting, SmartScreen, AppLocker or other endpoint restrictions | **Not tested**; unsigned app respects existing policy |

The functional runner reports **9 passed; 0 failed**. These are nine named groups
of assertions, not nine interactive UI tests. No tests were skipped. Settings
checks validate storage and restoration, not native window behaviour.

Reproduce current checks:

```bash
bash scripts/setup-cloud.sh
python3 scripts/inspect-build.py artifacts/windows-x64/EireTodo.exe
```

The release zip contains only `EireTodo.exe`, `QUICKSTART.txt` and
`WINDOWS-VERIFICATION.md`; no separately installed runtime is needed.
Artifacts are unsigned. See `WINDOWS-VERIFICATION.md` for the remaining standard-
account desktop checks. Source and build artifacts were created locally; no
Git commit, remote push, release publication, or cloud-environment publication
has been performed.
