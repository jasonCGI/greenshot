/*
 * Greenshot - a free and open source screenshot tool
 * Copyright (C) 2007-2026 Thomas Braun, Jens Klingen, Robin Krom
 * Licensed under the GNU General Public License, version 1 or later.
 */
using System;
using Dapplo.Ini;
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
        public void EditRenameDuplicateAndImportAreTransactionalAndKeepDefaults()
        {
            var model = new SettingsViewModel(); var config = model.CoreConfiguration;
            string stored = config.OutputExportProfiles;
            string format = config.OutputFileFormat; var dpi = config.OutputFileDpiPreset;
            try
            {
                config.OutputExportProfiles = ""; model = new SettingsViewModel { ExportProfileName = "My print" };
                model.SelectedExportProfile = model.ExportProfiles.Single(p => p.Name == "Print JPEG");
                model.DuplicateExportProfile(); Assert.Equal(4, model.ExportProfiles.Count);
                var edited = model.SelectedExportProfile.Copy("My print"); edited.JpegQuality = 42;
                model.UpdateExportProfile(edited); Assert.Equal(42, model.SelectedExportProfile.JpegQuality);
                Assert.Contains("quality 42", model.ExportProfileSummary);
                model.ExportProfileName = "Renamed print"; model.RenameExportProfile();
                Assert.Equal("Renamed print", model.SelectedExportProfile.Name);
                string document = model.ExportProfileDocument(); string before = config.OutputExportProfiles;
                model.ImportExportProfiles(document); Assert.Equal(before, config.OutputExportProfiles); Assert.Equal(4, model.ExportProfiles.Count);
                config.OutputExportProfiles = ""; model = new SettingsViewModel(); model.ImportExportProfiles(document);
                Assert.Equal(42, model.ExportProfiles.Single(p => p.Name == "Renamed print").JpegQuality);
                Assert.Equal(format, config.OutputFileFormat); Assert.Equal(dpi, config.OutputFileDpiPreset);
            }
            finally { config.OutputExportProfiles = stored; }
        }

        [Fact]
        public void CapturePickerShowsTemporaryProfilesAndSaveDensity()
        {
            Exception error = null;
            var thread = new Thread(() =>
            {
                Greenshot.UI.DynamicDestinationWindow window = null;
                try
                {
                    using var preview = new System.Drawing.Bitmap(1200, 800);
                    using (var graphics = System.Drawing.Graphics.FromImage(preview)) graphics.Clear(System.Drawing.Color.FromArgb(240, 244, 248));
                    var choices = new System.Collections.Generic.List<Greenshot.Base.Interfaces.IDestination>
                    { new Greenshot.Destinations.FileWithDialogDestination(), new Greenshot.Destinations.FileWithDialogDestination(review: true) };
                    choices.AddRange(Greenshot.Destinations.ProfileFileDestination.GetChoices(IniConfigRegistry.GetSection<ICoreConfiguration>()));
                    window = new Greenshot.UI.DynamicDestinationWindow("Choose output", preview, choices);
                    Assert.Contains(window.OtherDestinationTiles, t => t.Title.Contains("Web PNG"));
                    Assert.Contains(window.OtherDestinationTiles, t => t.Subtitle.Contains("300 DPI"));
                    Assert.Contains(window.CoreDestinationTiles, t => t.Subtitle.Contains("DPI"));
                    var root = (FrameworkElement)window.Content;
                    var size = new Size(1100, 850);
                    root.Measure(size); root.Arrange(new Rect(size)); root.UpdateLayout();
                    Dispatcher.CurrentDispatcher.Invoke(new Action(() => { }), DispatcherPriority.ContextIdle);
                    Assert.True(HasVisibleDpi(root), "Save density must be visible in the rendered template.");
                    string output = Environment.GetEnvironmentVariable("GREENSHOT_UI_QA_DIRECTORY");
                    if (!string.IsNullOrEmpty(output))
                    {
                        Directory.CreateDirectory(output);
                        var image = new RenderTargetBitmap(1100, 850, 96, 96, PixelFormats.Pbgra32);
                        var background = new DrawingVisual();
                        using (var draw = background.RenderOpen()) draw.DrawRectangle(window.Background ?? Brushes.White, null, new Rect(size));
                        image.Render(background); image.Render(root);
                        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
                        using var stream = File.Create(Path.Combine(output, "capture-profile-picker.png"));
                        encoder.Save(stream);
                    }
                }
                catch (Exception ex) { error = ex; }
                finally { window?.Close(); Dispatcher.CurrentDispatcher.InvokeShutdown(); }
            });
            thread.SetApartmentState(ApartmentState.STA); thread.Start();
            Assert.True(thread.Join(TimeSpan.FromSeconds(30)));
            if (error != null) throw new Exception("Capture picker check failed", error);
        }

        private static bool HasVisibleDpi(DependencyObject root)
        {
            if (root is System.Windows.Controls.TextBlock text && text.Visibility == Visibility.Visible &&
                text.ActualHeight > 0 && text.Text.Contains("DPI")) return true;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
                if (HasVisibleDpi(VisualTreeHelper.GetChild(root, i))) return true;
            return false;
        }

        [Theory]
        [InlineData(100)] [InlineData(125)] [InlineData(150)] [InlineData(200)]
        public void ProfileEditorAndSaveReviewRenderAtEachSize(int percent)
        {
            Exception error = null;
            var thread = new Thread(() =>
            {
                Window editor = null; Window review = null;
                var config = IniConfigRegistry.GetSection<ICoreConfiguration>(); int original = config.UiScalePercent;
                try
                {
                    config.UiScalePercent = percent;
                    var profile = ExportProfile.Defaults()[1].Copy("My print profile");
                    editor = new ExportProfileEditorWindow(profile);
                    var settings = profile.CreateOutputSettings(); settings.PreviewFileName = @"C:\Screenshots\review-example.jpg";
                    settings.PreviewSize = new System.Drawing.Size(1200, 800); settings.PreviewDpiX = 144; settings.PreviewDpiY = 144;
                    review = new QualityWindow(settings); UiScaleManager.Attach(review, config);
                    RenderDialog(editor, "profile-editor", percent);
                    RenderDialog(review, "review-save", percent);
                    Assert.Null(((ExportProfileEditorWindow)editor).Result);
                    Assert.Equal(90, profile.JpegQuality);
                    Assert.True(HasVisibleDpi((DependencyObject)review.Content));
                }
                catch (Exception ex) { error = ex; }
                finally { editor?.Close(); review?.Close(); config.UiScalePercent = original; Dispatcher.CurrentDispatcher.InvokeShutdown(); }
            }) { IsBackground = true };
            thread.SetApartmentState(ApartmentState.STA); thread.Start();
            Assert.True(thread.Join(TimeSpan.FromSeconds(30)));
            if (error != null) throw error;
        }

        private static void RenderDialog(Window window, string name, int percent)
        {
            var root = (FrameworkElement)window.Content; var size = new Size(700, 1100);
            root.Measure(size); root.Arrange(new Rect(size)); root.UpdateLayout();
            Dispatcher.CurrentDispatcher.Invoke(new Action(() => { }), DispatcherPriority.ContextIdle);
            string output = Environment.GetEnvironmentVariable("GREENSHOT_UI_QA_DIRECTORY");
            if (string.IsNullOrEmpty(output)) return;
            Directory.CreateDirectory(output);
            var image = new RenderTargetBitmap(700, 1100, 96, 96, PixelFormats.Pbgra32);
            var background = new DrawingVisual(); using (var draw = background.RenderOpen()) draw.DrawRectangle(window.Background ?? Brushes.White, null, new Rect(size));
            image.Render(background); image.Render(root);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
            using var stream = File.Create(Path.Combine(output, $"{name}-{percent}.png")); encoder.Save(stream);
        }

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
                    window.ManageExportProfiles.IsExpanded = true;
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
