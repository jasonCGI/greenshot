/*
 * Greenshot - a free and open source screenshot tool
 * Copyright (C) 2007-2026 Thomas Braun, Jens Klingen, Robin Krom
 *
 * For more information see: https://getgreenshot.org/
 * The Greenshot project is hosted on GitHub https://github.com/greenshot/greenshot
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 1 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program. If not, see <https://www.gnu.org/licenses/>.
 */

using System;
using System.Globalization;
using Greenshot.Base.Core.Enums;
using Greenshot.Base.Core.FileFormat;
using Greenshot.Base.Interfaces.Plugin;

namespace Greenshot.Base.Core
{
    /// <summary>
    /// Validates resolution metadata independently of capture scaling and pixel resizing.
    /// </summary>
    public static class ExportDpiSettings
    {
        public const int MinimumCustomDpi = 1;
        public const int MaximumCustomDpi = 2400;

        public static bool IsValidCustomDpi(int dpi)
        {
            return dpi >= MinimumCustomDpi && dpi <= MaximumCustomDpi;
        }

        public static bool TryParseCustomDpi(string value, out int dpi)
        {
            if (int.TryParse(value?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) &&
                IsValidCustomDpi(parsed))
            {
                dpi = parsed;
                return true;
            }

            dpi = 0;
            return false;
        }

        /// <summary>
        /// Null means preserve source resolution. An inactive custom value is ignored.
        /// </summary>
        public static float? GetResolution(ExportDpiPreset preset, int customDpi)
        {
            switch (preset)
            {
                case ExportDpiPreset.Preserve:
                    return null;
                case ExportDpiPreset.Web:
                    return 72;
                case ExportDpiPreset.Print:
                    return 300;
                case ExportDpiPreset.Custom:
                    if (!IsValidCustomDpi(customDpi))
                    {
                        throw new ArgumentOutOfRangeException(nameof(SurfaceOutputSettings.CustomDpi), customDpi,
                            "Custom export resolution must be a whole number from 1 to 2400 DPI.");
                    }
                    return customDpi;
                default:
                    throw new ArgumentOutOfRangeException(nameof(SurfaceOutputSettings.ExportDpiPreset), preset,
                        "Unknown export resolution preset.");
            }
        }

        /// <summary>
        /// PNG and JPEG support metadata presets. Other formats retain existing behavior.
        /// </summary>
        public static float? GetResolution(SurfaceOutputSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (!(WellKnownFileFormats.IsEqualFormat(WellKnownFileFormats.Png, settings.Format) ||
                  WellKnownFileFormats.IsEqualFormat(WellKnownFileFormats.Jpg, settings.Format)))
            {
                return null;
            }

            return GetResolution(settings.ExportDpiPreset, settings.CustomDpi);
        }
    }
}
