/*
 * Greenshot - a free and open source screenshot tool
 * Copyright (C) 2007-2026 Thomas Braun, Jens Klingen, Robin Krom
 * Licensed under the GNU General Public License, version 1 or later.
 * See https://www.gnu.org/licenses/ and https://getgreenshot.org/.
 */

using System;
using System.Drawing;
using Greenshot.Base.Core.FileFormat;
using Greenshot.Base.Interfaces.Plugin;

namespace Greenshot.Base.Core
{
    public static class WebpExportSettings
    {
        public const int MaximumDimension = 16383;

        public static bool IsWebp(string format) => WellKnownFileFormats.IsEqualFormat(WellKnownFileFormats.Webp, format);

        public static void Validate(SurfaceOutputSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (IsWebp(settings.Format)) ValidateQuality(settings.WebpLossless, settings.WebpQuality);
        }

        public static void ValidateQuality(bool lossless, int quality)
        {
            if (!lossless && (quality < 0 || quality > 100))
            {
                throw new ArgumentOutOfRangeException(nameof(SurfaceOutputSettings.WebpQuality), quality,
                    "Lossy WebP quality must be between 0 and 100.");
            }
        }

        public static void ValidateSize(Size size)
        {
            if (size.Width < 1 || size.Height < 1 || size.Width > MaximumDimension || size.Height > MaximumDimension)
            {
                throw new ArgumentOutOfRangeException(nameof(size), size, "WebP dimensions must be from 1 to 16383 pixels per side.");
            }
        }
    }
}
