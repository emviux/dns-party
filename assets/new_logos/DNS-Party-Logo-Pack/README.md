# DNS Party — Logo Asset Pack

A practical asset bundle for the DNS Party Windows application.

## Design notes
- The app name is intentionally absent from the symbol.
- The original cyan/blue/purple identity is retained.
- App and tray icons use a crisp, low-glow treatment for legibility at small sizes.
- The square app icons use a dark navy tile; transparent logo files are also included.

## Contents
- `Icons/`: Windows `.ico` plus PNG app icons from 16×16 through 1024×1024.
- `Branding/`: transparent logo, white/dark monochrome marks, light/dark background versions, SVG wrapper, and original concept preview.
- `Tray/`: default, active, connected, changing, and error tray assets at 16, 20, 24, 32, and 48 px.
- `Installer/`: installer logo artwork.
- `Overlay/`: compact overlay badge.

## Suggested usage
- Windows executable icon: `Icons/app.ico`
- Windows taskbar/start menu: `Icons/app.ico` or `Icons/app-256.png`
- In-app header: `Branding/logo-transparent.png`
- Light UI: `Branding/logo-dark.png`
- Dark UI: `Branding/logo-white.png` or `Branding/logo-transparent.png`
- System tray: matching state asset from `Tray/`
- In-game overlay: `Overlay/overlay-badge-256.png`

## Important
The supplied SVG is an SVG wrapper containing the transparent PNG artwork, not a hand-traced vector path. For pixel-perfect small-size rendering, use the PNG/ICO exports.
