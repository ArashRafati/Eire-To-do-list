# SUMAPP brand assets and verification

**Version 3.5.1 authoritative SUMAPP artwork:** the supplied original is
[`assets/teal-folded-hexagon.png`](assets/teal-folded-hexagon.png), downloaded from
https://raw.githubusercontent.com/ArashRafati/Eire-To-do-list/delivery/windows-v1/assets/teal-folded-hexagon.png.
It is a 1254 × 1254 RGBA PNG with transparency. SHA256:
`d7ebbc8aacb3917b1eb3dbfda95e0b4b36bc4a00494533e1a13a62d1e2091ff0`.

`Assets/SumappLogo.png` and `Assets/AppIcon.png` preserve those exact bytes.
The nine-frame `Assets/AppIcon.ico` derives only proportional icon-size conversions
from that PNG. The original artwork is not recreated, recoloured or retouched.
Run `python3 scripts/package-brand.py assets/teal-folded-hexagon.png` before a
source rebuild to regenerate icon frames. These assets are already embedded in
the delivered executable; no external logo file is needed when running it.
Default window/header artwork loads through absolute WPF pack resource streams,
so launch does not depend on the current folder. Home → Brand → Choose logo PNG
remains available for a user-selected override stored with their data.

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
