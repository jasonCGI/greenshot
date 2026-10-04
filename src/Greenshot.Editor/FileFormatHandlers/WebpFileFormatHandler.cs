/*
 * Greenshot - a free and open source screenshot tool
 * Copyright (C) 2007-2026 Thomas Braun, Jens Klingen, Robin Krom
 * Licensed under the GNU General Public License, version 1 or later.
 * See https://www.gnu.org/licenses/ and https://getgreenshot.org/.
 */

using System;
using System.Drawing;
using System.IO;
using Greenshot.Base.Core.FileFormat;
using Greenshot.Base.Interfaces;
using Greenshot.Base.Interfaces.Plugin;

namespace Greenshot.Editor.FileFormatHandlers
{
    /// <summary>Expose WebP without switching the encoders of existing formats into beta mode.</summary>
    public sealed class WebpFileFormatHandler : ImageSharpFileFormatHandler
    {
        public WebpFileFormatHandler()
        {
            foreach (FileFormatHandlerActions action in new[]
            {
                FileFormatHandlerActions.LoadDrawableFromStream, FileFormatHandlerActions.LoadFromStream,
                FileFormatHandlerActions.SaveToStream, FileFormatHandlerActions.SaveToFile,
                FileFormatHandlerActions.LoadFromFile
            })
            {
                SupportedExtensions[action] = new[] { ".webp" };
            }
        }

        public override void RegisterFileFormats(IFileFormatRegistry registry)
        {
            RegisterFileFormat(registry, WellKnownFileFormats.Webp, [".webp"], [".webp"], "webp", "image/webp", null, "WebP");
        }

        public override bool TrySaveToStream(Bitmap bitmap, Stream destination, string extension,
            ISurface surface = null, SurfaceOutputSettings surfaceOutputSettings = null)
        {
            return string.Equals(extension, ".webp", StringComparison.OrdinalIgnoreCase) &&
                base.TrySaveToStream(bitmap, destination, ".webp", surface, surfaceOutputSettings);
        }

        public override bool TryLoadFromStream(Stream stream, string extension, out Bitmap bitmap)
        {
            bitmap = null;
            return string.Equals(extension, ".webp", StringComparison.OrdinalIgnoreCase) &&
                base.TryLoadFromStream(stream, ".webp", out bitmap);
        }
    }
}
