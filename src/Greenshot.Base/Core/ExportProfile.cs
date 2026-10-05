/*
 * Greenshot - a free and open source screenshot tool
 * Copyright (C) 2007-2026 Thomas Braun, Jens Klingen, Robin Krom
 * Licensed under the GNU General Public License, version 1 or later.
 * See https://www.gnu.org/licenses/ and https://getgreenshot.org/.
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;
using Dapplo.Ini;
using Greenshot.Base.Core.Enums;
using Greenshot.Base.Interfaces.Plugin;

namespace Greenshot.Base.Core
{
    public class ExportProfile
    {
        public string Name { get; set; }
        public string Format { get; set; } = "png";
        public ExportDpiPreset DpiPreset { get; set; } = ExportDpiPreset.Preserve;
        public int CustomDpi { get; set; } = 96;
        public int JpegQuality { get; set; } = 80;
        public bool WebpLossless { get; set; }
        public int WebpQuality { get; set; } = 80;
        public bool ReduceColors { get; set; }
        public bool AutoReduceColors { get; set; }
        public bool PromptQuality { get; set; }
        [XmlIgnore] public bool BuiltIn { get; set; }

        public static List<ExportProfile> Defaults() => new List<ExportProfile>
        {
            new ExportProfile { Name = "Web PNG", DpiPreset = ExportDpiPreset.Web, BuiltIn = true },
            new ExportProfile { Name = "Print JPEG", Format = "jpg", DpiPreset = ExportDpiPreset.Print, JpegQuality = 90, BuiltIn = true },
            new ExportProfile { Name = "Lossless WebP", Format = "webp", WebpLossless = true, BuiltIn = true }
        };

        public static ExportProfile Capture(string name, ICoreConfiguration config)
        {
            var result = new ExportProfile
            {
                Name = name?.Trim(), Format = config.OutputFileFormat,
                DpiPreset = config.OutputFileDpiPreset, CustomDpi = config.OutputFileCustomDpi,
                JpegQuality = config.OutputFileJpegQuality, WebpLossless = config.OutputFileWebpLossless,
                WebpQuality = config.OutputFileWebpQuality, ReduceColors = config.OutputFileReduceColors,
                AutoReduceColors = config.OutputFileAutoReduceColors, PromptQuality = config.OutputFilePromptQuality
            };
            result.Validate();
            return result;
        }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Name) || Name != Name.Trim() || Name.Length > 64 || Name.Any(char.IsControl))
                throw new ArgumentException("Use a profile name from 1 to 64 characters without control characters.");
            if (string.IsNullOrWhiteSpace(Format) || Format.Length > 32 || Format.Any(char.IsControl))
                throw new ArgumentException("Invalid profile image format.");
            if (!Enum.IsDefined(typeof(ExportDpiPreset), DpiPreset) || !ExportDpiSettings.IsValidCustomDpi(CustomDpi))
                throw new ArgumentException("Profile DPI must be from 1 to 2400 with a valid preset.");
            if (JpegQuality < 0 || JpegQuality > 100 || WebpQuality < 0 || WebpQuality > 100)
                throw new ArgumentException("Profile quality must be from 0 to 100.");
        }

        public void Apply(ICoreConfiguration config)
        {
            Validate();
            string[] properties = { nameof(config.OutputFileFormat), nameof(config.OutputFileDpiPreset),
                nameof(config.OutputFileCustomDpi), nameof(config.OutputFileJpegQuality), nameof(config.OutputFileWebpLossless),
                nameof(config.OutputFileWebpQuality), nameof(config.OutputFileReduceColors),
                nameof(config.OutputFileAutoReduceColors), nameof(config.OutputFilePromptQuality) };
            if (properties.Any(config.IsConstant)) throw new InvalidOperationException("A fixed export setting prevents applying this profile.");
            config.OutputFileFormat = Format;
            config.OutputFileDpiPreset = DpiPreset;
            config.OutputFileCustomDpi = CustomDpi;
            config.OutputFileJpegQuality = JpegQuality;
            config.OutputFileWebpLossless = WebpLossless;
            config.OutputFileWebpQuality = WebpQuality;
            config.OutputFileReduceColors = ReduceColors;
            config.OutputFileAutoReduceColors = AutoReduceColors;
            config.OutputFilePromptQuality = PromptQuality;
        }

        public SurfaceOutputSettings CreateOutputSettings()
        {
            Validate();
            return new SurfaceOutputSettings(Format)
            {
                ExportDpiPreset = DpiPreset, CustomDpi = CustomDpi,
                JPGQuality = JpegQuality, WebpLossless = WebpLossless, WebpQuality = WebpQuality,
                ReduceColors = ReduceColors, AutoReduceColors = AutoReduceColors
            };
        }

        public static string Serialize(List<ExportProfile> profiles)
        {
            ValidateList(profiles);
            var serializer = new XmlSerializer(typeof(List<ExportProfile>));
            using var stream = new MemoryStream();
            serializer.Serialize(stream, profiles);
            return Convert.ToBase64String(stream.ToArray());
        }

        public static List<ExportProfile> Deserialize(string encoded)
        {
            if (string.IsNullOrEmpty(encoded)) return new List<ExportProfile>();
            if (encoded.Length > 65536) throw new ArgumentException("Profile storage is too large.");
            using var stream = new MemoryStream(Convert.FromBase64String(encoded));
            using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 65536 });
            var profiles = (List<ExportProfile>)new XmlSerializer(typeof(List<ExportProfile>)).Deserialize(reader);
            ValidateList(profiles);
            return profiles;
        }

        private static void ValidateList(List<ExportProfile> profiles)
        {
            if (profiles == null || profiles.Count > 20) throw new ArgumentException("Save at most 20 custom export profiles.");
            foreach (var profile in profiles) { if (profile == null) throw new ArgumentException("Invalid profile."); profile.Validate(); }
            if (profiles.Select(p => p.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != profiles.Count ||
                profiles.Any(p => Defaults().Any(d => string.Equals(d.Name, p.Name, StringComparison.OrdinalIgnoreCase))))
                throw new ArgumentException("Profile names must be unique and differ from built-in names.");
        }
    }
}
