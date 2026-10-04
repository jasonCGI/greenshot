/*
 * Greenshot - a free and open source screenshot tool
 * Copyright (C) 2007-2026 Thomas Braun, Jens Klingen, Robin Krom
 * Licensed under the GNU General Public License, version 1 or later.
 * See https://www.gnu.org/licenses/ and https://getgreenshot.org/.
 */

using System;
using System.Linq;
using System.Threading;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Dapplo.Ini;
using Dapplo.Ini.Parsing;
using Greenshot.Base.Controls;
using Greenshot.Base.Core;
using Greenshot.Base.Wpf;
using Greenshot.Forms.Wpf;
using Greenshot.Editor.Drawing;
using Xunit;
using WinForms = System.Windows.Forms;

namespace Greenshot.Tests.Forms
{
    [Collection(TestCollections.WpfThemeState)]
    public class UiScaleTests
    {
        public UiScaleTests() => TestEnvironment.EnsureInitialized();

        [Theory]
        [InlineData(100, 1.0)]
        [InlineData(125, 1.25)]
        [InlineData(150, 1.5)]
        [InlineData(200, 2.0)]
        [InlineData(0, 1.0)]
        [InlineData(-100, 1.0)]
        [InlineData(9999, 1.0)]
        public void InvalidSavedSizesFallBackAndSupportedSizesAreExact(int value, double factor)
        {
            Assert.Equal(factor, UiScaleSettings.Factor(value));
            var config = new CoreConfigurationImpl();
            config.ResetToDefaults();
            Assert.Equal(100, config.UiScalePercent);
        }

        [Theory]
        [InlineData(100)]
        [InlineData(125)]
        [InlineData(150)]
        [InlineData(200)]
        public void UiSizeSurvivesIniRoundTrip(int value)
        {
            var config = new CoreConfigurationImpl();
            config.ResetToDefaults();
            config.UiScalePercent = value;
            string key = nameof(ICoreConfiguration.UiScalePercent);
            var ini = new IniFile();
            foreach (var entry in config.GetAllRawValues().Where(entry => entry.Key == key))
                ini.GetOrAddSection(config.SectionName).SetValue(entry.Key, entry.Value);
            var parsed = IniFileParser.Parse(IniFileWriter.WriteToString(ini, new IniWriterOptions()), new IniParserOptions());
            var reloaded = new CoreConfigurationImpl();
            reloaded.ResetToDefaults();
            reloaded.SetRawValue(key, parsed.GetSection(config.SectionName).GetValue(key));
            Assert.Equal(value, reloaded.UiScalePercent);
        }

        [Fact]
        public void WpfLiveChangesDoNotAccumulateAndResetRestoresOriginalContent()
        {
            RunOnSta(() =>
            {
                var config = new CoreConfigurationImpl();
                config.ResetToDefaults();
                var content = new Grid();
                var text = new TextBlock { Text = "Preview", FontSize = 14 };
                content.Children.Add(text);
                var original = content.LayoutTransform;
                var window = new Window { Content = content, Width = 400, Height = 300, MinWidth = 200, MinHeight = 150 };
                try
                {
                    UiScaleManager.Attach(window, config);
                    foreach (int size in new[] { 200, 125, 150, 200 })
                    {
                        config.UiScalePercent = size;
                        var group = Assert.IsType<TransformGroup>(content.LayoutTransform);
                        Assert.Equal(size / 100.0, Assert.IsType<ScaleTransform>(group.Children.Last()).ScaleX);
                        Assert.IsType<ScrollViewer>(window.Content);
                        Assert.Equal(14, text.FontSize);
                        Assert.True(window.Width <= window.MaxWidth);
                        Assert.True(window.Height <= window.MaxHeight);
                    }
                    config.UiScalePercent = 100;
                    Assert.Same(content, Assert.IsType<ScrollViewer>(window.Content).Content);
                    Assert.Same(original, content.LayoutTransform);
                    Assert.Equal(400, content.Width);
                    Assert.Equal(400, window.Width);
                    Assert.Equal(300, window.Height);
                }
                finally { window.Close(); }
            });
        }

        [Fact]
        public void AutoSizedDialogGetsScaledContentAndScreenBounds()
        {
            RunOnSta(() =>
            {
                var config = new CoreConfigurationImpl();
                config.ResetToDefaults();
                config.UiScalePercent = 200;
                var content = new StackPanel { Width = 400 };
                content.Children.Add(new Button { Content = "OK" });
                var window = new Window { Content = content, SizeToContent = SizeToContent.WidthAndHeight };
                try
                {
                    UiScaleManager.Attach(window, config);
                    window.Measure(new Size(1000, 1000));
                    Assert.Equal(SizeToContent.WidthAndHeight, window.SizeToContent);
                    Assert.IsType<ScrollViewer>(window.Content);
                    Assert.Equal(400, content.Width);
                    Assert.True(window.MaxWidth > 0 && window.MaxHeight > 0);
                }
                finally { window.Close(); }
            });
        }

        [Fact]
        public void WinFormsChromeScalesWithoutChangingCanvasOrCompounding()
        {
            RunOnSta(() =>
            {
                var config = new CoreConfigurationImpl();
                config.ResetToDefaults();
                using var form = new WinForms.Form();
                using var strip = new WinForms.ToolStrip();
                var button = new WinForms.ToolStripButton("Save");
                strip.Items.Add(button);
                var bitmap = new System.Drawing.Bitmap(641, 479);
                bitmap.SetResolution(144, 72);
                bitmap.SetPixel(1, 1, System.Drawing.Color.Red);
                using var canvas = new Surface(bitmap);
                form.Controls.Add(canvas);
                form.Controls.Add(strip);
                float originalFont = strip.Font.Size;
                var originalImages = strip.ImageScalingSize;
                var originalCanvas = canvas.Size;
                var originalZoom = canvas.ZoomFactor;
                using var scaling = new UiChromeScale(form, config);
                foreach (int size in new[] { 200, 125, 150, 200, 100 })
                {
                    config.UiScalePercent = size;
                    Assert.Equal(originalFont * size / 100f, strip.Font.Size, 3);
                    Assert.Equal((int)Math.Round(originalImages.Width * size / 100.0), strip.ImageScalingSize.Width);
                    Assert.Equal(originalCanvas, canvas.Size);
                    Assert.Equal(originalZoom, canvas.ZoomFactor);
                    Assert.Equal(641, canvas.Image.Width);
                    Assert.Equal(479, canvas.Image.Height);
                    Assert.Equal(144, canvas.Image.HorizontalResolution);
                    Assert.Equal(72, canvas.Image.VerticalResolution);
                    Assert.Equal(System.Drawing.Color.Red.ToArgb(), ((System.Drawing.Bitmap)canvas.Image).GetPixel(1, 1).ToArgb());
                }
                var lateButton = new WinForms.ToolStripButton("New destination");
                strip.Items.Add(lateButton);
                config.UiScalePercent = 200;
                Assert.Equal(originalFont * 2, lateButton.Font.Size, 3);
                scaling.Dispose();
                config.UiScalePercent = 100;
                Assert.Equal(originalFont * 2, strip.Font.Size, 3);
            });
        }

        [Fact]
        public void SettingsRejectUnsupportedSizesAndResetKeepsExportPreferences()
        {
            var viewModel = new SettingsViewModel();
            int original = viewModel.CoreConfiguration.UiScalePercent;
            var dpi = viewModel.CoreConfiguration.OutputFileDpiPreset;
            string format = viewModel.CoreConfiguration.OutputFileFormat;
            try
            {
                viewModel.UiScalePercent = 150;
                viewModel.UiScalePercent = 175;
                Assert.Equal(150, viewModel.UiScalePercent);
                viewModel.UiScalePercent = 100;
                Assert.Equal(100, viewModel.CoreConfiguration.UiScalePercent);
                Assert.Equal(dpi, viewModel.CoreConfiguration.OutputFileDpiPreset);
                Assert.Equal(format, viewModel.CoreConfiguration.OutputFileFormat);
            }
            finally { viewModel.CoreConfiguration.UiScalePercent = original; }
        }

        [Theory]
        [InlineData(100)]
        [InlineData(125)]
        [InlineData(150)]
        [InlineData(200)]
        public void RealPreferencesExposeScaleAndCanRenderWithoutOpeningAWindow(int percent)
        {
            RunOnSta(() =>
            {
                var window = new SettingsWindow();
                var viewModel = (SettingsViewModel)window.DataContext;
                int original = viewModel.CoreConfiguration.UiScalePercent;
                try
                {
                    UiScaleManager.Attach(window, viewModel.CoreConfiguration);
                    var initialRoot = (FrameworkElement)window.Content;
                    initialRoot.Measure(new Size(window.Width, window.Height));
                    initialRoot.Arrange(new Rect(0, 0, window.Width, window.Height));
                    Dispatcher.CurrentDispatcher.Invoke(new Action(() => { }), DispatcherPriority.ContextIdle);
                    window.UiScaleComboBox.GetBindingExpression(ComboBox.ItemsSourceProperty)?.UpdateTarget();
                    window.UiScaleComboBox.GetBindingExpression(ComboBox.SelectedItemProperty)?.UpdateTarget();
                    window.UiScaleComboBox.SelectedItem = percent;
                    window.UiScaleComboBox.GetBindingExpression(ComboBox.SelectedItemProperty)?.UpdateSource();
                    Dispatcher.CurrentDispatcher.Invoke(new Action(() => { }), DispatcherPriority.ContextIdle);
                    Assert.True(viewModel.CoreConfiguration.UiScalePercent == percent,
                        $"Selected={window.UiScaleComboBox.SelectedItem}, Items={window.UiScaleComboBox.Items.Count}, " +
                        $"Binding={window.UiScaleComboBox.GetBindingExpression(ComboBox.SelectedItemProperty)?.Status}, " +
                        $"Error={window.UiScaleComboBox.GetBindingExpression(ComboBox.SelectedItemProperty)?.HasError}, " +
                        $"Fixed={viewModel.CoreConfiguration.IsConstant(nameof(ICoreConfiguration.UiScalePercent))}, Data={window.UiScaleComboBox.DataContext?.GetType().Name}");
                    Assert.Equal(percent, viewModel.CoreConfiguration.UiScalePercent);
                    Assert.Equal(new[] { 100, 125, 150, 200 }, viewModel.UiScaleChoices);
                    var root = (FrameworkElement)window.Content;
                    var size = new Size(Math.Min(window.Width, 1200), Math.Min(window.Height, 850));
                    root.Measure(size);
                    root.Arrange(new Rect(size));
                    root.UpdateLayout();
                    Assert.True(window.UiScaleComboBox.ActualWidth > 0);
                    string output = Environment.GetEnvironmentVariable("GREENSHOT_UI_QA_DIRECTORY");
                    if (!string.IsNullOrEmpty(output))
                    {
                        Directory.CreateDirectory(output);
                        var image = new RenderTargetBitmap((int)size.Width, (int)size.Height, 96, 96, PixelFormats.Pbgra32);
                        var background = new DrawingVisual();
                        using (var drawing = background.RenderOpen()) drawing.DrawRectangle(window.Background ?? Brushes.White, null, new Rect(size));
                        image.Render(background);
                        image.Render(root);
                        var encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(image));
                        using var stream = File.Create(Path.Combine(output, $"preferences-ui-{percent}.png"));
                        encoder.Save(stream);
                    }
                    window.UiScaleResetButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Assert.Equal(100, viewModel.CoreConfiguration.UiScalePercent);
                }
                finally { window.Close(); viewModel.CoreConfiguration.UiScalePercent = original; }
            });
        }

        [Fact]
        public void WebpIsDiscoverableInOutputPreferences()
        {
            var viewModel = new SettingsViewModel();
            Assert.Contains(viewModel.ImageFormats, format => format.Value == "webp" && format.DisplayNameWithPreferredExtension.Contains("WebP"));
        }

        private static void RunOnSta(Action action)
        {
            Exception error = null;
            Dispatcher dispatcher = null;
            var thread = new Thread(() =>
            {
                try { Volatile.Write(ref dispatcher, Dispatcher.CurrentDispatcher); action(); }
                catch (Exception exception) { error = exception; }
                finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
            }) { IsBackground = true };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            if (!thread.Join(TimeSpan.FromSeconds(30)))
            {
                var active = Volatile.Read(ref dispatcher);
                if (active != null && !active.HasShutdownStarted) active.BeginInvokeShutdown(DispatcherPriority.Send);
                throw new TimeoutException("UI scale test exceeded 30 seconds.");
            }
            if (error != null) throw new Exception("UI scale STA test failed", error);
        }
    }
}
