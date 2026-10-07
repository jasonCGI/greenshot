/* Greenshot, GNU General Public License version 1 or later. */
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Dapplo.Ini;
using Greenshot.Base.Core;
using Greenshot.Base.Core.Export;
using Greenshot.Base.Interfaces;
using Greenshot.Base.Interfaces.Plugin;
using Greenshot.Destinations;
using Greenshot.Editor.Drawing;
using Greenshot.Tests.Threading;
using Xunit;

namespace Greenshot.Tests.Core
{
    [Collection("Export profile policy")]
    public class ExportWorkflowTests
    {
        public ExportWorkflowTests() { TestEnvironment.EnsureInitialized(); }
        [Theory]
        [InlineData("filename")] [InlineData("review")]
        public async Task CancelPreservesExistingFileAndDefaults(string stage)
        {
            string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".png");
            File.WriteAllText(path, "existing file");
            using var dispatcher = StrictTestUiDispatcher.Create();
            var surface = await dispatcher.InvokeAsync(() => new Surface(new Bitmap(24, 16)));
            try
            {
                using var source = new SurfaceExportSource(surface, dispatcher, false);
                var config = IniConfigRegistry.GetSection<ICoreConfiguration>();
                var before = ExportProfile.Capture("Before", config);
                var ui = new SaveInteraction { Filename = stage == "filename" ? null : path, CancelReview = true };
                var result = await FileDestination.SaveWithDialogAsync(new ExportRequest(source, null, true, ui), false,
                    CancellationToken.None, ExportProfile.Defaults()[1], forceReview: true);
                Assert.Null(result); Assert.Equal("existing file", File.ReadAllText(path));
                Assert.Equal(before.Format, config.OutputFileFormat); Assert.Equal(before.DpiPreset, config.OutputFileDpiPreset);
                Assert.Equal(stage == "filename" ? 0 : 1, ui.Reviews);
            }
            finally { await dispatcher.InvokeAsync(() => surface.Dispose()); File.Delete(path); }
        }

        [Theory]
        [InlineData("png")] [InlineData("jpg")] [InlineData("webp")]
        public async Task ReviewUsesFinalChosenFormatAndOverwriteKeepsPixels(string extension)
        {
            string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + "." + extension);
            File.WriteAllText(path, "existing file");
            using var dispatcher = StrictTestUiDispatcher.Create();
            var surface = await dispatcher.InvokeAsync(() => new Surface(new Bitmap(24, 16)));
            try
            {
                using var source = new SurfaceExportSource(surface, dispatcher, false);
                var config = IniConfigRegistry.GetSection<ICoreConfiguration>();
                var before = ExportProfile.Capture("Before", config);
                var ui = new SaveInteraction { Filename = path };
                string result = await FileDestination.SaveWithDialogAsync(new ExportRequest(source, null, true, ui), false,
                    CancellationToken.None, ExportProfile.Defaults()[1], forceReview: true);
                Assert.Equal(path, result); Assert.Equal("jpg", ui.PreferredFormat);
                Assert.Equal(extension, ui.ReviewSettings.Format); Assert.Equal(path, ui.ReviewSettings.PreviewFileName);
                Assert.Equal(new Size(24, 16), ui.ReviewSettings.PreviewSize); Assert.Equal(1, ui.Reviews);
                Assert.True(new FileInfo(path).Length > 20);
                Assert.Equal(before.Format, config.OutputFileFormat); Assert.Equal(before.DpiPreset, config.OutputFileDpiPreset);
                if (extension != "webp")
                {
                    using var image = Image.FromFile(path);
                    Assert.Equal(new Size(24, 16), image.Size); Assert.InRange(image.HorizontalResolution, 299.9f, 300.1f);
                }
            }
            finally { await dispatcher.InvokeAsync(() => surface.Dispose()); File.Delete(path); }
        }

        [Fact]
        public async Task AutomaticPaletteProfilesHaveSeparateCachedEncodings()
        {
            using var dispatcher = StrictTestUiDispatcher.Create();
            var surface = await dispatcher.InvokeAsync(() => new Surface(new Bitmap(24, 16, PixelFormat.Format24bppRgb)));
            try
            {
                using var source = new SurfaceExportSource(surface, dispatcher, false);
                var plain = new SurfaceOutputSettings("png") { SaveBackgroundOnly = true, AutoReduceColors = false, ReduceColors = false };
                var palette = new SurfaceOutputSettings("png") { SaveBackgroundOnly = true, AutoReduceColors = true, ReduceColors = false };
                var first = await source.EncodeAsync(plain, CancellationToken.None);
                var second = await source.EncodeAsync(palette, CancellationToken.None);
                Assert.NotSame(first, second);
                Assert.Same(first, await source.EncodeAsync(plain, CancellationToken.None));
                Assert.Same(second, await source.EncodeAsync(palette, CancellationToken.None));
            }
            finally { await dispatcher.InvokeAsync(() => surface.Dispose()); }
        }

        private sealed class SaveInteraction : IUserInteraction
        {
            public string Filename; public bool CancelReview; public int Reviews; public string PreferredFormat;
            public SurfaceOutputSettings ReviewSettings;
            public bool IsInteractive => true;
            public Task<string> PickSaveFileAsync(SaveFileRequest request, CancellationToken cancellationToken)
            { PreferredFormat = request.PreferredFormat; return Task.FromResult(Filename); }
            public Task<SurfaceOutputSettings> PromptOutputSettingsAsync(SurfaceOutputSettings current, CancellationToken cancellationToken)
            { Reviews++; ReviewSettings = current; return Task.FromResult(CancelReview ? null : current); }
            public Task NotifyAsync(Notification notification) => Task.CompletedTask;
            public Task<IDestination> PickDestinationAsync(IReadOnlyList<IDestination> choices, ICaptureDetails metadata, CancellationToken cancellationToken) => throw new InvalidOperationException();
            public Task<TResult> ShowDialogAsync<TResult>(IDialogViewModel<TResult> model, CancellationToken cancellationToken) => throw new InvalidOperationException();
            public Task<T> RunWithProgressAsync<T>(string title, Func<IProgress<ProgressInfo>, CancellationToken, Task<T>> work, CancellationToken cancellationToken) => work(null, cancellationToken);
            public Task<bool?> ConfirmAsync(string title, string message, bool isError, CancellationToken cancellationToken) => throw new InvalidOperationException(message);
        }
    }
}
