/*
 * Greenshot - a free and open source screenshot tool
 * Copyright (C) 2007-2026 Thomas Braun, Jens Klingen, Robin Krom
 * Licensed under the GNU General Public License, version 1 or later.
 */
using System;
using System.Linq;
using System.IO;
using Dapplo.Ini;
using Dapplo.Ini.Parsing;
using Greenshot.Base.Core;
using Greenshot.Base.Core.Enums;
using Xunit;

namespace Greenshot.Tests.Core
{
    [CollectionDefinition("Export profile policy", DisableParallelization = true)]
    public class ExportProfilePolicyCollection { }

    [Collection("Export profile policy")]
    public class ExportProfileTests
    {
        [Fact]
        public void FixedSettingBlocksWholeProfileBeforeAnyChanges()
        {
            string directory = Path.Combine(Path.GetTempPath(), "Greenshot-profile-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string constants = Path.Combine(directory, "fixed.ini");
            File.WriteAllText(constants, "[Core]\nOutputFileJpegQuality=80\n");
            try
            {
                var config = new CoreConfigurationImpl();
                using var ini = IniConfigRegistry.ForFile(Path.Combine(directory, "settings.ini")).RegisterSection(config).AddConstantsFile(constants).Build().Load();
                Assert.True(config.IsConstant(nameof(config.OutputFileJpegQuality)));
                string originalFormat = config.OutputFileFormat;
                Assert.Throws<InvalidOperationException>(() => ExportProfile.Defaults()[2].Apply(config));
                Assert.Equal(originalFormat, config.OutputFileFormat);
            }
            finally
            {
                IniConfigRegistry.Unregister(Path.Combine(directory, "settings.ini"));
                File.Delete(constants);
                if (File.Exists(Path.Combine(directory, "settings.ini"))) File.Delete(Path.Combine(directory, "settings.ini"));
                Directory.Delete(directory);
            }
        }

        [Fact]
        public void EncodedProfilesSurviveIniWriterAndParser()
        {
            var config = new CoreConfigurationImpl();
            config.OutputExportProfiles = ExportProfile.Serialize(new System.Collections.Generic.List<ExportProfile> { new ExportProfile { Name = "Name = ; # café" } });
            var ini = new IniFile();
            string key = nameof(config.OutputExportProfiles);
            ini.GetOrAddSection(config.SectionName).SetValue(key, config.GetRawValue(key));
            var parsed = IniFileParser.Parse(IniFileWriter.WriteToString(ini, new IniWriterOptions()), new IniParserOptions());
            var reloaded = new CoreConfigurationImpl();
            reloaded.SetRawValue(key, parsed.GetSection(config.SectionName).GetValue(key));
            Assert.Equal("Name = ; # café", ExportProfile.Deserialize(reloaded.OutputExportProfiles).Single().Name);
        }

        [Fact]
        public void ProfileCountAndNameLimitsAreEnforced()
        {
            Assert.Throws<ArgumentException>(() => new ExportProfile { Name = new string('x', 65) }.Validate());
            Assert.Throws<ArgumentException>(() => ExportProfile.Serialize(Enumerable.Range(0, 21).Select(i => new ExportProfile { Name = "Profile " + i }).ToList()));
        }

        [Fact]
        public void RoundTripRestoresAllSettingsAndLeavesPathsAndUiUnchanged()
        {
            var source = new CoreConfigurationImpl
            {
                OutputFileFormat = "webp", OutputFileDpiPreset = ExportDpiPreset.Custom, OutputFileCustomDpi = 600,
                OutputFileJpegQuality = 92, OutputFileWebpQuality = 65, OutputFileWebpLossless = true,
                OutputFileReduceColors = true, OutputFileAutoReduceColors = true, OutputFilePromptQuality = true
            };
            var profile = ExportProfile.Capture("My export", source);
            var encoded = ExportProfile.Serialize(new System.Collections.Generic.List<ExportProfile> { profile });
            Assert.DoesNotContain("\n", encoded);
            var target = new CoreConfigurationImpl { OutputFilePath = "unchanged", UiScalePercent = 150 };
            ExportProfile.Deserialize(encoded).Single().Apply(target);
            Assert.Equal("webp", target.OutputFileFormat);
            Assert.Equal(600, target.OutputFileCustomDpi);
            Assert.Equal(ExportDpiPreset.Custom, target.OutputFileDpiPreset);
            Assert.Equal(92, target.OutputFileJpegQuality);
            Assert.Equal(65, target.OutputFileWebpQuality);
            Assert.True(target.OutputFileWebpLossless && target.OutputFileReduceColors && target.OutputFileAutoReduceColors && target.OutputFilePromptQuality);
            Assert.Equal("unchanged", target.OutputFilePath);
            Assert.Equal(150, target.UiScalePercent);
        }

        [Theory]
        [InlineData(0, 80)] [InlineData(2401, 80)] [InlineData(96, -1)] [InlineData(96, 101)]
        public void InvalidProfileCannotPartiallyApply(int dpi, int quality)
        {
            var config = new CoreConfigurationImpl { OutputFileFormat = "png" };
            var profile = new ExportProfile { Name = "Invalid", Format = "webp", CustomDpi = dpi, WebpQuality = quality };
            Assert.Throws<ArgumentException>(() => profile.Apply(config));
            Assert.Equal("png", config.OutputFileFormat);
        }

        [Fact]
        public void BuiltInsProvideExplicitChoices()
        {
            var profiles = ExportProfile.Defaults();
            Assert.Equal(3, profiles.Count);
            Assert.All(profiles, p => { Assert.True(p.BuiltIn); p.Validate(); });
            Assert.Equal(ExportDpiPreset.Web, profiles[0].DpiPreset);
            Assert.Equal("jpg", profiles[1].Format);
            Assert.Equal(ExportDpiPreset.Print, profiles[1].DpiPreset);
            Assert.Equal("webp", profiles[2].Format);
            Assert.True(profiles[2].WebpLossless);
        }

        [Fact]
        public void DuplicateAndReservedNamesAreRejected()
        {
            Assert.Throws<ArgumentException>(() => ExportProfile.Serialize(new System.Collections.Generic.List<ExportProfile>
                { new ExportProfile { Name = "One" }, new ExportProfile { Name = "one" } }));
            Assert.Throws<ArgumentException>(() => ExportProfile.Serialize(new System.Collections.Generic.List<ExportProfile>
                { new ExportProfile { Name = "Web PNG" } }));
        }

        [Theory]
        [InlineData("")] [InlineData(" ")] [InlineData("bad\nname")]
        public void InvalidNameRejected(string name)
        {
            Assert.Throws<ArgumentException>(() => new ExportProfile { Name = name }.Validate());
        }

        [Fact]
        public void MalformedAndOversizedStorageRejected()
        {
            Assert.Throws<FormatException>(() => ExportProfile.Deserialize("not base64!"));
            Assert.Throws<ArgumentException>(() => ExportProfile.Deserialize(new string('A', 65537)));
            Assert.Throws<InvalidOperationException>(() => ExportProfile.Deserialize(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("<!DOCTYPE x [<!ENTITY a SYSTEM 'file:///never-read'>]><x>&a;</x>"))));
            Assert.Empty(ExportProfile.Deserialize(null));
        }
    }
}
