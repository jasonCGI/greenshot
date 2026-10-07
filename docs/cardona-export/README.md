# Cardona Pipeline Tools: Greenshot export preview

This is an unofficial development fork of [Greenshot](https://github.com/greenshot/greenshot), based on revision `118d1c1a11ee7b434a8235ee1d80482e0020a8a8`. It preserves upstream license notices and does not imply official endorsement or a released installer.

## Features

- Export profiles: explicit Apply for Web PNG, Print JPEG, and Lossless WebP; up to 20 custom profiles stored with export preferences.

- PNG/JPEG resolution: Preserve, Web 72, Print 300, or Custom 1 to 2400 DPI. Presets change metadata without resizing pixels. Quick Preferences and the quality dialog share the setting.
- Still-image WebP: explicit Lossy/Lossless mode and separate quality 0 to 100. Lossless preserves rendered RGBA; WebP does not apply the PNG/JPEG DPI presets.
- Resize: exact unlocked pixels, consistently rounded aspect bounds, percentages, and inches/cm with explicit DPI resampling. Enlarging requires acknowledgment and adds no detail.
- Export summary: rendered pixels, PNG/JPEG DPI, nominal inches/cm, and compression, shown when the quality prompt is enabled.
- Application UI size: 100%, 125%, 150%, or 200%, live update and reset. Independent of Windows scaling and image zoom.
- Capture labels distinguish application windows from monitor choices.

See the [illustrated settings guide](SETTINGS-GUIDE.md) and [public whitepaper with screenshots](https://cardonalab.dev/static/greenshot-export/index.html).

## Build and verification

Windows, .NET Framework 4.8 desktop targeting support, Microsoft Visual Studio 2022 Build Tools, and an installed .NET SDK compatible with the upstream `global.json` are required. Full Git history is used by upstream build versioning.

Run `docs/cardona-export/scripts/Build-Preview.ps1 -Test` from a PowerShell terminal at the repository root. This builds the upstream build helper, lightweight preview, and test project. It runs the selected export/UI/resize regression suite. It does not install or launch the app. Results are written to `docs/cardona-export/test-results/`.

`-NoRestore` requires a prior build with cached packages, the unchanged build helper, and plugin builds. The normal restore graph was previously blocked by a package-feed failure in the development environment; it has not been independently retested.

Latest local evidence, October 7, 2026: 595 focused tests passed, zero failures, errors, timeouts, or aborts. This is not the full upstream suite. Ten copied-library PNG/JPEG comparisons additionally verified density tags, unchanged source, and equal PNG pixels. Existing upstream analyzer warnings remain. Test logs contain local environment details and are not published here.

Native mouse/keyboard, Save As, overwrite, mixed-monitor, and Photoshop placement acceptance remains pending. The illustrated controls are real WPF controls rendered offscreen, not native desktop captures. Native OS dialogs and plugin-specific legacy dialog scaling are outside the new UI-scale guarantee.

Keep Preserve and UI size 100 as current defaults. Main Preferences retains upstream live bindings; Cancel is not a full rollback of valid in-memory edits. Do not run this preview beside installed Greenshot: the single-instance lock is shared. Save your work and normally exit the installed copy first.

Application source is on `codex/export-dpi-presets`. The GitHub prerelease packages the lightweight preview. No upstream pull request or installer is included. See PREVIEW-README.md, ACCEPTANCE-CHECKLIST.md, and CODE-REVIEW.md.

## October 5 preview update

Save As exposes the current export format, DPI preset, and image pixels. Capture picker profile choices apply to one save and preselect the profile format without changing output defaults. A repeat launch of the same executable opens Preferences using the existing settings command. A different running installation retains the instance chooser or isolated-launcher guard. Native launch, Save As, cancellation, keyboard, and monitor acceptance remain pending.

## October 7 sprints

See [the sprint record](SPRINTS-2026-10-07.md) and [illustrated guide](SETTINGS-GUIDE.md). Added protected profile editing/rename/duplication and transactional XML import/export, final-format Review and Save As, stable shared-preference launcher, and local diagnostics. Fixed export-cache separation for automatic palette settings. Native acceptance remains open.
