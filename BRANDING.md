# SUMAPP brand assets and verification

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

**Pending Eire asset:** its inline preview is visible in the conversation but
its original image bytes are not present in the execution workspace. Please
provide its original PNG or SVG file to finalise that branding. Until then,
the right header says “Eire · logo pending”. `Branding.ApplyEireLogo` displays
the authoritative PNG once `src/EireTodo.Windows/Assets/EireLogo.png` is present
and the app is rebuilt; it preserves proportions on a neutral backing. If an
SVG is supplied, preserve its original and render a proportional PNG for WPF.

Exact brand colours live in `BrandTheme.cs` for geometry/export rendering and
the semantic resources at the start of `App.xaml` for WPF components. Typography,
field/button padding, minimum control height and corner radius are grouped
with those resources. Errors/overdue states use a separate red palette.
Node text/background contrasts across every palette, depth, priority and overdue
combination are tested against 4.5:1. Logo rendering and hover/focus/selected
states need the native Windows checklist; compilation cannot establish them.

`previews/SUMAPP-to-do-design.png` and `previews/SUMAPP-mind-map-design.png` are
clearly labelled **design previews**, rendered from the shared Core geometry and
brand tokens. They show sample data and pending Eire branding. They are not WPF
screenshots. Run `bash scripts/setup-cloud.sh` and then
`python3 scripts/design-previews.py` to regenerate them (optional PyMuPDF needed).

For actual Windows screenshots, unzip the app and run this in a normal PowerShell
window, from its folder:

```powershell
.\SUMAPP.exe --capture-previews "$env:USERPROFILE\Pictures\SUMAPP-previews"
```

This opens the real WPF app with disposable sample data, captures both views,
checks the header and View tab at three window sizes and measures long titles
in Segoe UI/Arial/Consolas at 12–48 px. It saves two PNGs and `visual-checks.txt`.
It never opens the normal profile. This command has compiled but cannot execute
in the Linux environment. See `WINDOWS-VERIFICATION.md` for interactive checks.

Build on Windows under a standard user account with the exact commands in
`README.md` / `scripts/build-windows.ps1`, without changing execution policy.
The portable release is self-contained and requests `asInvoker`. If company
policy blocks the unsigned executable, use the normal IT approval process.
