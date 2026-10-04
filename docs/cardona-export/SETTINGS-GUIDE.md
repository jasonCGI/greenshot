# Greenshot settings guide

These images render the actual development build controls offscreen at 125% UI size. They are not native desktop captures. Native mouse/keyboard and Save As acceptance remains pending.

## UI size

Open **Preferences > General > UI size**. Choose 100%, 125%, 150%, or 200%. Use **Reset to 100%** to restore the default. This changes application controls separately from Windows scaling and image zoom.

![Preferences General with UI size set to 125% and Reset to 100%](whitepaper/images/preferences-ui-size.png)

The hotkey labels distinguish monitor captures from application-window captures. On multiple displays the tray menu offers all monitors, the monitor at the pointer, or a listed monitor.

## Output format and export profiles

Open **Preferences > Output**. Select WebP in **Image format (PNG, JPEG, WebP...)**; the compression controls are further down in Quality settings. Save As can choose a format for one file.

Choose Web PNG, Print JPEG, or Lossless WebP in **Export profiles**, then press **Apply**. Merely selecting a profile does not apply it. Web PNG uses 72 DPI metadata; Print JPEG uses 300 DPI metadata and quality 90; Lossless WebP uses exact compression. Profiles do not resize pixels.

Enter a unique **New profile name** and press **Save current as** to retain the current export preferences. Up to 20 custom profiles are supported. **Delete custom** removes a saved custom profile without changing the current export settings. Profiles do not store paths, overwrite policy, or filename patterns. Preferences uses live bindings, so Cancel is not a full rollback of valid edits.

![Output settings showing profiles, image format discovery, and DPI controls](whitepaper/images/output-export-profiles.png)

This example selects Web PNG without pressing Apply, so the active DPI remains Preserve. Quality controls continue below the scroll position.

## Export resolution and summary

Enable the quality prompt under **Preferences > Output**. When saving PNG or JPEG, choose Preserve, Web 72, Print 300, or Custom DPI. The summary shows pixels, selected resolution, nominal print dimensions, and compression. Resolution metadata alone does not resize pixels.

![PNG quality dialog with Print 300 DPI and the live export summary](whitepaper/images/export-resolution-summary.png)

The example is 1200 by 900 pixels at 300 DPI, nominally 4 by 3 inches. JPEG quality is disabled because this example saves PNG. WebP is available under **Preferences > Output > Image format** and in **Save As**; its compression controls are separate and DPI presets do not apply.

## Inches and centimeters

Open the editor **Resize** dialog. Select Inches or Centimeters for dimensions and enter Print DPI from 1 to 2400. This explicitly resamples the image. Keep aspect ratio to derive the other dimension, or unlock it to specify both dimensions, which may change proportions.

![Resize dialog with four by three inches at 300 DPI yielding 1200 by 900 pixels](whitepaper/images/resize-physical-size.png)

Enlarging requires **Allow upscaling** acknowledgment and adds no detail. Cancel leaves the resize effect unchanged. Later PNG/JPEG export presets can override the resized image DPI.

The [published whitepaper](https://cardonalab.dev/static/greenshot-export/index.html#settings-screenshots) includes these figures and downloadable full-size images.
