# SUMAPP Windows download — version 3.5.1

Download `SUMAPP-Windows-x64.zip`, unzip it into a writable folder and double-click
`SUMAPP.exe`. Runtime and SUMAPP artwork/icon are bundled; no installation,
network, account or elevation is required. Existing data/settings stay under
`%LOCALAPPDATA%\EireTodo`. Close the previous executable before updating.

This build contains the exact neutral/teal/yellow colour system, supplied original SUMAPP
symbol/icon, light readable surfaces, atomic project deletion, priority states
and real font-based node sizing. It adds thumbnail galleries for 10 designs,
16 colour packs and 5 layouts, plus the supplied Eire emblem in the header.

`SUMAPP-Source.zip` includes source, tests, scripts, the original logo and instructions;
`SHA256SUMS.txt` records both archive hashes. These downloads are delivered through
GitHub as an alternative to the failing chat artifact route.

61 core check groups pass. The Windows standard-user manifest, icon resources and
bundled runtime were inspected. Native WPF, taskbar/Explorer rendering and Windows
non-elevated launch remain unrun in the Linux environment. `BRANDING.md` and
`WINDOWS-VERIFICATION.md` describe exact capture/build/acceptance commands.

Older EireTodo ZIPs retained here are superseded by the SUMAPP files above.
Do not use older executables to edit data containing the new priority fields.

The supplied original symbol-only PNG is bundled in this build and included
in the source package; see BRANDING.md. Free move, compact ribbon,
PC copies and bottom-right clock are included. Native Windows UI checks remain
unrun in Linux.

Use Home → Brand → Choose logo PNG to select your original desktop file for
header/window/taskbar branding. Its original bytes are saved in data and backups.

3.5.1 fixes the reported startup icon-path error. Re-extract the updated ZIP
and launch its executable. Keep your existing profile folder unchanged.
