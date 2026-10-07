# SUMAPP brand assets and verification

**Version 3.5 logo limitation:** the latest transparent symbol-only PNG is not yet
available as original bytes. The ChatGPT share URL returned a proxy policy denial
(403). Home → Brand → Choose logo PNG lets users select their desktop original for
header and window/taskbar icons, persisting its unchanged bytes with their data.
Executable assets below are retained provisionally; they are not the newly
attached symbol-only PNG. Supply an accessible original PNG and run
`python3 scripts/package-brand.py /path/to/logo.png`, then rebuild. The PNG is
copied byte-for-byte into header/window resources, while the ICO is encoded in
nine sizes with uniform scaling and transparent clear space. No artwork is
recreated or recoloured.

The user authorised changing the supplied **SAMAP** wordmark to **SUMAPP**.
`Assets/SumappLogo.png` is the resulting transparent logo, prepared from that
reference using the image editing tool. Its teal ribbon symbol and colours are
retained. No replacement Eire company logo has been invented.

SUMAPP is the application name, executable/product name, left header brand and
window/taskbar/shortcut icon source. The nine-resolution ICO contains 16, 20,
24, 32, 40, 48, 64, 128 and 256 px frames. The header uses a small white backing
and uniform scaling, preserving logo proportions and clear space. Internal
namespace/export identifiers and the `%LOCALAPPDATA%\EireTodo` data directory
remain compatible with existing saved tasks and diagrams.

**Eire header asset:** the compact yellow-square/three-black-bars emblem was
prepared from the user's attached inline reference with the image editing tool,
cropping the empty right section. It is bundled as `Assets/EireLogo.png`, scaled
uniformly at 24 px on a small neutral backing, alongside the ordinary UI label
“Eire”. No new company lettering or symbol was invented. The original attachment
binary was not exposed to the workspace; this prepared PNG rendition is not a
byte-identical archival copy. A later accessible original can replace this asset.

Exact brand colours live in `BrandTheme.cs` for geometry/export rendering and
the semantic resources at the start of `App.xaml` for WPF components. Typography,
field/button padding, minimum control height and corner radius are grouped
with those resources. Errors/overdue states use a separate red palette.
Version 3.4 adds ten diagram colour packs (sixteen total) without changing the
application chrome. Palette definitions and all ten node-design surfaces live in
`DiagramAppearance`; real nodes, gallery thumbnails and PDF output share them.
Flat nodes have no surrounding frame; underlined nodes have a bottom rule.
Mixed levels use a boxed root, underlined branches and flat leaves. Selection
outlines reserve constant space, and capsule corners are capped to protect titles.
Node text/background contrasts across every palette, design, depth, priority and overdue
combination are tested against 4.5:1. Logo rendering and hover/focus/selected
states need the native Windows checklist; compilation cannot establish them.

`previews/SUMAPP-to-do-design.png` and `previews/SUMAPP-mind-map-design.png` are
clearly labelled **design previews**, rendered from the shared Core geometry and
brand tokens. `SUMAPP-diagram-gallery-design.png` shows all sixteen colour packs,
ten designs and five layouts. They show sample data and Eire branding. They are not WPF
screenshots. Run `bash scripts/setup-cloud.sh` and then
`python3 scripts/design-previews.py` to regenerate them (optional PyMuPDF needed).

For actual Windows screenshots, unzip the app and run this in a normal PowerShell
window, from its folder:

```powershell
.\SUMAPP.exe --capture-previews "$env:USERPROFILE\Pictures\SUMAPP-previews"
```

This opens the real WPF app with disposable sample data, captures both views,
checks the header and View tab at three window sizes and measures long titles
in Segoe UI/Arial/Consolas at 12–48 px. It also captures the Format and View
galleries and checks the Eire resource and thumbnail visibility. It saves four
PNGs and `visual-checks.txt`.
It never opens the normal profile. This command has compiled but cannot execute
in the Linux environment. See `WINDOWS-VERIFICATION.md` for interactive checks.

Build on Windows under a standard user account with the exact commands in
`README.md` / `scripts/build-windows.ps1`, without changing execution policy.
The portable release is self-contained and requests `asInvoker`. If company
policy blocks the unsigned executable, use the normal IT approval process.
