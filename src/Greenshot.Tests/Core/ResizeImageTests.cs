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
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using Greenshot.Base.Core;
using Greenshot.Base.Core.Enums;
using Greenshot.Base.Effects;
using Greenshot.Base.Interfaces.Plugin;
using Xunit;

namespace Greenshot.Tests.Core
{
    [Collection(TestCollections.WpfThemeState)]
    public class ResizeImageTests
    {
        public ResizeImageTests()
        {
            TestEnvironment.EnsureInitialized();
        }

        [Theory]
        [InlineData(41, 41, 2, 2, false, 2, 2)]
        [InlineData(41, 41, 2, 2, true, 2, 2)]
        [InlineData(41, 41, 1, 1, false, 1, 1)]
        [InlineData(41, 41, 1, 1, true, 1, 1)]
        [InlineData(16, 9, 2, 1, true, 2, 1)]
        [InlineData(9, 16, 1, 2, true, 1, 2)]
        [InlineData(16, 9, 1, 1, true, 1, 1)]
        [InlineData(9, 16, 1, 1, true, 1, 1)]
        [InlineData(100, 50, 80, 20, true, 40, 20)]
        [InlineData(50, 100, 20, 80, true, 20, 40)]
        [InlineData(16, 9, 16, 16, true, 16, 9)]
        [InlineData(9, 16, 16, 16, true, 9, 16)]
        [InlineData(16, 9, 20, 14, true, 20, 11)]
        [InlineData(9, 16, 14, 20, true, 11, 20)]
        [InlineData(22, 11, 15, 8, true, 15, 8)]
        [InlineData(14, 7, 29, 15, true, 29, 14)]
        [InlineData(2, 1, 5, 2, true, 4, 2)]
        [InlineData(2, 5, 1, 3, true, 1, 2)]
        [InlineData(7, 3, 123, 47, false, 123, 47)]
        public void ResizeImage_UsesExactUnlockedSizesAndRoundedAspectBounds(int sourceWidth, int sourceHeight,
            int width, int height, bool aspectLocked, int expectedWidth, int expectedHeight)
        {
            using var source = CreateSource(sourceWidth, sourceHeight);
            using var matrix = new Matrix();
            using Image resized = ImageHelper.ResizeImage(source, aspectLocked, width, height, matrix);

            Assert.Equal(new Size(expectedWidth, expectedHeight), resized.Size);
            Assert.True(resized.Width <= width && resized.Height <= height);
            AssertDpi(resized, 144, 96);
            AssertSourceUnchanged(source);
            AssertMatrixScale(matrix, expectedWidth / (float)sourceWidth, expectedHeight / (float)sourceHeight);
        }

        [Theory]
        [InlineData(16, 9, 32, 0, false, 32, 18)]
        [InlineData(9, 16, 0, 32, false, 18, 32)]
        [InlineData(16, 9, 8, 0, false, 8, 4)]
        [InlineData(9, 16, 0, 8, false, 4, 8)]
        [InlineData(16, 9, 32, 0, true, 32, 18)]
        [InlineData(9, 16, 0, 32, true, 18, 32)]
        public void ResizeImage_OneAutomaticAxisRetainsAspectAndNormalizesCanvas(int sourceWidth, int sourceHeight,
            int width, int height, bool fixedCanvas, int expectedWidth, int expectedHeight)
        {
            using var source = CreateSource(sourceWidth, sourceHeight);
            using var matrix = new Matrix();
            using Image resized = ImageHelper.ResizeImage(source, true, fixedCanvas, Color.Blue, width, height, matrix);

            Assert.Equal(new Size(expectedWidth, expectedHeight), resized.Size);
            AssertDpi(resized, 144, 96);
            AssertSourceUnchanged(source);
            AssertMatrixScale(matrix, expectedWidth / (float)sourceWidth, expectedHeight / (float)sourceHeight);
        }

        [Fact]
        public void ResizeImage_FixedCanvasKeepsRequestedSizeCenteredPixelsAndExistingMatrixSemantics()
        {
            using var source = CreateSource(16, 9);
            using var matrix = new Matrix();
            using var resized = (Bitmap)ImageHelper.ResizeImage(source, true, true, Color.Blue, 16, 16, matrix);

            Assert.Equal(new Size(16, 16), resized.Size);
            Assert.Equal(Color.Blue.ToArgb(), resized.GetPixel(8, 0).ToArgb());
            Assert.Equal(Color.Red.ToArgb(), resized.GetPixel(8, 8).ToArgb());
            Assert.Equal(Color.Blue.ToArgb(), resized.GetPixel(8, 15).ToArgb());
            AssertDpi(resized, 144, 96);
            AssertSourceUnchanged(source);
            AssertMatrixScale(matrix, 1, 16 / 9f);
        }

        [Theory]
        [InlineData(0, 1, false)]
        [InlineData(1, 0, false)]
        [InlineData(0, 0, false)]
        [InlineData(0, 0, true)]
        [InlineData(-1, 1, false)]
        [InlineData(1, -1, false)]
        [InlineData(-1, 1, true)]
        [InlineData(1, -1, true)]
        [InlineData(32768, 1, false)]
        [InlineData(1, 32768, true)]
        [InlineData(10001, 10000, false)]
        [InlineData(10001, 10000, true)]
        [InlineData(int.MaxValue, int.MaxValue, false)]
        public void ResizeImage_InvalidBoundsRejectBeforeAllocationAndKeepSourceAndMatrix(int width, int height, bool aspectLocked)
        {
            using var source = CreateSource(16, 9);
            using var matrix = new Matrix(1, 0, 0, 1, 4, 5);
            float[] originalMatrix = matrix.Elements;

            Assert.Throws<ArgumentOutOfRangeException>(() => ImageHelper.ResizeImage(source, aspectLocked, width, height, matrix));

            Assert.Equal(originalMatrix, matrix.Elements);
            AssertSourceUnchanged(source);
        }

        [Theory]
        [InlineData(16, 1, 1, 1)]
        [InlineData(1, 16, 1, 1)]
        [InlineData(2, 1, 1, 1)]
        [InlineData(16, 1, 1, 0)]
        [InlineData(1, 16, 0, 1)]
        public void ResizeImage_AspectAxisRoundingBelowOnePixelIsRejectedWithoutClamping(int sourceWidth, int sourceHeight, int width, int height)
        {
            using var source = CreateSource(sourceWidth, sourceHeight);
            using var matrix = new Matrix(1, 0, 0, 1, 4, 5);
            float[] originalMatrix = matrix.Elements;

            Assert.Throws<ArgumentOutOfRangeException>(() => ImageHelper.ResizeImage(source, true, width, height, matrix));

            Assert.Equal(originalMatrix, matrix.Elements);
            AssertSourceUnchanged(source);
        }

        [Theory]
        [InlineData(1, 16, 32767, 0)]
        [InlineData(16, 1, 0, 32767)]
        [InlineData(1, int.MaxValue, 32767, 0)]
        [InlineData(int.MaxValue, 1, 0, 32767)]
        [InlineData(2, 3, 10000, 0)]
        public void GetResizeSize_AutomaticAxisValidatesDerivedLimitsBeforeIntegerConversion(int sourceWidth, int sourceHeight, int width, int height)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                ImageHelper.GetResizeSize(new Size(sourceWidth, sourceHeight), true, width, height));
        }

        [Theory]
        [InlineData(32767, 1)]
        [InlineData(1, 32767)]
        [InlineData(10000, 10000)]
        public void GetResizeSize_InclusiveGuardrailBoundsAreValidWithoutAllocatingLargeImages(int width, int height)
        {
            Assert.Equal(new Size(width, height), ImageHelper.GetResizeSize(new Size(16, 9), false, width, height));
        }

        [Fact]
        public void ResizeImage_NullSourceAndInvalidSourceSizeAreRejected()
        {
            Assert.Throws<ArgumentNullException>(() => ImageHelper.ResizeImage(null, false, 2, 2, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => ImageHelper.GetResizeSize(Size.Empty, false, 2, 2));
        }

        [Theory]
        [InlineData("png", ExportDpiPreset.Preserve, 144, 96)]
        [InlineData("jpg", ExportDpiPreset.Preserve, 144, 96)]
        [InlineData("png", ExportDpiPreset.Custom, 600, 600)]
        [InlineData("jpg", ExportDpiPreset.Custom, 600, 600)]
        public void ResizeEffect_ExportRetainsPreserveOrCustomDpiAfterExactPixelResize(string format, ExportDpiPreset preset,
            float expectedHorizontalDpi, float expectedVerticalDpi)
        {
            using var source = CreateSource(41, 41);
            var settings = new SurfaceOutputSettings(format, 90, false)
            {
                DisableReduceColors = true, ExportDpiPreset = preset, CustomDpi = 600
            };
            settings.Effects.Add(new ResizeEffect(2, 2, false));
            bool ownsOutput = ImageIO.CreateImageForOutput(source, false, settings, out Image output);
            Assert.True(ownsOutput);
            using (output)
            using (var stream = new MemoryStream())
            {
                Assert.Equal(new Size(2, 2), output.Size);
                AssertDpi(output, expectedHorizontalDpi, expectedVerticalDpi);
                ImageIO.SaveToStream(output, null, stream, settings);
                stream.Position = 0;
                using var decoded = new Bitmap(stream);
                Assert.Equal(new Size(2, 2), decoded.Size);
                AssertDpi(decoded, expectedHorizontalDpi, expectedVerticalDpi, 0.05f);
                if (format == "png")
                {
                    Assert.Equal(Color.Red.ToArgb(), decoded.GetPixel(0, 0).ToArgb());
                }
            }
            AssertSourceUnchanged(source);
        }

        private static Bitmap CreateSource(int width, int height)
        {
            var source = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            source.SetResolution(144, 96);
            using Graphics graphics = Graphics.FromImage(source);
            graphics.Clear(Color.Red);
            return source;
        }

        private static void AssertSourceUnchanged(Bitmap source)
        {
            AssertDpi(source, 144, 96);
            Assert.Equal(Color.Red.ToArgb(), source.GetPixel(0, 0).ToArgb());
        }

        private static void AssertDpi(Image image, float horizontalDpi, float verticalDpi, float tolerance = 0.001f)
        {
            Assert.InRange(image.HorizontalResolution, horizontalDpi - tolerance, horizontalDpi + tolerance);
            Assert.InRange(image.VerticalResolution, verticalDpi - tolerance, verticalDpi + tolerance);
        }

        private static void AssertMatrixScale(Matrix matrix, float horizontalScale, float verticalScale)
        {
            float[] elements = matrix.Elements;
            Assert.Equal(horizontalScale, elements[0]);
            Assert.Equal(verticalScale, elements[3]);
            Assert.Equal(0, elements[1]);
            Assert.Equal(0, elements[2]);
            Assert.Equal(0, elements[4]);
            Assert.Equal(0, elements[5]);
        }
    }
}
