# Cardona Pipeline Tools: Greenshot export preview

Unofficial Windows development preview of Greenshot. This ZIP is a lightweight DebugLight build for .NET Framework 4.8. It has no installer, updater, plugins, or AI tools. It is intended for evaluation. It has not completed native desktop acceptance or an independent clean build.

## Run with isolated settings

1. Download the ZIP and SHA256SUMS.txt from the same GitHub release. Run `Get-FileHash -LiteralPath '<downloaded ZIP filename>' -Algorithm SHA256`, replacing the placeholder with the actual filename, and compare with SHA256SUMS.txt.
2. Extract the ZIP into a writable folder. Keep all app files together. Windows with .NET Framework 4.8 is required.
3. Save open screenshots and normally Exit any running Greenshot, including an older preview. The single-instance lock is shared.
4. Run `Start-Preview.ps1` in PowerShell without changing execution policy. If Windows blocks a downloaded file, review its source and signature status before unblocking. This preview is unsigned.
5. The launcher creates `settings/` and `logs/` beside itself. It passes `--ini-directory` to isolate normal preferences and directs logs into this package. It refuses to start while Greenshot is running or an existing fixed policy is detected.

Use the launcher each time. Launching `app/Greenshot.exe` directly uses normal Greenshot configuration. This package does not replace the installed application. To return to the installed version, Exit the preview and start installed Greenshot normally.

## New in this preview

- Output settings explains where to choose WebP and how Save As chooses a format for one export.
- Export profiles: Web PNG (72 DPI metadata), Print JPEG (300 DPI metadata, quality 90), and Lossless WebP.
- Save up to 20 named custom profiles. Names must be unique, from 1 to 64 characters. Apply is explicit. Delete custom removes the saved profile without changing active export settings.
- Profiles retain format, DPI preset/custom DPI, JPEG quality, WebP mode/quality, color reduction, and quality-prompt preference. They do not retain paths, overwrite policy, filename patterns, resizing, capture behavior, or UI size.
- Existing explicit export DPI, physical resize, export summary, WebP, and independent UI scaling are included.

Preferences uses the upstream live settings bindings. Cancel is not a complete rollback of valid in-memory edits. Saving a profile follows normal configuration persistence; exit normally to retain it. Existing damaged profile storage is preserved and blocks saving/deleting until repaired. Fixed configuration policy blocks profile application.

## Verification and limits

See CODE-REVIEW.md and ACCEPTANCE-CHECKLIST.md. Focused automated tests cover the encoder and UI models; offscreen WPF rendering does not establish actual mouse/keyboard or mixed-monitor behavior. Full restore previously failed when the package feed was unavailable. This preview reused cached packages and build prerequisites, with existing analyzer warnings. Native Save As, overwrite, monitor capture, keyboard navigation, and Photoshop placement remain pending.

The package manifest identifies the exact source commit and each initial file hash. Source and build scripts are in [jasonCGI/greenshot](https://github.com/jasonCGI/greenshot). GitHub supplies source archives for the release tag. Upstream copyright and GPL notices are retained in LICENSE. Third-party attribution and license files are included. No official Greenshot endorsement is implied.
