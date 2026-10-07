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
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Greenshot.Base.Core.Enums;
using Greenshot.Base.Core.FileFormat;
using Greenshot.Base.Interfaces;
using Greenshot.Base.Interfaces.Plugin;

namespace Greenshot.Base.Core
{
    public static class ExportSummary
    {
        public static async Task PopulateAsync(IExportSource source, SurfaceOutputSettings settings, CancellationToken cancellationToken)
        {
            var preview = new SurfaceOutputSettings(WellKnownFileFormats.Png)
            {
                ExportDpiPreset = ExportDpiPreset.Preserve,
                ReduceColors = false,
                DisableReduceColors = true,
                SaveBackgroundOnly = settings.SaveBackgroundOnly
            };
            preview.Effects.AddRange(settings.Effects);
            using (var lease = await source.RenderAsync(preview, cancellationToken).ConfigureAwait(false))
            {
                settings.PreviewSize = lease.Image.Size;
                settings.PreviewDpiX = lease.Image.HorizontalResolution;
                settings.PreviewDpiY = lease.Image.VerticalResolution;
            }
        }

        public static string Describe(SurfaceOutputSettings settings)
        {
            string pixels = settings.PreviewSize.Width > 0 && settings.PreviewSize.Height > 0
                ? $"{settings.PreviewSize.Width} x {settings.PreviewSize.Height} pixels" : "Pixel dimensions unavailable";
            bool png = WellKnownFileFormats.IsEqualFormat(WellKnownFileFormats.Png, settings.Format);
            bool jpeg = WellKnownFileFormats.IsEqualFormat(WellKnownFileFormats.Jpg, settings.Format);
            bool webp = WellKnownFileFormats.IsEqualFormat(WellKnownFileFormats.Webp, settings.Format);
            string compression = webp ? (settings.WebpLossless ? "WebP lossless compression" : $"WebP lossy, quality {settings.WebpQuality}")
                : jpeg ? $"JPEG lossy, quality {settings.JPGQuality}" : png ? "PNG lossless compression" : settings.Format;
            if (png && (settings.ReduceColors || settings.AutoReduceColors) && !settings.DisableReduceColors) compression += "; palette reduction may change colors";
            if (!png && !jpeg) return $"{pixels}\n{compression}\nDPI presets do not apply to this format.";
            float? dpi;
            try { dpi = ExportDpiSettings.GetResolution(settings); }
            catch (ArgumentOutOfRangeException) { return $"{pixels}\n{compression}\nChoose a valid export resolution."; }
            double x = dpi ?? settings.PreviewDpiX;
            double y = dpi ?? settings.PreviewDpiY;
            if (x <= 0 || y <= 0 || settings.PreviewSize.IsEmpty) return $"{pixels}\n{compression}\nSource resolution unavailable.";
            return string.Format(CultureInfo.CurrentCulture,
                "{0}\n{1} x {2} DPI; nominal print size {3:0.##} x {4:0.##} in ({5:0.##} x {6:0.##} cm)\n{7}\nResolution metadata does not resize pixels.",
                pixels, x, y, settings.PreviewSize.Width / x, settings.PreviewSize.Height / y,
                settings.PreviewSize.Width / x * 2.54, settings.PreviewSize.Height / y * 2.54, compression);
        }
    }
}
