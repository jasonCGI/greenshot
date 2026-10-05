# Preview acceptance checklist

Status: automated verification completed for covered paths; native desktop checks pending. Native automation is unavailable in the development session. Nothing below is marked passed without an actual interactive observation.

Use the isolated launcher, keep installed Greenshot closed, and record Windows scaling, screen resolutions, UI size, format, and expected/observed result for each check.

| Check | Expected result | Status |
| --- | --- | --- |
| PNG Preserve, Web 72, Print 300, Custom | Pixels unchanged; expected density metadata; source unchanged | Automated coverage; native save pending |
| JPEG resolution and quality | Correct density and quality; no silent resize | Automated coverage; native save pending |
| WebP lossy/lossless | Save As offers .webp; correct compression; no PNG/JPEG DPI claim | Encoder/UI coverage; native Save As pending |
| Existing filename | Overwrite or Save As follows configured policy; cancellation keeps existing file | Pending native check |
| Invalid Custom DPI and Cancel | Cannot confirm an invalid active value; cancel keeps source and file unchanged | Automated coverage; native keyboard check pending |
| UI sizes 100, 125, 150, 200 | Controls readable; scrolling reaches every action; image zoom/pixels unchanged | Offscreen coverage; native navigation pending |
| Windows scaling 100%, 150%, 200% | UI size remains independent; dialogs remain reachable | Pending native check |
| Inches/cm resizing | Expected rounded pixels; aspect lock works; enlargement requires acknowledgment | Automated coverage; native check pending |
| Built-in profiles | Explicit Apply changes export preferences only | Automated coverage; native check pending |
| Custom profile restart/delete | Normal exit and restart retains profile; delete keeps current settings | Model/INI coverage; process restart pending |
| Keyboard | Tab order, dropdowns, validation, scrolling, and actions remain usable | Pending native check |
| Monitor capture | All monitors, pointer monitor, and selected monitor match actual bounds | Pending native check |
| Photoshop placement | Open and Place behavior recorded independently against document settings | Pending native check |

For each failure, include reproduction steps, actual image dimensions/metadata where relevant, and a screenshot. Do not promote the preview to a stable release until the required native checks pass.

## October 5 preview update

Save As exposes the current export format, DPI preset, and image pixels. Capture picker profile choices apply to one save and preselect the profile format without changing output defaults. A repeat launch of the same executable opens Preferences using the existing settings command. A different running installation retains the instance chooser or isolated-launcher guard. Native launch, Save As, cancellation, keyboard, and monitor acceptance remain pending.
