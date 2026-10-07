# Windows download — version 3.0

`EireTodo-Windows-x64.zip` contains the self-contained Windows x64 executable,
short instructions, chart/planning import guide, component notices and remaining Windows acceptance checks. Download the raw
ZIP from GitHub, unzip into a writable folder, and double-click `EireTodo.exe`.
No installer, separate runtime installation or elevation is required. The app
is unsigned; follow normal IT approval if your organisation blocks it.

Data is stored under `%LOCALAPPDATA%\EireTodo`. Automated core checks passed;
interactive WPF behaviour and standard-user Windows launch have not been run
in the Linux build environment. See the included acceptance checklist.

`EireTodo-Source.zip` contains the source, tests, build scripts and detailed
instructions. `SHA256SUMS.txt` records checksums for both ZIPs.

These are copies of the already-built artifacts, prepared as an alternative to
the failing chat download route. Repository visibility is not changed.

Version 1.1 increases body/table text to 17 px, fixes the white window background,
uses bright labels and black/slate panels, and corrects project/category labels.
Close the previous app, unzip this release, and run the new executable. Existing
tasks and projects are read from the same data folder without a migration.

Version 1.2 embeds a custom multi-resolution black-and-yellow app icon in the
executable and uses it in the taskbar, app windows and header.

Version 2.0 adds To-do / Mind map / WBS switching, Enter/Insert node creation,
five chart layouts, local autosave/backup, and CSV/XML/PDF exports. Native
MSP/P6 import remains a Windows acceptance check; P6 CSV requires mapping into
its exported XLSX template. Use PLANNING-EXPORTS.md from the ZIP for details.

Version 2.1 improves dynamic placement: new main branches use the less crowded
side, existing sides remain stable, and subtree spacing recalculates as cells
are added/moved/deleted. It adds Balance branches and improved viewport handling.
Twenty-three automated check groups pass, including 1,000-node growth checks.

Version 3.0 adds the tabbed ribbon, inline titles, branch drag/drop, saved text
formatting and format painter, local clock, red overdue items/history, and a
separate Connections module with movable nodes and labelled directional leads.
The black/slate/yellow palette is preserved. See QUICKSTART.txt in the ZIP.
