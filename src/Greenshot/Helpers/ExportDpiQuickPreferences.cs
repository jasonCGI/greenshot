/*
 * Greenshot - a free and open source screenshot tool
 * Copyright (C) 2007-2026 Thomas Braun, Jens Klingen, Robin Krom
 * Licensed under the GNU General Public License, version 1 or later.
 * See https://www.gnu.org/licenses/ and https://getgreenshot.org/.
 */

using System;
using System.Linq;
using System.Windows.Forms;
using Greenshot.Base.Core;
using Greenshot.Base.Core.Enums;
using Greenshot.Base.Core.FileFormat;
using Greenshot.Base.Languages;

namespace Greenshot.Helpers
{
    internal static class ExportDpiQuickPreferences
    {
        internal static bool CanSelectPreset(bool presetIsFixed, ExportDpiPreset current, ExportDpiPreset selected)
            => !presetIsFixed || current == selected;

        internal static ToolStripMenuItem Create(ICoreConfiguration configuration, Func<int, int?> requestCustomDpi)
        {
            var menu = new ToolStripMenuItem(Texts.Settings.Exportdpi)
            {
                Name = "QuickExportDpi", ToolTipText = Texts.Settings.ExportdpiDescription
            };
            foreach (ExportDpiPreset preset in Enum.GetValues(typeof(ExportDpiPreset)))
            {
                var item = new ToolStripMenuItem { Tag = preset, Name = "QuickExportDpi" + preset };
                item.Click += (sender, args) =>
                {
                    if (!IsActive(configuration) || !CanSelectPreset(configuration.IsConstant(nameof(configuration.OutputFileDpiPreset)),
                        configuration.OutputFileDpiPreset, preset)) return;
                    int customDpi = configuration.OutputFileCustomDpi;
                    bool customFixed = configuration.IsConstant(nameof(configuration.OutputFileCustomDpi));
                    if (preset == ExportDpiPreset.Custom)
                    {
                        int? chosen = customFixed ? customDpi : requestCustomDpi(customDpi);
                        if (!chosen.HasValue || !ExportDpiSettings.IsValidCustomDpi(chosen.Value)) return;
                        customDpi = chosen.Value;
                    }
                    // A modal prompt may outlive a policy or format change. Recheck before mutation.
                    if (!IsActive(configuration) || !CanSelectPreset(configuration.IsConstant(nameof(configuration.OutputFileDpiPreset)),
                        configuration.OutputFileDpiPreset, preset)) return;
                    if (preset == ExportDpiPreset.Custom)
                    {
                        if (configuration.IsConstant(nameof(configuration.OutputFileCustomDpi)))
                        {
                            if (configuration.OutputFileCustomDpi != customDpi) return;
                        }
                        else configuration.OutputFileCustomDpi = customDpi;
                    }
                    if (!configuration.IsConstant(nameof(configuration.OutputFileDpiPreset))) configuration.OutputFileDpiPreset = preset;
                    Refresh(menu, configuration);
                };
                menu.DropDownItems.Add(item);
            }
            menu.DropDownOpening += (sender, args) => Refresh(menu, configuration);
            Refresh(menu, configuration);
            return menu;
        }

        private static bool IsActive(ICoreConfiguration configuration) => !configuration.DisableQuickSettings &&
            (WellKnownFileFormats.IsEqualFormat(WellKnownFileFormats.Png, configuration.OutputFileFormat) ||
             WellKnownFileFormats.IsEqualFormat(WellKnownFileFormats.Jpg, configuration.OutputFileFormat));

        internal static void Refresh(ToolStripMenuItem menu, ICoreConfiguration configuration)
        {
            menu.Enabled = IsActive(configuration);
            foreach (ToolStripMenuItem item in menu.DropDownItems.OfType<ToolStripMenuItem>())
            {
                var preset = (ExportDpiPreset)item.Tag;
                item.Checked = configuration.OutputFileDpiPreset == preset;
                item.Enabled = CanSelectPreset(configuration.IsConstant(nameof(configuration.OutputFileDpiPreset)),
                    configuration.OutputFileDpiPreset, preset);
                item.Text = preset switch
                {
                    ExportDpiPreset.Preserve => Texts.Settings.ExportdpiPreserve,
                    ExportDpiPreset.Web => Texts.Settings.ExportdpiWeb,
                    ExportDpiPreset.Print => Texts.Settings.ExportdpiPrint,
                    _ => $"{Texts.Settings.ExportdpiCustom} ({configuration.OutputFileCustomDpi} DPI)" +
                         (configuration.IsConstant(nameof(configuration.OutputFileCustomDpi)) ? string.Empty : "...")
                };
                if (preset == ExportDpiPreset.Custom && configuration.IsConstant(nameof(configuration.OutputFileCustomDpi)) &&
                    !ExportDpiSettings.IsValidCustomDpi(configuration.OutputFileCustomDpi)) item.Enabled = false;
            }
        }
    }
}
