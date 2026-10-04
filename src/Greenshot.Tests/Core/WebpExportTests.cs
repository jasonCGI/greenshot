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
using System.Collections.Generic;
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
using Greenshot.Base.Effects;
using Greenshot.Base.Interfaces;
using Greenshot.Base.Interfaces.Plugin;
using Greenshot.Editor.Drawing;
using Greenshot.Editor.FileFormatHandlers;
using Greenshot.Tests.Threading;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;
using SharpImage = SixLabors.ImageSharp.Image;

namespace Greenshot.Tests.Core
{
    [Collection(TestCollections.WpfThemeState)]
    public class WebpExportTests
    {
        private const int SampleSize = 128;
        private const float SourceHorizontalDpi = 144;
        private const float SourceVerticalDpi = 96;

        public WebpExportTests()
        {
            TestEnvironment.EnsureInitialized();
        }

        [Fact]
        public void Configuration_DeclaredAndResetDefaults_AreLossyQuality80()
        {
            PropertyInfo losslessProperty = typeof(ICoreConfiguration).GetProperty(nameof(ICoreConfiguration.OutputFileWebpLossless));
            PropertyInfo qualityProperty = typeof(ICoreConfiguration).GetProperty(nameof(ICoreConfiguration.OutputFileWebpQuality));
            Assert.Equal(false, losslessProperty.GetCustomAttribute<DefaultValueAttribute>().Value);
            Assert.Equal(80, qualityProperty.GetCustomAttribute<DefaultValueAttribute>().Value);

            var configuration = new CoreConfigurationImpl();
            configuration.ResetToDefaults();
            Assert.False(configuration.OutputFileWebpLossless);
            Assert.Equal(80, configuration.OutputFileWebpQuality);
        }

        [Theory]
        [InlineData(false, 0)]
        [InlineData(false, 80)]
        [InlineData(false, 100)]
        [InlineData(true, 0)]
        [InlineData(true, 80)]
        [InlineData(true, 100)]
        public void Configuration_IniRoundTrip_PreservesModeAndQuality(bool lossless, int quality)
        {
            var configuration = new CoreConfigurationImpl();
            configuration.ResetToDefaults();
            configuration.OutputFileWebpLossless = lossless;
            configuration.OutputFileWebpQuality = quality;
            string losslessKey = nameof(ICoreConfiguration.OutputFileWebpLossless);
            string qualityKey = nameof(ICoreConfiguration.OutputFileWebpQuality);
            var ini = new IniFile();
            IniSection section = ini.GetOrAddSection(configuration.SectionName);
            foreach (var entry in configuration.GetAllRawValues().Where(entry => entry.Key == losslessKey || entry.Key == qualityKey))
            {
                section.SetValue(entry.Key, entry.Value);
            }

            string serialized = IniFileWriter.WriteToString(ini, new IniWriterOptions());
            IniFile parsed = IniFileParser.Parse(serialized, new IniParserOptions());
            var reloaded = new CoreConfigurationImpl();
            reloaded.ResetToDefaults();
            reloaded.SetRawValue(losslessKey, parsed.GetSection(configuration.SectionName).GetValue(losslessKey));
            reloaded.SetRawValue(qualityKey, parsed.GetSection(configuration.SectionName).GetValue(qualityKey));

            Assert.Equal(lossless, reloaded.OutputFileWebpLossless);
            Assert.Equal(quality, reloaded.OutputFileWebpQuality);
        }

        [Fact]
        public void Configuration_IniWithoutWebpSettings_KeepsSafeDefaults()
        {
            IniFile parsed = IniFileParser.Parse("[Core]\nOutputFileFormat=webp\n", new IniParserOptions());
            var configuration = new CoreConfigurationImpl();
            configuration.ResetToDefaults();
            configuration.SetRawValue(nameof(ICoreConfiguration.OutputFileFormat), parsed.GetSection("Core").GetValue(nameof(ICoreConfiguration.OutputFileFormat)));

            Assert.Equal(WellKnownFileFormats.Webp, configuration.OutputFileFormat);
            Assert.False(configuration.OutputFileWebpLossless);
            Assert.Equal(80, configuration.OutputFileWebpQuality);
        }

        [Fact]
        public void SurfaceOutputSettings_InheritsWebpDefaultsAndExplicitQualityOverloads()
        {
            var configuration = IniConfigRegistry.GetSection<ICoreConfiguration>();
            bool originalLossless = configuration.OutputFileWebpLossless;
            int originalQuality = configuration.OutputFileWebpQuality;
            try
            {
                configuration.OutputFileWebpLossless = true;
                configuration.OutputFileWebpQuality = 73;
                var settings = new[]
                {
                    new SurfaceOutputSettings(),
                    new SurfaceOutputSettings(WellKnownFileFormats.Webp),
                    new SurfaceOutputSettings(WellKnownFileFormats.Webp, 91),
                    new SurfaceOutputSettings(WellKnownFileFormats.Webp, 92, true)
                };

                Assert.All(settings, setting => Assert.True(setting.WebpLossless));
                Assert.Equal(new[] { 73, 73, 91, 92 }, settings.Select(setting => setting.WebpQuality));
            }
            finally
            {
                configuration.OutputFileWebpLossless = originalLossless;
                configuration.OutputFileWebpQuality = originalQuality;
            }
        }

        [Fact]
        public void NormalInitialization_ExposesWebpRegistryHandlerAndSaveOptionsWithoutBeta()
        {
            var configuration = IniConfigRegistry.GetSection<ICoreConfiguration>();
            Assert.False(configuration.IsBetaTester);
            IFileFormatHandler[] handlers = SimpleServiceProvider.Current.GetAllInstances<IFileFormatHandler>().ToArray();
            Assert.Contains(handlers, handler => handler is DefaultFileFormatHandler);
            Assert.Contains(handlers, handler => handler is WebpFileFormatHandler);
            Assert.DoesNotContain(handlers, handler => handler.GetType() == typeof(ImageSharpFileFormatHandler));

            var registry = SimpleServiceProvider.Current.GetInstance<IFileFormatRegistry>();
            Assert.True(registry.TryGet(WellKnownFileFormats.Webp, out FileFormatDefinition definition));
            Assert.Equal("image/webp", definition.MimeType);
            Assert.True(definition.CanOpen);
            Assert.True(definition.CanSave);
            Assert.Equal(new[] { "webp" }, definition.SaveableExtensions);
            Assert.Contains(registry.GetSaveableFileFormats(), format => format.Id == WellKnownFileFormats.Webp);
            Assert.Contains(registry.GetSaveableFileFormatOptions(), option => option.Id == WellKnownFileFormats.Webp);
            Assert.Contains(WellKnownFileFormats.Webp, new SaveableFileFormatIds().GetAllowedValues());
            Assert.Equal("WebP (.webp)", definition.GetDisplayNameWithSaveableExtensions());
            Assert.Equal(WellKnownFileFormats.Webp, ImageIO.FormatForFilename("capture.WEBP"));
            Assert.Equal("webp", FileFormatRegistry.GetPreferredExtension("WEBP"));
            Assert.True(WellKnownFileFormats.IsEqualFormat(WellKnownFileFormats.Webp, "WEBP"));
        }

        [Theory]
        [InlineData(false, 0, "VP8 ")]
        [InlineData(false, 80, "VP8 ")]
        [InlineData(false, 100, "VP8 ")]
        [InlineData(true, 80, "VP8L")]
        public void SaveToStream_WritesRealRiffEncodingModeIncludingQuality100(bool lossless, int quality, string expectedChunk)
        {
            using var source = CreatePatternBitmap();
            int[] originalPixels = ReadPixels(source);
            byte[] encoded = Encode(source, CreateSettings(lossless, quality));
            string[] chunks = ReadRiffChunks(encoded);

            Assert.Contains(expectedChunk, chunks);
            Assert.DoesNotContain(lossless ? "VP8 " : "VP8L", chunks);
            AssertSourceUnchanged(source, originalPixels);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(80)]
        [InlineData(100)]
        public void Lossless_RoundTripPreservesEveryRgbaPixelIncludingHiddenTransparentRgb(int quality)
        {
            using var source = CreatePatternBitmap();
            int[] originalPixels = ReadPixels(source);
            Assert.Equal(0, source.GetPixel(0, 0).A);
            Assert.NotEqual(0, source.GetPixel(0, 0).R);
            byte[] encoded = Encode(source, CreateSettings(true, quality));
            using SixLabors.ImageSharp.Image<Rgba32> decoded = SharpImage.Load<Rgba32>(encoded);

            Assert.Equal(SampleSize, decoded.Width);
            Assert.Equal(SampleSize, decoded.Height);
            Assert.Equal(originalPixels, ReadPixels(decoded));
            AssertSourceUnchanged(source, originalPixels);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(80)]
        [InlineData(100)]
        public void Lossy_RoundTripPreservesDimensionsAndExactAlphaWithoutClaimingIdenticalRgb(int quality)
        {
            using var source = CreatePatternBitmap();
            int[] originalPixels = ReadPixels(source);
            byte[] encoded = Encode(source, CreateSettings(false, quality));
            using SixLabors.ImageSharp.Image<Rgba32> decoded = SharpImage.Load<Rgba32>(encoded);

            Assert.Equal(source.Width, decoded.Width);
            Assert.Equal(source.Height, decoded.Height);
            for (int y = 0; y < source.Height; y++)
            {
                for (int x = 0; x < source.Width; x++)
                {
                    Assert.Equal(source.GetPixel(x, y).A, decoded[x, y].A);
                }
            }
            AssertSourceUnchanged(source, originalPixels);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void CreateImageForOutput_WebpSkipsForcedColorReductionAndDpiPresets(bool lossless)
        {
            using var source = CreatePatternBitmap();
            int[] originalPixels = ReadPixels(source);
            var settings = CreateSettings(lossless, 80);
            settings.ReduceColors = true;
            settings.DisableReduceColors = false;
            settings.ExportDpiPreset = ExportDpiPreset.Custom;
            settings.CustomDpi = 0;

            bool ownsOutput = ImageIO.CreateImageForOutput(source, false, settings, out Image output);

            Assert.False(ownsOutput);
            Assert.Same(source, output);
            Assert.Equal(PixelFormat.Format32bppArgb, output.PixelFormat);
            AssertSourceUnchanged(source, originalPixels);
            byte[] encoded = Encode(source, settings);
            Assert.Contains(lossless ? "VP8L" : "VP8 ", ReadRiffChunks(encoded));
            if (lossless)
            {
                using SixLabors.ImageSharp.Image<Rgba32> decoded = SharpImage.Load<Rgba32>(encoded);
                Assert.Equal(originalPixels, ReadPixels(decoded));
            }
            AssertSourceUnchanged(source, originalPixels);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(101)]
        [InlineData(int.MinValue)]
        [InlineData(int.MaxValue)]
        public void ActiveLossyQuality_InvalidValuesRejectBeforeStreamWritesOrOwnedSourceMutation(int quality)
        {
            using var source = CreatePatternBitmap();
            int[] originalPixels = ReadPixels(source);
            var settings = CreateSettings(false, quality);
            settings.Effects.Add(new ResizeEffect(2, 2, false));
            settings.ReduceColors = true;
            using var stream = new MemoryStream();
            stream.WriteByte(0x5a);

            ArgumentOutOfRangeException saveException = Assert.Throws<ArgumentOutOfRangeException>(() =>
                ImageIO.SaveToStream(source, null, stream, settings));
            ArgumentOutOfRangeException prepareException = Assert.Throws<ArgumentOutOfRangeException>(() =>
                ImageIO.CreateImageForOutput(source, true, settings, out _));

            Assert.Equal(nameof(SurfaceOutputSettings.WebpQuality), saveException.ParamName);
            Assert.Equal(nameof(SurfaceOutputSettings.WebpQuality), prepareException.ParamName);
            Assert.Equal(new byte[] { 0x5a }, stream.ToArray());
            Assert.Equal(1, stream.Position);
            AssertSourceUnchanged(source, originalPixels);
        }

        [Theory]
        [InlineData(false, -1, false)]
        [InlineData(true, -1, false)]
        [InlineData(false, 101, false)]
        [InlineData(true, 101, false)]
        [InlineData(false, 80, true)]
        [InlineData(true, 80, true)]
        public void SaveRenderedImage_InvalidQualityOrSizeDoesNotCreateOrTruncateDestination(bool existingDestination, int quality, bool oversized)
        {
            string directory = Path.Combine(Path.GetTempPath(), "Greenshot-WebpExportTests-" + Guid.NewGuid().ToString("N"));
            string destination = Path.Combine(directory, "guarded.webp");
            byte[] sentinel = { 0x47, 0x53, 0x2d, 0x73, 0x61, 0x66, 0x65 };
            Directory.CreateDirectory(directory);
            try
            {
                if (existingDestination) File.WriteAllBytes(destination, sentinel);
                using var source = oversized ? CreatePatternBitmap(16384, 1) : CreatePatternBitmap();
                int[] originalPixels = ReadPixels(source);
                Size originalSize = source.Size;
                var settings = CreateSettings(false, quality);

                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    ImageIO.SaveRenderedImage(source, destination, true, settings, false));

                if (existingDestination)
                {
                    Assert.Equal(sentinel, File.ReadAllBytes(destination));
                }
                else
                {
                    Assert.False(File.Exists(destination));
                }
                Assert.Equal(originalSize, source.Size);
                AssertDpi(source);
                Assert.Equal(originalPixels, ReadPixels(source));
            }
            finally
            {
                // Both paths belong to this unique test folder; never recurse into unrelated files.
                if (File.Exists(destination)) File.Delete(destination);
                Directory.Delete(directory);
            }
        }

        [Theory]
        [InlineData(-1, false)]
        [InlineData(101, false)]
        [InlineData(80, true)]
        public void SaveToTmpFile_RenderedImageRejectsInvalidQualityOrSizeWithoutCreatingAFile(int quality, bool oversized)
        {
            string directory = Path.Combine(Path.GetTempPath(), "Greenshot-WebpExportTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                Assert.Empty(Directory.GetFileSystemEntries(directory));
                using var source = oversized ? CreatePatternBitmap(16384, 1) : CreatePatternBitmap();
                int[] originalPixels = ReadPixels(source);
                Size originalSize = source.Size;

                Assert.Null(ImageIO.SaveToTmpFile(source, CreateSettings(false, quality), directory));

                Assert.Empty(Directory.GetFileSystemEntries(directory));
                Assert.Equal(originalSize, source.Size);
                Assert.Equal(PixelFormat.Format32bppArgb, source.PixelFormat);
                AssertDpi(source);
                Assert.Equal(originalPixels, ReadPixels(source));
            }
            finally
            {
                // Keep any unexpected output for diagnosis rather than touch the shared temp-file cache.
                if (Directory.Exists(directory) && Directory.GetFileSystemEntries(directory).Length == 0)
                {
                    Directory.Delete(directory);
                }
            }
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(101)]
        public void DirectWebpHandler_RejectsInvalidActiveQualityEvenWithMismatchedSettingsFormat(int quality)
        {
            using var source = CreatePatternBitmap();
            int[] originalPixels = ReadPixels(source);
            var settings = new SurfaceOutputSettings(WellKnownFileFormats.Png)
            {
                WebpLossless = false,
                WebpQuality = quality
            };
            using var stream = new MemoryStream();

            Assert.Throws<ArgumentOutOfRangeException>(() => new WebpFileFormatHandler().TrySaveToStream(source, stream, ".webp", null, settings));

            Assert.Equal(0, stream.Length);
            AssertSourceUnchanged(source, originalPixels);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(101)]
        [InlineData(int.MinValue)]
        [InlineData(int.MaxValue)]
        public void Lossless_IgnoresInvalidLatentQualityAndKeepsExactRgba(int latentQuality)
        {
            using var source = CreatePatternBitmap();
            int[] originalPixels = ReadPixels(source);
            byte[] baseline = Encode(source, CreateSettings(true, 80));
            byte[] encoded = Encode(source, CreateSettings(true, latentQuality));
            Assert.Equal(baseline, encoded);
            Assert.Contains("VP8L", ReadRiffChunks(encoded));
            using SixLabors.ImageSharp.Image<Rgba32> decoded = SharpImage.Load<Rgba32>(encoded);
            Assert.Equal(originalPixels, ReadPixels(decoded));
            AssertSourceUnchanged(source, originalPixels);
        }

        [Theory]
        [InlineData(WellKnownFileFormats.Png, false, -1)]
        [InlineData(WellKnownFileFormats.Png, true, int.MinValue)]
        [InlineData(WellKnownFileFormats.Jpg, false, 101)]
        [InlineData(WellKnownFileFormats.Jpg, true, int.MaxValue)]
        public void PngAndJpeg_IgnoreInactiveWebpSettings(string format, bool lossless, int quality)
        {
            using var source = CreatePatternBitmap();
            int[] originalPixels = ReadPixels(source);
            var baselineSettings = new SurfaceOutputSettings(format, 90, false)
            {
                DisableReduceColors = true,
                ExportDpiPreset = ExportDpiPreset.Preserve
            };
            byte[] baseline = Encode(source, baselineSettings);
            baselineSettings.WebpLossless = lossless;
            baselineSettings.WebpQuality = quality;

            Assert.False(ImageIO.CreateImageForOutput(source, false, baselineSettings, out Image output));
            Assert.Same(source, output);
            Assert.Equal(baseline, Encode(source, baselineSettings));
            AssertSourceUnchanged(source, originalPixels);
        }

        [Theory]
        [InlineData(1, 1)]
        [InlineData(16383, 1)]
        [InlineData(1, 16383)]
        [InlineData(16383, 16383)]
        public void DimensionValidation_AcceptsInclusiveBoundsWithoutAllocatingLargeImages(int width, int height)
        {
            Assert.Equal(16383, WebpExportSettings.MaximumDimension);
            WebpExportSettings.ValidateSize(new Size(width, height));
        }

        [Theory]
        [InlineData(0, 1)]
        [InlineData(1, 0)]
        [InlineData(-1, 1)]
        [InlineData(1, -1)]
        [InlineData(16384, 1)]
        [InlineData(1, 16384)]
        [InlineData(int.MaxValue, int.MaxValue)]
        public void DimensionValidation_RejectsZeroNegativeAndBeyondWebpLimitBeforeEncoding(int width, int height)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => WebpExportSettings.ValidateSize(new Size(width, height)));
        }

        [Fact]
        public void Validation_NullSettingsAreRejected()
        {
            Assert.Throws<ArgumentNullException>(() => WebpExportSettings.Validate(null));
        }

        [Fact]
        public async Task SurfaceExportSource_CachesLossyQualitiesAndLosslessModeSeparately()
        {
            using var dispatcher = StrictTestUiDispatcher.Create();
            Surface surface = await dispatcher.InvokeAsync(() => new Surface(CreatePatternBitmap()));
            try
            {
                int[] originalPixels = await dispatcher.InvokeAsync(() => ReadPixels((Bitmap)surface.Image));
                using var source = new SurfaceExportSource(surface, dispatcher, false);
                var settings = CreateSettings(false, 0);
                settings.SaveBackgroundOnly = true;
                var lossyEncodings = new EncodedImage[3];
                int[] qualities = { 0, 80, 100 };
                for (int index = 0; index < qualities.Length; index++)
                {
                    settings.WebpQuality = qualities[index];
                    lossyEncodings[index] = await source.EncodeAsync(settings, CancellationToken.None);
                    Assert.Contains("VP8 ", ReadRiffChunks(lossyEncodings[index].ToArray()));
                    if (index > 0)
                    {
                        Assert.NotSame(lossyEncodings[index - 1], lossyEncodings[index]);
                    }
                }

                settings.WebpLossless = true;
                EncodedImage lossless = await source.EncodeAsync(settings, CancellationToken.None);
                Assert.Contains("VP8L", ReadRiffChunks(lossless.ToArray()));
                Assert.All(lossyEncodings, lossy => Assert.NotSame(lossy, lossless));

                for (int index = qualities.Length - 1; index >= 0; index--)
                {
                    settings.WebpLossless = false;
                    settings.WebpQuality = qualities[index];
                    Assert.Same(lossyEncodings[index], await source.EncodeAsync(settings, CancellationToken.None));
                }
                settings.WebpLossless = true;
                Assert.Same(lossless, await source.EncodeAsync(settings, CancellationToken.None));
                Assert.Equal(0, source.OpenLeases);
                await dispatcher.InvokeAsync(() => AssertSourceUnchanged((Bitmap)surface.Image, originalPixels));
            }
            finally
            {
                await dispatcher.InvokeAsync(() => surface.Dispose());
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task SurfaceExportSource_IgnoresInactiveJpegAndLatentLosslessQualityInCache(bool lossless)
        {
            using var dispatcher = StrictTestUiDispatcher.Create();
            Surface surface = await dispatcher.InvokeAsync(() => new Surface(CreatePatternBitmap()));
            try
            {
                int[] originalPixels = await dispatcher.InvokeAsync(() => ReadPixels((Bitmap)surface.Image));
                using var source = new SurfaceExportSource(surface, dispatcher, false);
                var settings = CreateSettings(lossless, 80);
                settings.SaveBackgroundOnly = true;
                EncodedImage encoded = await source.EncodeAsync(settings, CancellationToken.None);
                foreach (int quality in new[] { 0, 100, -1, 101, int.MaxValue })
                {
                    settings.JPGQuality = quality;
                    if (lossless) settings.WebpQuality = quality;
                    Assert.Same(encoded, await source.EncodeAsync(settings, CancellationToken.None));
                    using IImageLease lease = await source.RenderAsync(settings, CancellationToken.None);
                    Assert.Equal(new Size(SampleSize, SampleSize), lease.Image.Size);
                    AssertDpi(lease.Image);
                }
                Assert.Equal(0, source.OpenLeases);
                await dispatcher.InvokeAsync(() => AssertSourceUnchanged((Bitmap)surface.Image, originalPixels));
            }
            finally
            {
                await dispatcher.InvokeAsync(() => surface.Dispose());
            }
        }

        [Fact]
        public async Task SurfaceExportSource_InvalidLossyQualityCannotReturnCachedEncodingOrRender()
        {
            using var dispatcher = StrictTestUiDispatcher.Create();
            Surface surface = await dispatcher.InvokeAsync(() => new Surface(CreatePatternBitmap()));
            try
            {
                int[] originalPixels = await dispatcher.InvokeAsync(() => ReadPixels((Bitmap)surface.Image));
                using var source = new SurfaceExportSource(surface, dispatcher, false);
                var settings = CreateSettings(false, 80);
                settings.SaveBackgroundOnly = true;
                EncodedImage valid = await source.EncodeAsync(settings, CancellationToken.None);
                settings.WebpQuality = 101;
                await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => source.EncodeAsync(settings, CancellationToken.None));
                await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
                {
                    using IImageLease invalid = await source.RenderAsync(settings, CancellationToken.None);
                });
                settings.WebpQuality = 80;
                Assert.Same(valid, await source.EncodeAsync(settings, CancellationToken.None));
                Assert.Equal(0, source.OpenLeases);
                await dispatcher.InvokeAsync(() => AssertSourceUnchanged((Bitmap)surface.Image, originalPixels));
            }
            finally
            {
                await dispatcher.InvokeAsync(() => surface.Dispose());
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task SaveSurface_WebpValidatesEffectOutputDimensionsRatherThanOriginalDimensions(bool oversizedSource)
        {
            using var dispatcher = StrictTestUiDispatcher.Create();
            Surface surface = await dispatcher.InvokeAsync(() => new Surface(oversizedSource
                ? CreatePatternBitmap(16384, 1) : CreatePatternBitmap()));
            string directory = Path.Combine(Path.GetTempPath(), "Greenshot-WebpExportTests-" + Guid.NewGuid().ToString("N"));
            string destination = Path.Combine(directory, "effect-output.webp");
            try
            {
                Directory.CreateDirectory(directory);
                int[] originalPixels = await dispatcher.InvokeAsync(() => ReadPixels((Bitmap)surface.Image));
                Size originalSize = await dispatcher.InvokeAsync(() => surface.Image.Size);
                var settings = CreateSettings(true, 80);
                settings.SaveBackgroundOnly = true;
                settings.Effects.Add(new ResizeEffect(oversizedSource ? 128 : 16384, 1, false));
                if (oversizedSource)
                {
                    await dispatcher.InvokeAsync(() => ImageIO.Save(surface, destination, true, settings, false));
                    byte[] encoded = File.ReadAllBytes(destination);
                    Assert.Contains("VP8L", ReadRiffChunks(encoded));
                    using SixLabors.ImageSharp.Image<Rgba32> decoded = SharpImage.Load<Rgba32>(encoded);
                    Assert.Equal(128, decoded.Width);
                    Assert.Equal(1, decoded.Height);
                }
                else
                {
                    await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => dispatcher.InvokeAsync(() =>
                        ImageIO.Save(surface, destination, true, settings, false)));
                    Assert.False(File.Exists(destination));
                }
                await dispatcher.InvokeAsync(() =>
                {
                    Assert.Equal(originalSize, surface.Image.Size);
                    AssertDpi(surface.Image);
                    Assert.Equal(originalPixels, ReadPixels((Bitmap)surface.Image));
                });
            }
            finally
            {
                await dispatcher.InvokeAsync(() => surface.Dispose());
                if (File.Exists(destination)) File.Delete(destination);
                if (Directory.Exists(directory)) Directory.Delete(directory);
            }
        }

        private static SurfaceOutputSettings CreateSettings(bool lossless, int quality)
        {
            return new SurfaceOutputSettings(WellKnownFileFormats.Webp)
            {
                DisableReduceColors = true,
                ExportDpiPreset = ExportDpiPreset.Preserve,
                WebpLossless = lossless,
                WebpQuality = quality
            };
        }

        private static byte[] Encode(Bitmap source, SurfaceOutputSettings settings)
        {
            using var stream = new MemoryStream();
            ImageIO.SaveToStream(source, null, stream, settings);
            return stream.ToArray();
        }

        private static Bitmap CreatePatternBitmap(int width = SampleSize, int height = SampleSize)
        {
            var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            bitmap.SetResolution(SourceHorizontalDpi, SourceVerticalDpi);
            int[] alphaValues = { 0, 64, 128, 255 };
            for (int y = 0; y < bitmap.Height; y++)
            {
                for (int x = 0; x < bitmap.Width; x++)
                {
                    // Hidden nonzero RGB is deliberate: exact lossless export must preserve it too.
                    bitmap.SetPixel(x, y, Color.FromArgb(alphaValues[(x + y) % alphaValues.Length],
                        (x * 29 + 17) % 256, (y * 37 + 23) % 256, ((x + y) * 43 + 31) % 256));
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

        private static int[] ReadPixels(SixLabors.ImageSharp.Image<Rgba32> image)
        {
            var pixels = new int[image.Width * image.Height];
            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    Rgba32 pixel = image[x, y];
                    pixels[y * image.Width + x] = Color.FromArgb(pixel.A, pixel.R, pixel.G, pixel.B).ToArgb();
                }
            }
            return pixels;
        }

        private static void AssertSourceUnchanged(Bitmap source, int[] originalPixels)
        {
            Assert.Equal(new Size(SampleSize, SampleSize), source.Size);
            Assert.Equal(PixelFormat.Format32bppArgb, source.PixelFormat);
            AssertDpi(source);
            Assert.Equal(originalPixels, ReadPixels(source));
        }

        private static void AssertDpi(Image image)
        {
            Assert.Equal(SourceHorizontalDpi, image.HorizontalResolution);
            Assert.Equal(SourceVerticalDpi, image.VerticalResolution);
        }

        private static string[] ReadRiffChunks(byte[] encoded)
        {
            Assert.True(encoded.Length >= 12);
            Assert.Equal("RIFF", Encoding.ASCII.GetString(encoded, 0, 4));
            Assert.Equal((uint)(encoded.Length - 8), ReadLittleEndianUInt32(encoded, 4));
            Assert.Equal("WEBP", Encoding.ASCII.GetString(encoded, 8, 4));
            var chunks = new List<string>();
            int offset = 12;
            while (offset < encoded.Length)
            {
                Assert.True(offset + 8 <= encoded.Length);
                uint length = ReadLittleEndianUInt32(encoded, offset + 4);
                Assert.InRange(length, 0U, (uint)(encoded.Length - offset - 8));
                chunks.Add(Encoding.ASCII.GetString(encoded, offset, 4));
                offset = checked(offset + 8 + (int)length + ((int)length & 1));
            }
            Assert.Equal(encoded.Length, offset);
            return chunks.ToArray();
        }

        private static uint ReadLittleEndianUInt32(byte[] bytes, int offset)
        {
            return bytes[offset] | ((uint)bytes[offset + 1] << 8) | ((uint)bytes[offset + 2] << 16) | ((uint)bytes[offset + 3] << 24);
        }
    }
}
