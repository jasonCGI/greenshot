/*
 * Greenshot - a free and open source screenshot tool
 * Copyright (C) 2007-2026 Thomas Braun, Jens Klingen, Robin Krom
 *
 * For more information see: https://getgreenshot.org/
 * The Greenshot project is hosted on GitHub https://github.com/greenshot/greenshot
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
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
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dapplo.Ini;
using Dapplo.Ini.Parsing;
using Greenshot.Base.Core;
using Greenshot.Base.Core.Enums;
using Greenshot.Base.Core.Export;
using Greenshot.Base.Core.FileFormat;
using Greenshot.Base.Interfaces;
using Greenshot.Base.Interfaces.Plugin;
using Greenshot.Editor.Drawing;
using Greenshot.Tests.Threading;
using SixLabors.ImageSharp.Metadata;
using Xunit;

namespace Greenshot.Tests.Core
{
    [Collection("Export DPI configuration")]
    public class ExportDpiTests
    {
        private const float SourceHorizontalDpi = 144;
        private const float SourceVerticalDpi = 96;

        public ExportDpiTests()
        {
            TestEnvironment.EnsureInitialized();
        }

        [Theory]
        [InlineData(ExportDpiPreset.Preserve)]
        [InlineData(ExportDpiPreset.Web)]
        [InlineData(ExportDpiPreset.Print)]
        [InlineData(ExportDpiPreset.Custom)]
        public void SurfaceOutputSettings_AllConstructors_InheritConfiguredPreset(ExportDpiPreset preset)
        {
            var configuration = IniConfigRegistry.GetSection<ICoreConfiguration>();
            ExportDpiPreset originalPreset = configuration.OutputFileDpiPreset;
            int originalCustomDpi = configuration.OutputFileCustomDpi;
            try
            {
                configuration.OutputFileDpiPreset = preset;
                configuration.OutputFileCustomDpi = 600;
                var settings = new[]
                {
                    new SurfaceOutputSettings(),
                    new SurfaceOutputSettings(WellKnownFileFormats.Png),
                    new SurfaceOutputSettings(WellKnownFileFormats.Jpg, 90),
                    new SurfaceOutputSettings(WellKnownFileFormats.Png, 90, true)
                };

                foreach (var outputSettings in settings)
                {
                    Assert.Equal(preset, outputSettings.ExportDpiPreset);
                    Assert.Equal(600, outputSettings.CustomDpi);
                }
            }
            finally
            {
                configuration.OutputFileDpiPreset = originalPreset;
                configuration.OutputFileCustomDpi = originalCustomDpi;
            }
        }

        [Fact]
        public void OutputFileDpiPreset_DeclaredDefault_IsPreserve()
        {
            PropertyInfo property = typeof(ICoreConfiguration).GetProperty(nameof(ICoreConfiguration.OutputFileDpiPreset));
            Assert.NotNull(property);
            var defaultValue = property.GetCustomAttribute<DefaultValueAttribute>();
            Assert.NotNull(defaultValue);
            Assert.Equal(nameof(ExportDpiPreset.Preserve), defaultValue.Value?.ToString());
        }

        [Theory]
        [InlineData(ExportDpiPreset.Preserve)]
        [InlineData(ExportDpiPreset.Web)]
        [InlineData(ExportDpiPreset.Print)]
        [InlineData(ExportDpiPreset.Custom)]
        public void OutputFileDpiPreset_IniRoundTrip_PreservesConfiguredValue(ExportDpiPreset preset)
        {
            var configuration = new CoreConfigurationImpl();
            configuration.ResetToDefaults();
            configuration.OutputFileDpiPreset = preset;
            string key = nameof(ICoreConfiguration.OutputFileDpiPreset);
            string rawValue = configuration.GetAllRawValues().Single(entry => entry.Key == key).Value;
            Assert.Equal(preset.ToString(), rawValue);

            var ini = new IniFile();
            ini.GetOrAddSection(configuration.SectionName).SetValue(key, rawValue);
            string serialized = IniFileWriter.WriteToString(ini, new IniWriterOptions());
            IniFile parsed = IniFileParser.Parse(serialized, new IniParserOptions());
            var reloaded = new CoreConfigurationImpl();
            reloaded.ResetToDefaults();
            reloaded.SetRawValue(key, parsed.GetSection(configuration.SectionName).GetValue(key));

            Assert.Equal(preset, reloaded.OutputFileDpiPreset);
        }

        [Fact]
        public void OutputFileCustomDpi_DeclaredAndResetDefaults_Are96()
        {
            PropertyInfo property = typeof(ICoreConfiguration).GetProperty(nameof(ICoreConfiguration.OutputFileCustomDpi));
            Assert.NotNull(property);
            var defaultValue = property.GetCustomAttribute<DefaultValueAttribute>();
            Assert.NotNull(defaultValue);
            Assert.Equal(96, defaultValue.Value);

            var configuration = new CoreConfigurationImpl();
            configuration.ResetToDefaults();
            Assert.Equal(96, configuration.OutputFileCustomDpi);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(96)]
        [InlineData(144)]
        [InlineData(600)]
        [InlineData(2400)]
        public void OutputFileCustomDpi_IniRoundTrip_PreservesConfiguredValue(int customDpi)
        {
            var configuration = new CoreConfigurationImpl();
            configuration.ResetToDefaults();
            configuration.OutputFileDpiPreset = ExportDpiPreset.Custom;
            configuration.OutputFileCustomDpi = customDpi;
            string presetKey = nameof(ICoreConfiguration.OutputFileDpiPreset);
            string customKey = nameof(ICoreConfiguration.OutputFileCustomDpi);
            var ini = new IniFile();
            IniSection section = ini.GetOrAddSection(configuration.SectionName);
            foreach (var entry in configuration.GetAllRawValues().Where(entry => entry.Key == presetKey || entry.Key == customKey))
            {
                section.SetValue(entry.Key, entry.Value);
            }

            string serialized = IniFileWriter.WriteToString(ini, new IniWriterOptions());
            IniFile parsed = IniFileParser.Parse(serialized, new IniParserOptions());
            var reloaded = new CoreConfigurationImpl();
            reloaded.ResetToDefaults();
            reloaded.SetRawValue(presetKey, parsed.GetSection(configuration.SectionName).GetValue(presetKey));
            reloaded.SetRawValue(customKey, parsed.GetSection(configuration.SectionName).GetValue(customKey));

            Assert.Equal(ExportDpiPreset.Custom, reloaded.OutputFileDpiPreset);
            Assert.Equal(customDpi, reloaded.OutputFileCustomDpi);
        }

        [Fact]
        public void OutputFileCustomDpi_IniWithoutCustomValue_KeepsDefault96()
        {
            IniFile parsed = IniFileParser.Parse("[Core]\nOutputFileDpiPreset=Custom\n", new IniParserOptions());
            var configuration = new CoreConfigurationImpl();
            configuration.ResetToDefaults();
            configuration.SetRawValue(nameof(ICoreConfiguration.OutputFileDpiPreset), parsed.GetSection("Core").GetValue(nameof(ICoreConfiguration.OutputFileDpiPreset)));

            Assert.Equal(ExportDpiPreset.Custom, configuration.OutputFileDpiPreset);
            Assert.Equal(96, configuration.OutputFileCustomDpi);
        }

        [Fact]
        public void ExportDpiSettings_CustomDpiBounds_Are1Through2400()
        {
            Assert.Equal(3, (int)ExportDpiPreset.Custom);
            Assert.Equal(1, ExportDpiSettings.MinimumCustomDpi);
            Assert.Equal(2400, ExportDpiSettings.MaximumCustomDpi);
        }

        [Theory]
        [InlineData(1, true)]
        [InlineData(96, true)]
        [InlineData(144, true)]
        [InlineData(600, true)]
        [InlineData(2400, true)]
        [InlineData(0, false)]
        [InlineData(-1, false)]
        [InlineData(2401, false)]
        [InlineData(int.MinValue, false)]
        [InlineData(int.MaxValue, false)]
        public void ExportDpiSettings_IsValidCustomDpi_EnforcesInclusiveBounds(int customDpi, bool valid)
        {
            Assert.Equal(valid, ExportDpiSettings.IsValidCustomDpi(customDpi));
        }

        [Theory]
        [InlineData("1", 1)]
        [InlineData("96", 96)]
        [InlineData("144", 144)]
        [InlineData("600", 600)]
        [InlineData("2400", 2400)]
        [InlineData(" 600 ", 600)]
        [InlineData("\t96\r\n", 96)]
        public void ExportDpiSettings_TryParseCustomDpi_AcceptsDigitsAndSurroundingWhitespace(string text, int expectedDpi)
        {
            Assert.True(ExportDpiSettings.TryParseCustomDpi(text, out int customDpi));
            Assert.Equal(expectedDpi, customDpi);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("0")]
        [InlineData("-1")]
        [InlineData("+96")]
        [InlineData("2401")]
        [InlineData("96.5")]
        [InlineData("96.0")]
        [InlineData("9.6e1")]
        [InlineData("1,200")]
        [InlineData("1 200")]
        [InlineData("invalid")]
        [InlineData("2147483648")]
        public void ExportDpiSettings_TryParseCustomDpi_RejectsInvalidInputAndClearsResult(string text)
        {
            Assert.False(ExportDpiSettings.TryParseCustomDpi(text, out int customDpi));
            Assert.Equal(0, customDpi);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(96)]
        [InlineData(144)]
        [InlineData(600)]
        [InlineData(2400)]
        public void ExportDpiSettings_GetResolution_Custom_ReturnsValidatedValue(int customDpi)
        {
            Assert.Equal((float?)customDpi, ExportDpiSettings.GetResolution(ExportDpiPreset.Custom, customDpi));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(2401)]
        [InlineData(int.MaxValue)]
        public void ExportDpiSettings_GetResolution_Custom_RejectsInvalidValue(int customDpi)
        {
            ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
                ExportDpiSettings.GetResolution(ExportDpiPreset.Custom, customDpi));

            Assert.Equal(nameof(SurfaceOutputSettings.CustomDpi), exception.ParamName);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(2401)]
        [InlineData(int.MaxValue)]
        public void ExportDpiSettings_GetResolution_NonCustom_IgnoresInvalidCustomValue(int customDpi)
        {
            Assert.Null(ExportDpiSettings.GetResolution(ExportDpiPreset.Preserve, customDpi));
            Assert.Equal((float?)72, ExportDpiSettings.GetResolution(ExportDpiPreset.Web, customDpi));
            Assert.Equal((float?)300, ExportDpiSettings.GetResolution(ExportDpiPreset.Print, customDpi));
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(999)]
        public void ExportDpiSettings_GetResolution_InvalidPreset_IsRejected(int invalidPreset)
        {
            ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
                ExportDpiSettings.GetResolution((ExportDpiPreset)invalidPreset, 96));

            Assert.Equal(nameof(SurfaceOutputSettings.ExportDpiPreset), exception.ParamName);
        }

        [Theory]
        [InlineData(WellKnownFileFormats.Bmp)]
        [InlineData(WellKnownFileFormats.Gif)]
        [InlineData(WellKnownFileFormats.Tiff)]
        [InlineData(WellKnownFileFormats.Greenshot)]
        public void ExportDpiSettings_GetResolution_UnsupportedFormats_IgnoreCustomDpi(string format)
        {
            var settings = CreateCustomSettings(format, 0);

            Assert.Null(ExportDpiSettings.GetResolution(settings));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void CreateImageForOutput_Preserve_KeepsSourceAndOwnership(bool ownsSourceImage)
        {
            using var source = CreatePatternBitmap();
            int[] originalPixels = ReadPixels(source);
            var settings = CreateSettings(WellKnownFileFormats.Png, ExportDpiPreset.Preserve);

            bool disposeImage = ImageIO.CreateImageForOutput(source, ownsSourceImage, settings, out Image output);
            try
            {
                Assert.Same(source, output);
                Assert.Equal(ownsSourceImage, disposeImage);
                AssertResolution(output, SourceHorizontalDpi, SourceVerticalDpi);
                Assert.Equal(originalPixels, ReadPixels((Bitmap)output));
            }
            finally
            {
                DisposeReplacement(source, output, disposeImage);
            }
        }

        [Theory]
        [InlineData(WellKnownFileFormats.Png, ExportDpiPreset.Web, 72, false)]
        [InlineData(WellKnownFileFormats.Png, ExportDpiPreset.Print, 300, false)]
        [InlineData(WellKnownFileFormats.Jpg, ExportDpiPreset.Web, 72, false)]
        [InlineData(WellKnownFileFormats.Jpg, ExportDpiPreset.Print, 300, false)]
        [InlineData(WellKnownFileFormats.Png, ExportDpiPreset.Web, 72, true)]
        [InlineData(WellKnownFileFormats.Png, ExportDpiPreset.Print, 300, true)]
        [InlineData(WellKnownFileFormats.Jpg, ExportDpiPreset.Web, 72, true)]
        [InlineData(WellKnownFileFormats.Jpg, ExportDpiPreset.Print, 300, true)]
        public void CreateImageForOutput_Preset_ChangesOnlyResolution(string format, ExportDpiPreset preset, float expectedDpi, bool ownsSourceImage)
        {
            using var source = CreatePatternBitmap();
            Size originalSize = source.Size;
            int[] originalPixels = ReadPixels(source);
            var settings = CreateSettings(format, preset);

            bool disposeImage = ImageIO.CreateImageForOutput(source, ownsSourceImage, settings, out Image output);
            try
            {
                Assert.True(disposeImage);
                Assert.Equal(originalSize, output.Size);
                AssertResolution(output, expectedDpi, expectedDpi);
                Assert.Equal(originalPixels, ReadPixels((Bitmap)output));
                if (!ownsSourceImage)
                {
                    Assert.NotSame(source, output);
                    AssertResolution(source, SourceHorizontalDpi, SourceVerticalDpi);
                    Assert.Equal(originalPixels, ReadPixels(source));
                }
            }
            finally
            {
                DisposeReplacement(source, output, disposeImage);
            }
        }

        [Theory]
        [InlineData(ExportDpiPreset.Web, 72, false)]
        [InlineData(ExportDpiPreset.Print, 300, false)]
        [InlineData(ExportDpiPreset.Web, 72, true)]
        [InlineData(ExportDpiPreset.Print, 300, true)]
        public void CreateImageForOutput_AlreadyAtPreset_KeepsSourceAndOwnership(ExportDpiPreset preset, float dpi, bool ownsSourceImage)
        {
            using var source = CreatePatternBitmap();
            source.SetResolution(dpi, dpi);
            var settings = CreateSettings(WellKnownFileFormats.Png, preset);

            bool disposeImage = ImageIO.CreateImageForOutput(source, ownsSourceImage, settings, out Image output);
            try
            {
                Assert.Same(source, output);
                Assert.Equal(ownsSourceImage, disposeImage);
                AssertResolution(output, dpi, dpi);
            }
            finally
            {
                DisposeReplacement(source, output, disposeImage);
            }
        }

        [Theory]
        [InlineData(WellKnownFileFormats.Png, 1, false)]
        [InlineData(WellKnownFileFormats.Png, 96, false)]
        [InlineData(WellKnownFileFormats.Png, 144, false)]
        [InlineData(WellKnownFileFormats.Png, 600, false)]
        [InlineData(WellKnownFileFormats.Png, 2400, false)]
        [InlineData(WellKnownFileFormats.Jpg, 1, false)]
        [InlineData(WellKnownFileFormats.Jpg, 96, false)]
        [InlineData(WellKnownFileFormats.Jpg, 144, false)]
        [InlineData(WellKnownFileFormats.Jpg, 600, false)]
        [InlineData(WellKnownFileFormats.Jpg, 2400, false)]
        [InlineData(WellKnownFileFormats.Png, 1, true)]
        [InlineData(WellKnownFileFormats.Png, 96, true)]
        [InlineData(WellKnownFileFormats.Png, 144, true)]
        [InlineData(WellKnownFileFormats.Png, 600, true)]
        [InlineData(WellKnownFileFormats.Png, 2400, true)]
        [InlineData(WellKnownFileFormats.Jpg, 1, true)]
        [InlineData(WellKnownFileFormats.Jpg, 96, true)]
        [InlineData(WellKnownFileFormats.Jpg, 144, true)]
        [InlineData(WellKnownFileFormats.Jpg, 600, true)]
        [InlineData(WellKnownFileFormats.Jpg, 2400, true)]
        public void CreateImageForOutput_CustomDpi_ChangesOnlyResolution(string format, int customDpi, bool ownsSourceImage)
        {
            using var source = CreatePatternBitmap();
            Size originalSize = source.Size;
            int[] originalPixels = ReadPixels(source);
            var settings = CreateCustomSettings(format, customDpi);

            bool disposeImage = ImageIO.CreateImageForOutput(source, ownsSourceImage, settings, out Image output);
            try
            {
                Assert.True(disposeImage);
                Assert.Equal(originalSize, output.Size);
                AssertResolution(output, customDpi, customDpi);
                Assert.Equal(originalPixels, ReadPixels((Bitmap)output));
                if (!ownsSourceImage)
                {
                    Assert.NotSame(source, output);
                    AssertResolution(source, SourceHorizontalDpi, SourceVerticalDpi);
                    Assert.Equal(originalPixels, ReadPixels(source));
                }
            }
            finally
            {
                DisposeReplacement(source, output, disposeImage);
            }
        }

        [Theory]
        [InlineData(96, false)]
        [InlineData(600, false)]
        [InlineData(96, true)]
        [InlineData(600, true)]
        public void CreateImageForOutput_AlreadyAtCustomDpi_KeepsSourceAndOwnership(int customDpi, bool ownsSourceImage)
        {
            using var source = CreatePatternBitmap();
            source.SetResolution(customDpi, customDpi);
            var settings = CreateCustomSettings(WellKnownFileFormats.Png, customDpi);

            bool disposeImage = ImageIO.CreateImageForOutput(source, ownsSourceImage, settings, out Image output);
            try
            {
                Assert.Same(source, output);
                Assert.Equal(ownsSourceImage, disposeImage);
                AssertResolution(output, customDpi, customDpi);
            }
            finally
            {
                DisposeReplacement(source, output, disposeImage);
            }
        }

        [Theory]
        [InlineData(0, false)]
        [InlineData(-1, false)]
        [InlineData(2401, false)]
        [InlineData(0, true)]
        [InlineData(-1, true)]
        [InlineData(2401, true)]
        public void CreateImageForOutput_InvalidCustomDpi_RejectsBeforeColorReduction(int customDpi, bool ownsSourceImage)
        {
            using var source = CreateTwoColorBitmap();
            int[] originalPixels = ReadPixels(source);
            var settings = CreateCustomSettings(WellKnownFileFormats.Png, customDpi);
            settings.DisableReduceColors = false;
            settings.ReduceColors = true;

            ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
                ImageIO.CreateImageForOutput(source, ownsSourceImage, settings, out _));

            Assert.Equal(nameof(SurfaceOutputSettings.CustomDpi), exception.ParamName);
            AssertResolution(source, SourceHorizontalDpi, SourceVerticalDpi);
            Assert.Equal(originalPixels, ReadPixels(source));
        }

        [Theory]
        [InlineData(WellKnownFileFormats.Png, -1, false)]
        [InlineData(WellKnownFileFormats.Png, 999, false)]
        [InlineData(WellKnownFileFormats.Jpg, -1, false)]
        [InlineData(WellKnownFileFormats.Jpg, 999, false)]
        [InlineData(WellKnownFileFormats.Png, -1, true)]
        [InlineData(WellKnownFileFormats.Png, 999, true)]
        [InlineData(WellKnownFileFormats.Jpg, -1, true)]
        [InlineData(WellKnownFileFormats.Jpg, 999, true)]
        public void CreateImageForOutput_InvalidPreset_RejectsWithoutChangingSource(string format, int invalidPreset, bool ownsSourceImage)
        {
            using var source = CreatePatternBitmap();
            int[] originalPixels = ReadPixels(source);
            var settings = CreateSettings(format, (ExportDpiPreset)invalidPreset);

            ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
                ImageIO.CreateImageForOutput(source, ownsSourceImage, settings, out _));

            Assert.Equal(nameof(SurfaceOutputSettings.ExportDpiPreset), exception.ParamName);
            AssertResolution(source, SourceHorizontalDpi, SourceVerticalDpi);
            Assert.Equal(originalPixels, ReadPixels(source));
        }

        [Theory]
        [InlineData(ExportDpiPreset.Preserve, SourceHorizontalDpi, SourceVerticalDpi, false)]
        [InlineData(ExportDpiPreset.Web, 72, 72, false)]
        [InlineData(ExportDpiPreset.Print, 300, 300, false)]
        [InlineData(ExportDpiPreset.Preserve, SourceHorizontalDpi, SourceVerticalDpi, true)]
        [InlineData(ExportDpiPreset.Web, 72, 72, true)]
        [InlineData(ExportDpiPreset.Print, 300, 300, true)]
        public void CreateImageForOutput_ColorReduction_RetainsRequestedResolution(ExportDpiPreset preset, float expectedHorizontalDpi, float expectedVerticalDpi, bool ownsSourceImage)
        {
            using var source = CreateTwoColorBitmap();
            Size originalSize = source.Size;
            int[] originalPixels = ReadPixels(source);
            var settings = new SurfaceOutputSettings(WellKnownFileFormats.Png, 90, true)
            {
                ExportDpiPreset = preset
            };

            bool disposeImage = ImageIO.CreateImageForOutput(source, ownsSourceImage, settings, out Image output);
            try
            {
                Assert.True(disposeImage);
                Assert.NotSame(source, output);
                Assert.Equal(PixelFormat.Format8bppIndexed, output.PixelFormat);
                Assert.Equal(originalSize, output.Size);
                AssertResolution(output, expectedHorizontalDpi, expectedVerticalDpi);
                Assert.Equal(originalPixels, ReadPixels((Bitmap)output));
                if (!ownsSourceImage)
                {
                    AssertResolution(source, SourceHorizontalDpi, SourceVerticalDpi);
                    Assert.Equal(originalPixels, ReadPixels(source));
                }
            }
            finally
            {
                DisposeReplacement(source, output, disposeImage);
            }
        }

        [Theory]
        [InlineData(WellKnownFileFormats.Png, 1)]
        [InlineData(WellKnownFileFormats.Png, 96)]
        [InlineData(WellKnownFileFormats.Png, 144)]
        [InlineData(WellKnownFileFormats.Png, 600)]
        [InlineData(WellKnownFileFormats.Png, 2400)]
        [InlineData(WellKnownFileFormats.Jpg, 1)]
        [InlineData(WellKnownFileFormats.Jpg, 96)]
        [InlineData(WellKnownFileFormats.Jpg, 144)]
        [InlineData(WellKnownFileFormats.Jpg, 600)]
        [InlineData(WellKnownFileFormats.Jpg, 2400)]
        public void CreateImageForOutput_ColorReduction_EncodesCustomDpi(string format, int customDpi)
        {
            using var source = CreateTwoColorBitmap();
            int[] originalPixels = ReadPixels(source);
            var settings = CreateCustomSettings(format, customDpi);
            settings.DisableReduceColors = false;
            settings.ReduceColors = true;

            bool disposeImage = ImageIO.CreateImageForOutput(source, false, settings, out Image output);
            try
            {
                Assert.True(disposeImage);
                Assert.NotSame(source, output);
                Assert.Equal(PixelFormat.Format8bppIndexed, output.PixelFormat);
                Assert.Equal(source.Size, output.Size);
                AssertResolution(output, customDpi, customDpi);
                Assert.Equal(originalPixels, ReadPixels((Bitmap)output));
                using var stream = new MemoryStream();
                ImageIO.SaveToStream(output, null, stream, settings);
                stream.Position = 0;
                using var decoded = new Bitmap(stream);
                Assert.Equal(source.Size, decoded.Size);
                AssertResolution(decoded, customDpi, customDpi, 0.05f);
                if (format == WellKnownFileFormats.Png)
                {
                    Assert.Equal(originalPixels, ReadPixels(decoded));
                    AssertPngResolution(stream.ToArray(), customDpi, customDpi);
                }
                else
                {
                    AssertJpegResolution(stream.ToArray(), customDpi, customDpi);
                }

                AssertResolution(source, SourceHorizontalDpi, SourceVerticalDpi);
                Assert.Equal(originalPixels, ReadPixels(source));
            }
            finally
            {
                DisposeReplacement(source, output, disposeImage);
            }
        }

        [Theory]
        [InlineData(ExportDpiPreset.Web, 72)]
        [InlineData(ExportDpiPreset.Print, 300)]
        public void CreateImageForOutput_AutomaticReductionSkipped_AppliesPreset(ExportDpiPreset preset, float expectedDpi)
        {
            var configuration = IniConfigRegistry.GetSection<ICoreConfiguration>();
            bool originalAutoReduceColors = configuration.OutputFileAutoReduceColors;
            using var source = CreatePatternBitmap(PixelFormat.Format24bppRgb, 32, 16);
            int[] originalPixels = ReadPixels(source);
            var settings = CreateSettings(WellKnownFileFormats.Png, preset);
            settings.DisableReduceColors = false;
            Image output = null;
            bool disposeImage = false;

            try
            {
                // This changes only the in-memory test configuration and is restored below.
                configuration.OutputFileAutoReduceColors = true;
                disposeImage = ImageIO.CreateImageForOutput(source, false, settings, out output);

                Assert.True(disposeImage);
                Assert.NotSame(source, output);
                Assert.Equal(PixelFormat.Format24bppRgb, output.PixelFormat);
                Assert.Equal(source.Size, output.Size);
                AssertResolution(output, expectedDpi, expectedDpi);
                Assert.Equal(originalPixels, ReadPixels((Bitmap)output));
                AssertResolution(source, SourceHorizontalDpi, SourceVerticalDpi);
                Assert.Equal(originalPixels, ReadPixels(source));
            }
            finally
            {
                configuration.OutputFileAutoReduceColors = originalAutoReduceColors;
                DisposeReplacement(source, output, disposeImage);
            }
        }

        [Theory]
        [InlineData(ExportDpiPreset.Web)]
        [InlineData(ExportDpiPreset.Print)]
        public void CreateImageForOutput_Gif_IgnoresPresetAndRetainsSourceResolution(ExportDpiPreset preset)
        {
            using var source = CreateTwoColorBitmap();
            int[] originalPixels = ReadPixels(source);
            var settings = new SurfaceOutputSettings(WellKnownFileFormats.Gif)
            {
                ExportDpiPreset = preset
            };

            bool disposeImage = ImageIO.CreateImageForOutput(source, false, settings, out Image output);
            try
            {
                Assert.True(disposeImage);
                Assert.Equal(source.Size, output.Size);
                AssertResolution(output, SourceHorizontalDpi, SourceVerticalDpi);
                AssertResolution(source, SourceHorizontalDpi, SourceVerticalDpi);
                Assert.Equal(originalPixels, ReadPixels(source));
            }
            finally
            {
                DisposeReplacement(source, output, disposeImage);
            }
        }

        [Theory]
        [InlineData(WellKnownFileFormats.Png, ExportDpiPreset.Preserve, SourceHorizontalDpi, SourceVerticalDpi)]
        [InlineData(WellKnownFileFormats.Png, ExportDpiPreset.Web, 72, 72)]
        [InlineData(WellKnownFileFormats.Png, ExportDpiPreset.Print, 300, 300)]
        [InlineData(WellKnownFileFormats.Jpg, ExportDpiPreset.Preserve, SourceHorizontalDpi, SourceVerticalDpi)]
        [InlineData(WellKnownFileFormats.Jpg, ExportDpiPreset.Web, 72, 72)]
        [InlineData(WellKnownFileFormats.Jpg, ExportDpiPreset.Print, 300, 300)]
        public void SaveToStream_RoundTrip_WritesDpiWithoutChangingSource(string format, ExportDpiPreset preset, float expectedHorizontalDpi, float expectedVerticalDpi)
        {
            PixelFormat pixelFormat = format == WellKnownFileFormats.Png ? PixelFormat.Format32bppArgb : PixelFormat.Format24bppRgb;
            using var source = CreatePatternBitmap(pixelFormat);
            int[] originalPixels = ReadPixels(source);
            using var stream = new MemoryStream();
            var settings = CreateSettings(format, preset);

            ImageIO.SaveToStream(source, null, stream, settings);

            AssertResolution(source, SourceHorizontalDpi, SourceVerticalDpi);
            Assert.Equal(originalPixels, ReadPixels(source));
            stream.Position = 0;
            using var decoded = new Bitmap(stream);
            Assert.Equal(source.Size, decoded.Size);
            AssertResolution(decoded, expectedHorizontalDpi, expectedVerticalDpi, 0.05f);
            if (format == WellKnownFileFormats.Png)
            {
                Assert.Equal(originalPixels, ReadPixels(decoded));
                AssertPngResolution(stream.ToArray(), expectedHorizontalDpi, expectedVerticalDpi);
            }
            else
            {
                AssertJpegResolution(stream.ToArray(), expectedHorizontalDpi, expectedVerticalDpi);
            }
        }

        [Theory]
        [InlineData(WellKnownFileFormats.Png, 1)]
        [InlineData(WellKnownFileFormats.Png, 96)]
        [InlineData(WellKnownFileFormats.Png, 144)]
        [InlineData(WellKnownFileFormats.Png, 600)]
        [InlineData(WellKnownFileFormats.Png, 2400)]
        [InlineData(WellKnownFileFormats.Jpg, 1)]
        [InlineData(WellKnownFileFormats.Jpg, 96)]
        [InlineData(WellKnownFileFormats.Jpg, 144)]
        [InlineData(WellKnownFileFormats.Jpg, 600)]
        [InlineData(WellKnownFileFormats.Jpg, 2400)]
        public void SaveToStream_CustomDpi_WritesMetadataWithoutChangingPixelsOrSource(string format, int customDpi)
        {
            PixelFormat pixelFormat = format == WellKnownFileFormats.Png ? PixelFormat.Format32bppArgb : PixelFormat.Format24bppRgb;
            using var source = CreatePatternBitmap(pixelFormat);
            int[] originalPixels = ReadPixels(source);
            using var stream = new MemoryStream();

            ImageIO.SaveToStream(source, null, stream, CreateCustomSettings(format, customDpi));

            AssertResolution(source, SourceHorizontalDpi, SourceVerticalDpi);
            Assert.Equal(originalPixels, ReadPixels(source));
            stream.Position = 0;
            using var decoded = new Bitmap(stream);
            Assert.Equal(source.Size, decoded.Size);
            AssertResolution(decoded, customDpi, customDpi, 0.05f);
            if (format == WellKnownFileFormats.Png)
            {
                Assert.Equal(originalPixels, ReadPixels(decoded));
                AssertPngResolution(stream.ToArray(), customDpi, customDpi);
            }
            else
            {
                AssertJpegResolution(stream.ToArray(), customDpi, customDpi);
            }
        }

        [Theory]
        [InlineData(WellKnownFileFormats.Png, 0)]
        [InlineData(WellKnownFileFormats.Png, -1)]
        [InlineData(WellKnownFileFormats.Png, 2401)]
        [InlineData(WellKnownFileFormats.Png, int.MaxValue)]
        [InlineData(WellKnownFileFormats.Jpg, 0)]
        [InlineData(WellKnownFileFormats.Jpg, -1)]
        [InlineData(WellKnownFileFormats.Jpg, 2401)]
        [InlineData(WellKnownFileFormats.Jpg, int.MaxValue)]
        public void SaveToStream_InvalidCustomDpi_RejectsBeforeWritingAndKeepsCallerImage(string format, int customDpi)
        {
            using var source = CreatePatternBitmap();
            int[] originalPixels = ReadPixels(source);
            using var stream = new MemoryStream();
            var settings = CreateCustomSettings(format, customDpi);

            ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
                ImageIO.SaveToStream(source, null, stream, settings));

            Assert.Equal(nameof(SurfaceOutputSettings.CustomDpi), exception.ParamName);
            Assert.Equal(0, stream.Length);
            AssertResolution(source, SourceHorizontalDpi, SourceVerticalDpi);
            Assert.Equal(originalPixels, ReadPixels(source));
        }

        [Theory]
        [InlineData(WellKnownFileFormats.Png, ExportDpiPreset.Preserve, SourceHorizontalDpi, SourceVerticalDpi)]
        [InlineData(WellKnownFileFormats.Png, ExportDpiPreset.Web, 72, 72)]
        [InlineData(WellKnownFileFormats.Png, ExportDpiPreset.Print, 300, 300)]
        [InlineData(WellKnownFileFormats.Jpg, ExportDpiPreset.Preserve, SourceHorizontalDpi, SourceVerticalDpi)]
        [InlineData(WellKnownFileFormats.Jpg, ExportDpiPreset.Web, 72, 72)]
        [InlineData(WellKnownFileFormats.Jpg, ExportDpiPreset.Print, 300, 300)]
        public void SaveToStream_NonCustomPresets_IgnoreInvalidCustomDpi(string format, ExportDpiPreset preset, float expectedHorizontalDpi, float expectedVerticalDpi)
        {
            PixelFormat pixelFormat = format == WellKnownFileFormats.Png ? PixelFormat.Format32bppArgb : PixelFormat.Format24bppRgb;
            using var source = CreatePatternBitmap(pixelFormat);
            int[] originalPixels = ReadPixels(source);
            foreach (int invalidCustomDpi in new[] { 0, -1, 2401, int.MaxValue })
            {
                var settings = CreateSettings(format, preset);
                settings.CustomDpi = invalidCustomDpi;
                using var stream = new MemoryStream();

                ImageIO.SaveToStream(source, null, stream, settings);

                stream.Position = 0;
                using var decoded = new Bitmap(stream);
                Assert.Equal(source.Size, decoded.Size);
                AssertResolution(decoded, expectedHorizontalDpi, expectedVerticalDpi, 0.05f);
                if (format == WellKnownFileFormats.Png)
                {
                    Assert.Equal(originalPixels, ReadPixels(decoded));
                    AssertPngResolution(stream.ToArray(), expectedHorizontalDpi, expectedVerticalDpi);
                }
                else
                {
                    AssertJpegResolution(stream.ToArray(), expectedHorizontalDpi, expectedVerticalDpi);
                }

                AssertResolution(source, SourceHorizontalDpi, SourceVerticalDpi);
                Assert.Equal(originalPixels, ReadPixels(source));
            }
        }

        [Theory]
        [InlineData(WellKnownFileFormats.Png, WellKnownFileFormats.Png, ExportDpiPreset.Preserve)]
        [InlineData(WellKnownFileFormats.Png, WellKnownFileFormats.Png, ExportDpiPreset.Web)]
        [InlineData(WellKnownFileFormats.Png, WellKnownFileFormats.Png, ExportDpiPreset.Print)]
        [InlineData(WellKnownFileFormats.Jpg, WellKnownFileFormats.Jpg, ExportDpiPreset.Preserve)]
        [InlineData(WellKnownFileFormats.Jpg, WellKnownFileFormats.Jpg, ExportDpiPreset.Web)]
        [InlineData(WellKnownFileFormats.Jpg, WellKnownFileFormats.Jpg, ExportDpiPreset.Print)]
        [InlineData(WellKnownFileFormats.Png, WellKnownFileFormats.Jpg, ExportDpiPreset.Preserve)]
        [InlineData(WellKnownFileFormats.Png, WellKnownFileFormats.Jpg, ExportDpiPreset.Web)]
        [InlineData(WellKnownFileFormats.Png, WellKnownFileFormats.Jpg, ExportDpiPreset.Print)]
        [InlineData(WellKnownFileFormats.Jpg, WellKnownFileFormats.Png, ExportDpiPreset.Preserve)]
        [InlineData(WellKnownFileFormats.Jpg, WellKnownFileFormats.Png, ExportDpiPreset.Web)]
        [InlineData(WellKnownFileFormats.Jpg, WellKnownFileFormats.Png, ExportDpiPreset.Print)]
        public void SaveToStream_OpenedImage_UpdatesExportMetadataAndKeepsSource(string inputFormat, string outputFormat, ExportDpiPreset preset)
        {
            PixelFormat pixelFormat = inputFormat == WellKnownFileFormats.Png ? PixelFormat.Format32bppArgb : PixelFormat.Format24bppRgb;
            using var original = CreatePatternBitmap(pixelFormat);
            using var inputStream = new MemoryStream();
            original.Save(inputStream, inputFormat == WellKnownFileFormats.Png ? ImageFormat.Png : ImageFormat.Jpeg);
            inputStream.Position = 0;
            using var opened = new Bitmap(inputStream);
            float originalHorizontalDpi = opened.HorizontalResolution;
            float originalVerticalDpi = opened.VerticalResolution;
            int[] originalPixels = ReadPixels(opened);
            string[] originalResolutionProperties = ReadResolutionProperties(opened);
            using var outputStream = new MemoryStream();
            var settings = CreateSettings(outputFormat, preset);

            ImageIO.SaveToStream(opened, null, outputStream, settings);

            AssertResolution(opened, originalHorizontalDpi, originalVerticalDpi);
            Assert.Equal(originalPixels, ReadPixels(opened));
            Assert.Equal(originalResolutionProperties, ReadResolutionProperties(opened));
            outputStream.Position = 0;
            using var decoded = new Bitmap(outputStream);
            Assert.Equal(opened.Size, decoded.Size);
            float expectedHorizontalDpi = preset == ExportDpiPreset.Preserve ? originalHorizontalDpi : preset == ExportDpiPreset.Web ? 72 : 300;
            float expectedVerticalDpi = preset == ExportDpiPreset.Preserve ? originalVerticalDpi : preset == ExportDpiPreset.Web ? 72 : 300;
            AssertResolution(decoded, expectedHorizontalDpi, expectedVerticalDpi, 0.05f);
            if (outputFormat == WellKnownFileFormats.Png)
            {
                Assert.Equal(originalPixels, ReadPixels(decoded));
                AssertPngResolution(outputStream.ToArray(), expectedHorizontalDpi, expectedVerticalDpi);
            }
            else
            {
                AssertJpegResolution(outputStream.ToArray(), expectedHorizontalDpi, expectedVerticalDpi);
            }
        }

        [Theory]
        [InlineData("capture.jpg", ExportDpiPreset.Web, 72)]
        [InlineData("capture.jpeg", ExportDpiPreset.Web, 72)]
        [InlineData("capture.JPG", ExportDpiPreset.Print, 300)]
        [InlineData("capture.JPEG", ExportDpiPreset.Print, 300)]
        public void SaveToStream_JpegExtensionAliases_ResolveAndApplyPreset(string filename, ExportDpiPreset preset, float expectedDpi)
        {
            string format = ImageIO.FormatForFilename(filename);
            Assert.Equal(WellKnownFileFormats.Jpg, format);
            using var source = CreatePatternBitmap(PixelFormat.Format24bppRgb);
            using var stream = new MemoryStream();

            ImageIO.SaveToStream(source, null, stream, CreateSettings(format, preset));

            AssertJpegResolution(stream.ToArray(), expectedDpi, expectedDpi);
            AssertResolution(source, SourceHorizontalDpi, SourceVerticalDpi);
        }

        [Theory]
        [InlineData(ExportDpiPreset.Web, 72)]
        [InlineData(ExportDpiPreset.Print, 300)]
        public void SaveToStream_UppercaseJpgFormat_AppliesPreset(ExportDpiPreset preset, float expectedDpi)
        {
            using var source = CreatePatternBitmap(PixelFormat.Format24bppRgb);
            using var stream = new MemoryStream();

            ImageIO.SaveToStream(source, null, stream, CreateSettings("JPG", preset));

            AssertJpegResolution(stream.ToArray(), expectedDpi, expectedDpi);
            AssertResolution(source, SourceHorizontalDpi, SourceVerticalDpi);
        }

        [Theory]
        [InlineData(WellKnownFileFormats.Png, -1)]
        [InlineData(WellKnownFileFormats.Png, 999)]
        [InlineData(WellKnownFileFormats.Jpg, -1)]
        [InlineData(WellKnownFileFormats.Jpg, 999)]
        public void SaveToStream_InvalidPreset_RejectsBeforeWriting(string format, int invalidPreset)
        {
            using var source = CreatePatternBitmap();
            int[] originalPixels = ReadPixels(source);
            using var stream = new MemoryStream();
            var settings = CreateSettings(format, (ExportDpiPreset)invalidPreset);

            ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
                ImageIO.SaveToStream(source, null, stream, settings));

            Assert.Equal(nameof(SurfaceOutputSettings.ExportDpiPreset), exception.ParamName);
            Assert.Equal(0, stream.Length);
            AssertResolution(source, SourceHorizontalDpi, SourceVerticalDpi);
            Assert.Equal(originalPixels, ReadPixels(source));
        }

        [Theory]
        [InlineData(PixelFormat.Format24bppRgb)]
        [InlineData(PixelFormat.Format32bppRgb)]
        [InlineData(PixelFormat.Format32bppArgb)]
        [InlineData(PixelFormat.Format32bppPArgb)]
        [InlineData(PixelFormat.Format8bppIndexed)]
        public void ConvertToImageSharp_PreservesResolutionAcrossPixelFormats(PixelFormat pixelFormat)
        {
            using var source = new Bitmap(7, 5, pixelFormat);
            source.SetResolution(SourceHorizontalDpi, SourceVerticalDpi);

            using var converted = ImageSharpHelper.ConvertToImageSharp(source);

            Assert.NotNull(converted);
            Assert.Equal(source.Width, converted.Width);
            Assert.Equal(source.Height, converted.Height);
            Assert.Equal(PixelResolutionUnit.PixelsPerInch, converted.Metadata.ResolutionUnits);
            Assert.Equal((double)SourceHorizontalDpi, converted.Metadata.HorizontalResolution);
            Assert.Equal((double)SourceVerticalDpi, converted.Metadata.VerticalResolution);
            AssertResolution(source, SourceHorizontalDpi, SourceVerticalDpi);
        }

        [Fact]
        public async Task ExportSummary_UsesFinalEffectSizeAndSourceDensityWithoutMutatingSource()
        {
            using var dispatcher = StrictTestUiDispatcher.Create();
            Surface surface = await dispatcher.InvokeAsync(() => new Surface(CreatePatternBitmap()));
            try
            {
                using var source = new SurfaceExportSource(surface, dispatcher, false);
                var settings = CreateSettings(WellKnownFileFormats.Png, ExportDpiPreset.Print);
                settings.SaveBackgroundOnly = true;
                settings.Effects.Add(new Greenshot.Base.Effects.ResizeEffect(12, 8, false));
                await ExportSummary.PopulateAsync(source, settings, CancellationToken.None);
                Assert.Equal(new System.Drawing.Size(12, 8), settings.PreviewSize);
                Assert.Equal(SourceHorizontalDpi, settings.PreviewDpiX);
                Assert.Equal(SourceVerticalDpi, settings.PreviewDpiY);
                Assert.Equal(0, source.OpenLeases);
                using (var lease = await source.RenderAsync(settings, CancellationToken.None))
                    AssertResolution(lease.Image, 300, 300);
                await dispatcher.InvokeAsync(() => AssertResolution(surface.Image, SourceHorizontalDpi, SourceVerticalDpi));
            }
            finally { await dispatcher.InvokeAsync(surface.Dispose); }
        }

        [Fact]
        public async Task SurfaceExportSource_RenderAndEncode_CachesEachDpiPresetSeparately()
        {
            using var dispatcher = StrictTestUiDispatcher.Create();
            Surface surface = await dispatcher.InvokeAsync(() => new Surface(CreatePatternBitmap()));
            try
            {
                using var source = new SurfaceExportSource(surface, dispatcher, false);
                var webSettings = CreateSettings(WellKnownFileFormats.Png, ExportDpiPreset.Web);
                var printSettings = CreateSettings(WellKnownFileFormats.Png, ExportDpiPreset.Print);
                var preserveSettings = CreateSettings(WellKnownFileFormats.Png, ExportDpiPreset.Preserve);
                webSettings.SaveBackgroundOnly = true;
                printSettings.SaveBackgroundOnly = true;
                preserveSettings.SaveBackgroundOnly = true;

                using (IImageLease web = await source.RenderAsync(webSettings, CancellationToken.None))
                using (IImageLease print = await source.RenderAsync(printSettings, CancellationToken.None))
                using (IImageLease preserve = await source.RenderAsync(preserveSettings, CancellationToken.None))
                {
                    Assert.Equal(web.Image.Size, print.Image.Size);
                    Assert.Equal(web.Image.Size, preserve.Image.Size);
                    AssertResolution(web.Image, 72, 72);
                    AssertResolution(print.Image, 300, 300);
                    AssertResolution(preserve.Image, SourceHorizontalDpi, SourceVerticalDpi);
                }

                Assert.Equal(0, source.OpenLeases);
                EncodedImage webEncoded = await source.EncodeAsync(webSettings, CancellationToken.None);
                EncodedImage printEncoded = await source.EncodeAsync(printSettings, CancellationToken.None);
                EncodedImage preserveEncoded = await source.EncodeAsync(preserveSettings, CancellationToken.None);
                Assert.NotSame(webEncoded, printEncoded);
                Assert.NotSame(webEncoded, preserveEncoded);
                Assert.NotSame(printEncoded, preserveEncoded);
                AssertPngResolution(webEncoded.ToArray(), 72, 72);
                AssertPngResolution(printEncoded.ToArray(), 300, 300);
                AssertPngResolution(preserveEncoded.ToArray(), SourceHorizontalDpi, SourceVerticalDpi);
                Assert.Same(webEncoded, await source.EncodeAsync(webSettings, CancellationToken.None));
                Assert.Same(printEncoded, await source.EncodeAsync(printSettings, CancellationToken.None));
                Assert.Same(preserveEncoded, await source.EncodeAsync(preserveSettings, CancellationToken.None));
                await dispatcher.InvokeAsync(() => AssertResolution(surface.Image, SourceHorizontalDpi, SourceVerticalDpi));
            }
            finally
            {
                await dispatcher.InvokeAsync(() => surface.Dispose());
            }
        }

        [Theory]
        [InlineData(WellKnownFileFormats.Png)]
        [InlineData(WellKnownFileFormats.Jpg)]
        public async Task SurfaceExportSource_CustomDpi_CachesEachValueSeparately(string format)
        {
            using var dispatcher = StrictTestUiDispatcher.Create();
            Surface surface = await dispatcher.InvokeAsync(() => new Surface(CreatePatternBitmap(PixelFormat.Format24bppRgb)));
            try
            {
                int[] originalPixels = await dispatcher.InvokeAsync(() => ReadPixels((Bitmap)surface.Image));
                using var source = new SurfaceExportSource(surface, dispatcher, false);
                var settings = CreateCustomSettings(format, 96);
                settings.SaveBackgroundOnly = true;
                int[] customValues = { 96, 144, 600 };
                var encodings = new EncodedImage[customValues.Length];
                for (int index = 0; index < customValues.Length; index++)
                {
                    int customDpi = customValues[index];
                    settings.CustomDpi = customDpi;
                    using (IImageLease lease = await source.RenderAsync(settings, CancellationToken.None))
                    {
                        AssertResolution(lease.Image, customDpi, customDpi);
                        Assert.Equal(originalPixels, ReadPixels((Bitmap)lease.Image));
                    }

                    encodings[index] = await source.EncodeAsync(settings, CancellationToken.None);
                    if (format == WellKnownFileFormats.Png)
                    {
                        AssertPngResolution(encodings[index].ToArray(), customDpi, customDpi);
                    }
                    else
                    {
                        AssertJpegResolution(encodings[index].ToArray(), customDpi, customDpi);
                    }

                    if (index > 0)
                    {
                        Assert.NotSame(encodings[index - 1], encodings[index]);
                    }
                }

                for (int index = customValues.Length - 1; index >= 0; index--)
                {
                    settings.CustomDpi = customValues[index];
                    Assert.Same(encodings[index], await source.EncodeAsync(settings, CancellationToken.None));
                    using IImageLease lease = await source.RenderAsync(settings, CancellationToken.None);
                    AssertResolution(lease.Image, customValues[index], customValues[index]);
                }

                settings.CustomDpi = 0;
                ArgumentOutOfRangeException renderException = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
                {
                    using IImageLease invalid = await source.RenderAsync(settings, CancellationToken.None);
                });
                ArgumentOutOfRangeException encodeException = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                    source.EncodeAsync(settings, CancellationToken.None));
                Assert.Equal(nameof(SurfaceOutputSettings.CustomDpi), renderException.ParamName);
                Assert.Equal(nameof(SurfaceOutputSettings.CustomDpi), encodeException.ParamName);
                settings.CustomDpi = customValues[1];
                Assert.Same(encodings[1], await source.EncodeAsync(settings, CancellationToken.None));
                Assert.Equal(0, source.OpenLeases);
                await dispatcher.InvokeAsync(() =>
                {
                    AssertResolution(surface.Image, SourceHorizontalDpi, SourceVerticalDpi);
                    Assert.Equal(originalPixels, ReadPixels((Bitmap)surface.Image));
                });
            }
            finally
            {
                await dispatcher.InvokeAsync(() => surface.Dispose());
            }
        }

        [Theory]
        [InlineData(ExportDpiPreset.Preserve, SourceHorizontalDpi, SourceVerticalDpi)]
        [InlineData(ExportDpiPreset.Web, 72, 72)]
        [InlineData(ExportDpiPreset.Print, 300, 300)]
        public async Task SurfaceExportSource_NonCustomPresets_IgnoreCustomDpiInCache(ExportDpiPreset preset, float expectedHorizontalDpi, float expectedVerticalDpi)
        {
            using var dispatcher = StrictTestUiDispatcher.Create();
            Surface surface = await dispatcher.InvokeAsync(() => new Surface(CreatePatternBitmap(PixelFormat.Format24bppRgb)));
            try
            {
                using var source = new SurfaceExportSource(surface, dispatcher, false);
                var settings = CreateSettings(WellKnownFileFormats.Png, preset);
                settings.SaveBackgroundOnly = true;
                settings.CustomDpi = 0;
                EncodedImage encoded = await source.EncodeAsync(settings, CancellationToken.None);
                AssertPngResolution(encoded.ToArray(), expectedHorizontalDpi, expectedVerticalDpi);

                foreach (int customDpi in new[] { 96, 600, -1, 2401, int.MaxValue })
                {
                    settings.CustomDpi = customDpi;
                    Assert.Same(encoded, await source.EncodeAsync(settings, CancellationToken.None));
                    using IImageLease lease = await source.RenderAsync(settings, CancellationToken.None);
                    AssertResolution(lease.Image, expectedHorizontalDpi, expectedVerticalDpi);
                }

                Assert.Equal(0, source.OpenLeases);
                await dispatcher.InvokeAsync(() => AssertResolution(surface.Image, SourceHorizontalDpi, SourceVerticalDpi));
            }
            finally
            {
                await dispatcher.InvokeAsync(() => surface.Dispose());
            }
        }

        private static SurfaceOutputSettings CreateCustomSettings(string format, int customDpi)
        {
            var settings = CreateSettings(format, ExportDpiPreset.Custom);
            settings.CustomDpi = customDpi;
            return settings;
        }

        private static SurfaceOutputSettings CreateSettings(string format, ExportDpiPreset preset)
        {
            return new SurfaceOutputSettings(format, 90, false)
            {
                DisableReduceColors = true,
                ExportDpiPreset = preset
            };
        }

        private static Bitmap CreatePatternBitmap(PixelFormat pixelFormat = PixelFormat.Format32bppArgb, int width = 19, int height = 13)
        {
            var bitmap = new Bitmap(width, height, pixelFormat);
            bitmap.SetResolution(SourceHorizontalDpi, SourceVerticalDpi);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int alpha = Image.IsAlphaPixelFormat(pixelFormat) ? new[] { 0, 64, 128, 255 }[(x + y) % 4] : 255;
                    Color color = alpha == 0
                        ? Color.FromArgb(0, 0, 0, 0)
                        : Color.FromArgb(alpha, (x * 7) % 256, (y * 15) % 256, ((x + y) * 5) % 256);
                    bitmap.SetPixel(x, y, color);
                }
            }

            return bitmap;
        }

        private static Bitmap CreateTwoColorBitmap()
        {
            var bitmap = new Bitmap(19, 13, PixelFormat.Format24bppRgb);
            bitmap.SetResolution(SourceHorizontalDpi, SourceVerticalDpi);
            for (int y = 0; y < bitmap.Height; y++)
            {
                for (int x = 0; x < bitmap.Width; x++)
                {
                    bitmap.SetPixel(x, y, (x + y) % 2 == 0 ? Color.Red : Color.Blue);
                }
            }

            return bitmap;
        }

        private static int[] ReadPixels(Bitmap bitmap)
        {
            var pixels = new int[bitmap.Width * bitmap.Height];
            for (int y = 0; y < bitmap.Height; y++)
            {
                for (int x = 0; x < bitmap.Width; x++)
                {
                    pixels[y * bitmap.Width + x] = bitmap.GetPixel(x, y).ToArgb();
                }
            }

            return pixels;
        }

        private static string[] ReadResolutionProperties(Image image)
        {
            int[] resolutionPropertyIds = { 0x011a, 0x011b, 0x0128, 0x5110, 0x5111 };
            return image.PropertyItems
                .Where(property => resolutionPropertyIds.Contains(property.Id))
                .OrderBy(property => property.Id)
                .Select(property => $"{property.Id}|{property.Type}|{property.Len}|{BitConverter.ToString(property.Value)}")
                .ToArray();
        }

        private static void AssertResolution(Image image, float horizontalDpi, float verticalDpi, float tolerance = 0.001f)
        {
            Assert.InRange(image.HorizontalResolution, horizontalDpi - tolerance, horizontalDpi + tolerance);
            Assert.InRange(image.VerticalResolution, verticalDpi - tolerance, verticalDpi + tolerance);
        }

        private static void DisposeReplacement(Image source, Image output, bool disposeImage)
        {
            if (disposeImage && !ReferenceEquals(source, output))
            {
                output?.Dispose();
            }
        }

        private static void AssertPngResolution(byte[] png, float horizontalDpi, float verticalDpi)
        {
            Assert.True(png.Length >= 8);
            Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, new ArraySegment<byte>(png, 0, 8));
            int offset = 8;
            while (offset + 12 <= png.Length)
            {
                int length = checked((int)ReadBigEndianUInt32(png, offset));
                Assert.InRange(length, 0, png.Length - offset - 12);
                string type = Encoding.ASCII.GetString(png, offset + 4, 4);
                if (type == "pHYs")
                {
                    Assert.Equal(9, length);
                    Assert.Equal((byte)1, png[offset + 16]);
                    double expectedHorizontal = horizontalDpi / 0.0254;
                    double expectedVertical = verticalDpi / 0.0254;
                    Assert.InRange((double)ReadBigEndianUInt32(png, offset + 8), expectedHorizontal - 1, expectedHorizontal + 1);
                    Assert.InRange((double)ReadBigEndianUInt32(png, offset + 12), expectedVertical - 1, expectedVertical + 1);
                    return;
                }

                offset += length + 12;
            }

            Assert.Fail("PNG export is missing the pHYs resolution chunk.");
        }

        private static void AssertJpegResolution(byte[] jpeg, float horizontalDpi, float verticalDpi)
        {
            Assert.True(jpeg.Length >= 2);
            Assert.Equal((byte)0xff, jpeg[0]);
            Assert.Equal((byte)0xd8, jpeg[1]);
            int offset = 2;
            while (offset + 4 <= jpeg.Length)
            {
                Assert.Equal((byte)0xff, jpeg[offset]);
                byte marker = jpeg[offset + 1];
                if (marker == 0xda || marker == 0xd9)
                {
                    break;
                }

                int length = ReadBigEndianUInt16(jpeg, offset + 2);
                Assert.InRange(length, 2, jpeg.Length - offset - 2);
                int dataOffset = offset + 4;
                if (marker == 0xe0 && length >= 16 && Encoding.ASCII.GetString(jpeg, dataOffset, 5) == "JFIF\0")
                {
                    Assert.Equal((byte)1, jpeg[dataOffset + 7]);
                    Assert.Equal((int)Math.Round(horizontalDpi, MidpointRounding.AwayFromZero), ReadBigEndianUInt16(jpeg, dataOffset + 8));
                    Assert.Equal((int)Math.Round(verticalDpi, MidpointRounding.AwayFromZero), ReadBigEndianUInt16(jpeg, dataOffset + 10));
                    return;
                }

                offset += length + 2;
            }

            Assert.Fail("JPEG export is missing the JFIF resolution header.");
        }

        private static uint ReadBigEndianUInt32(byte[] bytes, int offset)
        {
            return ((uint)bytes[offset] << 24) | ((uint)bytes[offset + 1] << 16) | ((uint)bytes[offset + 2] << 8) | bytes[offset + 3];
        }

        private static int ReadBigEndianUInt16(byte[] bytes, int offset)
        {
            return (bytes[offset] << 8) | bytes[offset + 1];
        }
    }

    [CollectionDefinition("Export DPI configuration", DisableParallelization = true)]
    public class ExportDpiConfigurationCollection
    {
    }
}
