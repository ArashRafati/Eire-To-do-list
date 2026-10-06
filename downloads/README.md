# Windows download — version 1.1

`EireTodo-Windows-x64.zip` contains the self-contained Windows x64 executable,
short instructions and remaining Windows acceptance checks. Download the raw
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
