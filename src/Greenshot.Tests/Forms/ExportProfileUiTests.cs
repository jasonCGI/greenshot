/*
 * Greenshot - a free and open source screenshot tool
 * Copyright (C) 2007-2026 Thomas Braun, Jens Klingen, Robin Krom
 * Licensed under the GNU General Public License, version 1 or later.
 */
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Greenshot.Base.Core;
using Greenshot.Base.Wpf;
using Greenshot.Forms.Wpf;
using Xunit;

namespace Greenshot.Tests.Forms
{
    [Collection(TestCollections.WpfThemeState)]
    public class ExportProfileUiTests
    {
        public ExportProfileUiTests() { TestEnvironment.EnsureInitialized(); }

        [Fact]
        public void SaveApplyDeleteAndReopenPersistCustomSettings()
        {
            var model = new SettingsViewModel();
            var config = model.CoreConfiguration;
            string storage = config.OutputExportProfiles;
            var original = ExportProfile.Capture("Original", config);
            try
            {
                config.OutputExportProfiles = "";
                model = new SettingsViewModel { ExportProfileName = "My WebP" };
                config.OutputFileFormat = "webp";
                config.OutputFileWebpLossless = true;
                config.OutputFileWebpQuality = 47;
                model.SaveExportProfile();
                Assert.Contains("Saved", model.ExportProfileStatus);
                var reopened = new SettingsViewModel();
                reopened.SelectedExportProfile = reopened.ExportProfiles.Single(p => p.Name == "My WebP");
                config.OutputFileFormat = "png";
                config.OutputFileWebpLossless = false;
                reopened.ApplyExportProfile();
                Assert.Equal("webp", config.OutputFileFormat);
                Assert.True(config.OutputFileWebpLossless);
                Assert.Equal(47, config.OutputFileWebpQuality);
                reopened.DeleteExportProfile();
                Assert.DoesNotContain(new SettingsViewModel().ExportProfiles, p => p.Name == "My WebP");
                reopened.SelectedExportProfile = reopened.ExportProfiles.First();
                reopened.DeleteExportProfile();
                Assert.Equal(3, reopened.ExportProfiles.Count);
            }
            finally { original.Apply(config); config.OutputExportProfiles = storage; }
        }

        [Fact]
        public void CorruptStorageIsKeptAndCannotBeOverwritten()
        {
            var config = new SettingsViewModel().CoreConfiguration;
            string original = config.OutputExportProfiles;
            try
            {
                config.OutputExportProfiles = "bad-storage!";
                var model = new SettingsViewModel { ExportProfileName = "Replacement" };
                model.SaveExportProfile();
                Assert.Equal("bad-storage!", config.OutputExportProfiles);
                Assert.Contains("Repair", model.ExportProfileStatus);
            }
            finally { config.OutputExportProfiles = original; }
        }

        [Theory]
        [InlineData(100)] [InlineData(125)] [InlineData(150)] [InlineData(200)]
        public void OutputProfilesRenderAtEachUiSize(int percent)
        {
            Exception error = null;
            var thread = new Thread(() =>
            {
                SettingsWindow window = null;
                int original = 100;
                ICoreConfiguration config = null;
                try
                {
                    window = new SettingsWindow(initialTabName: "output");
                    config = ((SettingsViewModel)window.DataContext).CoreConfiguration;
                    original = config.UiScalePercent;
                    config.UiScalePercent = percent;
                    UiScaleManager.Attach(window, config);
                    var root = (FrameworkElement)window.Content;
                    var size = new Size(1100, 850);
                    root.Measure(size); root.Arrange(new Rect(size)); root.UpdateLayout();
                    Dispatcher.CurrentDispatcher.Invoke(new Action(() => { }), DispatcherPriority.ContextIdle);
                    Assert.Equal(3, window.ExportProfileInput.Items.Count);
                    Assert.True(window.ExportProfileInput.ActualWidth > 100);
                    Assert.True(window.ExportProfileNameInput.ActualWidth > 100);
                    string output = Environment.GetEnvironmentVariable("GREENSHOT_UI_QA_DIRECTORY");
                    if (!string.IsNullOrEmpty(output))
                    {
                        Directory.CreateDirectory(output);
                        var image = new RenderTargetBitmap(1100, 850, 96, 96, PixelFormats.Pbgra32);
                        var background = new DrawingVisual();
                        using (var draw = background.RenderOpen()) draw.DrawRectangle(window.Background ?? Brushes.White, null, new Rect(size));
                        image.Render(background); image.Render(root);
                        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
                        using var stream = File.Create(Path.Combine(output, $"output-profiles-{percent}.png"));
                        encoder.Save(stream);
                    }
                }
                catch (Exception exception) { error = exception; }
                finally { window?.Close(); if (config != null) config.UiScalePercent = original; Dispatcher.CurrentDispatcher.InvokeShutdown(); }
            }) { IsBackground = true };
            thread.SetApartmentState(ApartmentState.STA); thread.Start();
            Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "Output layout check timed out.");
            if (error != null) throw error;
        }
    }
}
