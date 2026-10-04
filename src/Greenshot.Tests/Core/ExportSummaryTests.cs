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

using System.Drawing;
using Greenshot.Base.Core;
using Greenshot.Base.Core.Enums;
using Greenshot.Base.Interfaces.Plugin;
using Xunit;

namespace Greenshot.Tests.Core
{
    public class ExportSummaryTests
    {
        public ExportSummaryTests() { TestEnvironment.EnsureInitialized(); }
        [Theory]
        [InlineData(ExportDpiPreset.Print, 144, "300 x 300 DPI", "4 x 3 in")]
        [InlineData(ExportDpiPreset.Custom, 600, "600 x 600 DPI", "2 x 1.5 in")]
        [InlineData(ExportDpiPreset.Preserve, 144, "144 x 96 DPI", "8.33 x 9.38 in")]
        public void Summary_UsesSelectedMetadataAndIndependentAxes(ExportDpiPreset preset, int custom, string dpi, string inches)
        {
            var settings = new SurfaceOutputSettings("png") { PreviewSize = new Size(1200, 900), PreviewDpiX = 144,
                PreviewDpiY = 96, ExportDpiPreset = preset, CustomDpi = custom, ReduceColors = false };
            string summary = ExportSummary.Describe(settings);
            Assert.Contains("1200 x 900 pixels", summary);
            Assert.Contains(dpi, summary);
            Assert.Contains(inches, summary);
            Assert.Contains("does not resize pixels", summary);
        }
        [Theory]
        [InlineData(true, "lossless compression")]
        [InlineData(false, "lossy, quality 81")]
        public void Webp_DoesNotPromiseDpiOrPrintSize(bool lossless, string compression)
        {
            var settings = new SurfaceOutputSettings("webp") { PreviewSize = new Size(1200, 900), WebpLossless = lossless,
                WebpQuality = 81, ExportDpiPreset = ExportDpiPreset.Custom, CustomDpi = 0 };
            string summary = ExportSummary.Describe(settings);
            Assert.Contains(compression, summary);
            Assert.Contains("DPI presets do not apply", summary);
            Assert.DoesNotContain("print size", summary);
        }
    }
}
