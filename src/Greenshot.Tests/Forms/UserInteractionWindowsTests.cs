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
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using Greenshot.Base.Core;
using Greenshot.Base.Core.Enums;
using Greenshot.Base.Interfaces;
using Greenshot.Base.Interfaces.Plugin;
using Greenshot.Base.Wpf;
using Greenshot.Forms.Wpf;
using Xunit;

namespace Greenshot.Tests.Forms
{
    /// <summary>
    /// The WPF windows of the interactive IUserInteraction: progress and output quality
    /// </summary>
    [Collection(TestCollections.WpfThemeState)]
    public class UserInteractionWindowsTests
    {
        public UserInteractionWindowsTests()
        {
            TestEnvironment.EnsureInitialized();
        }

        private static void RunOnSta(Action action)
        {
            Exception threadEx = null;
            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    threadEx = ex;
                }
                finally
                {
                    Dispatcher.CurrentDispatcher.InvokeShutdown();
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (threadEx != null)
            {
                throw new Exception("Failed on the STA thread", threadEx);
            }
        }

        /// <summary>
        /// Show the window outside of the visible screen, without activating it
        /// </summary>
        private static void ShowOffScreen(Window window)
        {
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = -5000;
            window.Top = -5000;
            window.ShowActivated = false;
            window.Show();
        }

        private static T Find<T>(DependencyObject parent) where T : DependencyObject
        {
            foreach (var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
            {
                if (child is T found)
                {
                    return found;
                }

                var nested = Find<T>(child);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        private static bool? RunQualityDialog(QualityWindow window, Action action)
        {
            Exception callbackError = null;
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = -5000;
            window.Top = -5000;
            window.ShowActivated = false;
            window.Loaded += (s, e) => window.Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    callbackError = ex;
                }
                finally
                {
                    if (window.IsVisible)
                    {
                        window.Close();
                    }
                }
            }), DispatcherPriority.Background);

            bool? result = window.ShowDialog();
            if (callbackError != null)
            {
                throw new Exception("Failed in the quality dialog callback", callbackError);
            }
            return result;
        }

        private static void ClickQualityOk(QualityWindow window)
        {
            var root = (StackPanel)window.Content;
            var ok = root.Children.OfType<StackPanel>().Last().Children.OfType<Button>().First(b => b.IsDefault);
            ok.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        }

        [Fact]
        public void ProgressWindow_ClosedByUser_CancelsTheWork()
        {
            RunOnSta(() =>
            {
                int cancelled = 0;
                var window = new ProgressWindow("Uploading", () => cancelled++);
                ShowOffScreen(window);
                window.Close();
                Assert.Equal(1, cancelled);
            });
        }

        [Fact]
        public void ProgressWindow_ClosedWhenTheWorkEnded_DoesNotCancel()
        {
            RunOnSta(() =>
            {
                int cancelled = 0;
                var window = new ProgressWindow("Uploading", () => cancelled++);
                ShowOffScreen(window);
                window.DetachCancel();
                window.CloseByCode();
                Assert.Equal(0, cancelled);
            });
        }

        [Fact]
        public void ProgressWindow_CancelButton_CancelsOnce()
        {
            RunOnSta(() =>
            {
                int cancelled = 0;
                var window = new ProgressWindow("Uploading", () => cancelled++);
                ShowOffScreen(window);
                var cancelButton = Find<Button>(window);
                cancelButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                Assert.False(cancelButton.IsEnabled);
                window.Close();
                Assert.Equal(1, cancelled);
            });
        }

        [Fact]
        public void ProgressWindow_Report_SwitchesBetweenPercentageAndIndeterminate()
        {
            RunOnSta(() =>
            {
                var window = new ProgressWindow("Uploading", null);
                var progressBar = Find<ProgressBar>(window);
                Assert.True(progressBar.IsIndeterminate);

                window.Report(new ProgressInfo("Half way", 50));
                Assert.False(progressBar.IsIndeterminate);
                Assert.Equal(50, progressBar.Value);

                window.Report(new ProgressInfo(null, 150));
                Assert.Equal(100, progressBar.Value);

                window.Report(new ProgressInfo("Waiting for the server"));
                Assert.True(progressBar.IsIndeterminate);
                window.CloseByCode();
            });
        }

        [Fact]
        public void QualityWindow_Ok_TakesTheValues()
        {
            RunOnSta(() =>
            {
                var settings = new SurfaceOutputSettings("jpg", 80, false) { ExportDpiPreset = ExportDpiPreset.Preserve };
                var window = new QualityWindow(settings);
                var slider = Find<Slider>(window);
                var dpiPreset = Find<ComboBox>(window);
                Assert.True(slider.IsEnabled);
                Assert.True(dpiPreset.IsEnabled);
                Assert.Equal(ExportDpiPreset.Preserve, dpiPreset.SelectedValue);
                window.WindowStartupLocation = WindowStartupLocation.Manual;
                window.Left = -5000;
                window.Top = -5000;
                window.ShowActivated = false;
                window.Loaded += (s, e) => window.Dispatcher.BeginInvoke(new Action(() =>
                {
                    slider.Value = 55;
                    dpiPreset.SelectedValue = ExportDpiPreset.Web;
                    // The first check box is "reduce colors", the second "don't ask again"
                    Find<CheckBox>(window).IsChecked = true;
                    var ok = LogicalTreeHelper.GetChildren((StackPanel)window.Content).OfType<StackPanel>().Last().Children.OfType<Button>().First(b => b.IsDefault);
                    ok.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                }), DispatcherPriority.Background);

                Assert.True(window.ShowDialog());
                Assert.Equal(55, settings.JPGQuality);
                Assert.True(settings.ReduceColors);
                Assert.Equal(ExportDpiPreset.Web, settings.ExportDpiPreset);
            });
        }

        [Fact]
        public void QualityWindow_Cancel_KeepsTheValues()
        {
            RunOnSta(() =>
            {
                var config = IniConfigHelper.EnsureSection<ICoreConfiguration>(() => new CoreConfigurationImpl());
                var originalPreset = config.OutputFileDpiPreset;
                bool originalPrompt = config.OutputFilePromptQuality;
                var settings = new SurfaceOutputSettings("png", 80, false) { ExportDpiPreset = ExportDpiPreset.Web };
                var window = new QualityWindow(settings);
                var slider = Find<Slider>(window);
                var dpiPreset = Find<ComboBox>(window);
                // The quality only matters for JPEG
                Assert.False(slider.Parent is UIElement parent && parent.IsEnabled);
                window.WindowStartupLocation = WindowStartupLocation.Manual;
                window.Left = -5000;
                window.Top = -5000;
                window.ShowActivated = false;
                window.Loaded += (s, e) => window.Dispatcher.BeginInvoke(new Action(() =>
                {
                    slider.Value = 10;
                    dpiPreset.SelectedValue = ExportDpiPreset.Print;
                    ((StackPanel)window.Content).Children.OfType<CheckBox>().Last().IsChecked = true;
                    window.Close();
                }), DispatcherPriority.Background);

                Assert.NotEqual(true, window.ShowDialog());
                Assert.Equal(80, settings.JPGQuality);
                Assert.Equal(ExportDpiPreset.Web, settings.ExportDpiPreset);
                Assert.Equal(originalPreset, config.OutputFileDpiPreset);
                Assert.Equal(originalPrompt, config.OutputFilePromptQuality);
                Assert.Same(settings, window.Settings);
            });
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void QualityWindow_OnlyRemembersDpiPresetWhenDontAskAgainIsChecked(bool dontAskAgain)
        {
            RunOnSta(() =>
            {
                var config = IniConfigHelper.EnsureSection<ICoreConfiguration>(() => new CoreConfigurationImpl());
                var originalPreset = config.OutputFileDpiPreset;
                bool originalPrompt = config.OutputFilePromptQuality;
                int originalQuality = config.OutputFileJpegQuality;
                bool originalReduceColors = config.OutputFileReduceColors;
                try
                {
                    config.OutputFileDpiPreset = ExportDpiPreset.Preserve;
                    config.OutputFilePromptQuality = true;
                    var settings = new SurfaceOutputSettings("png", 80, false);
                    var window = new QualityWindow(settings);
                    window.WindowStartupLocation = WindowStartupLocation.Manual;
                    window.Left = -5000;
                    window.Top = -5000;
                    window.ShowActivated = false;
                    window.Loaded += (s, e) => window.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        Find<ComboBox>(window).SelectedValue = ExportDpiPreset.Print;
                        var root = (StackPanel)window.Content;
                        root.Children.OfType<CheckBox>().Last().IsChecked = dontAskAgain;
                        var ok = root.Children.OfType<StackPanel>().Last().Children.OfType<Button>().First(b => b.IsDefault);
                        ok.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                    }), DispatcherPriority.Background);

                    Assert.True(window.ShowDialog());
                    Assert.Equal(ExportDpiPreset.Print, settings.ExportDpiPreset);
                    Assert.Equal(dontAskAgain ? ExportDpiPreset.Print : ExportDpiPreset.Preserve, config.OutputFileDpiPreset);
                    Assert.Equal(!dontAskAgain, config.OutputFilePromptQuality);
                }
                finally
                {
                    config.OutputFileDpiPreset = originalPreset;
                    config.OutputFilePromptQuality = originalPrompt;
                    config.OutputFileJpegQuality = originalQuality;
                    config.OutputFileReduceColors = originalReduceColors;
                }
            });
        }

        [Theory]
        [InlineData("png", true)]
        [InlineData("jpg", true)]
        [InlineData("bmp", false)]
        [InlineData("gif", false)]
        [InlineData("tiff", false)]
        [InlineData("greenshot", false)]
        public void QualityWindow_DpiPresetsAreAvailableOnlyForPngAndJpeg(string format, bool enabled)
        {
            RunOnSta(() =>
            {
                var window = new QualityWindow(new SurfaceOutputSettings(format));
                Assert.Equal(enabled, Find<ComboBox>(window).IsEnabled);
                Find<ComboBox>(window).SelectedValue = ExportDpiPreset.Custom;
                Assert.Equal(enabled, Find<TextBox>(window).IsEnabled);
                Find<ComboBox>(window).SelectedValue = ExportDpiPreset.Preserve;
                Assert.False(Find<TextBox>(window).IsEnabled);
                window.Close();
            });
        }

        [Theory]
        [InlineData("png", "1", 1)]
        [InlineData("png", "96", 96)]
        [InlineData("png", " 150 ", 150)]
        [InlineData("png", "2400", 2400)]
        [InlineData("jpg", "150", 150)]
        [InlineData("jpg", "2400", 2400)]
        public void QualityWindow_CustomDpi_OkTakesValidWholeNumbers(string format, string input, int expectedDpi)
        {
            RunOnSta(() =>
            {
                var config = IniConfigHelper.EnsureSection<ICoreConfiguration>(() => new CoreConfigurationImpl());
                var originalPreset = config.OutputFileDpiPreset;
                int originalCustomDpi = config.OutputFileCustomDpi;
                var settings = new SurfaceOutputSettings(format, 80, false) { ExportDpiPreset = ExportDpiPreset.Preserve, CustomDpi = 144 };
                var window = new QualityWindow(settings);

                Assert.True(RunQualityDialog(window, () =>
                {
                    Find<ComboBox>(window).SelectedValue = ExportDpiPreset.Custom;
                    Find<TextBox>(window).Text = input;
                    ClickQualityOk(window);
                }));

                Assert.Equal(ExportDpiPreset.Custom, settings.ExportDpiPreset);
                Assert.Equal(expectedDpi, settings.CustomDpi);
                Assert.Equal(originalPreset, config.OutputFileDpiPreset);
                Assert.Equal(originalCustomDpi, config.OutputFileCustomDpi);
            });
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void QualityWindow_OnlyRemembersCustomDpiAfterSuccessfulOkWithDontAskAgain(bool dontAskAgain)
        {
            RunOnSta(() =>
            {
                var config = IniConfigHelper.EnsureSection<ICoreConfiguration>(() => new CoreConfigurationImpl());
                var originalPreset = config.OutputFileDpiPreset;
                int originalCustomDpi = config.OutputFileCustomDpi;
                bool originalPrompt = config.OutputFilePromptQuality;
                int originalQuality = config.OutputFileJpegQuality;
                bool originalReduceColors = config.OutputFileReduceColors;
                try
                {
                    config.OutputFileDpiPreset = ExportDpiPreset.Preserve;
                    config.OutputFileCustomDpi = 96;
                    config.OutputFilePromptQuality = true;
                    var settings = new SurfaceOutputSettings("png", 80, false) { CustomDpi = 144 };
                    var window = new QualityWindow(settings);

                    Assert.True(RunQualityDialog(window, () =>
                    {
                        Find<ComboBox>(window).SelectedValue = ExportDpiPreset.Custom;
                        Find<TextBox>(window).Text = "300";
                        ((StackPanel)window.Content).Children.OfType<CheckBox>().Last().IsChecked = dontAskAgain;
                        ClickQualityOk(window);
                    }));

                    Assert.Equal(ExportDpiPreset.Custom, settings.ExportDpiPreset);
                    Assert.Equal(300, settings.CustomDpi);
                    Assert.Equal(dontAskAgain ? ExportDpiPreset.Custom : ExportDpiPreset.Preserve, config.OutputFileDpiPreset);
                    Assert.Equal(dontAskAgain ? 300 : 96, config.OutputFileCustomDpi);
                    Assert.Equal(!dontAskAgain, config.OutputFilePromptQuality);
                }
                finally
                {
                    config.OutputFileDpiPreset = originalPreset;
                    config.OutputFileCustomDpi = originalCustomDpi;
                    config.OutputFilePromptQuality = originalPrompt;
                    config.OutputFileJpegQuality = originalQuality;
                    config.OutputFileReduceColors = originalReduceColors;
                }
            });
        }

        [Theory]
        [InlineData("png", "")]
        [InlineData("png", "abc")]
        [InlineData("png", "0")]
        [InlineData("png", "-1")]
        [InlineData("png", "2401")]
        [InlineData("png", "+72")]
        [InlineData("png", "72.5")]
        [InlineData("png", "1e2")]
        [InlineData("png", "1,000")]
        [InlineData("png", "2147483648")]
        [InlineData("jpg", "")]
        [InlineData("jpg", "2401")]
        public void QualityWindow_CustomDpi_InvalidOkShowsErrorWithoutChangingAnySettings(string format, string input)
        {
            RunOnSta(() =>
            {
                var config = IniConfigHelper.EnsureSection<ICoreConfiguration>(() => new CoreConfigurationImpl());
                var originalPreset = config.OutputFileDpiPreset;
                int originalCustomDpi = config.OutputFileCustomDpi;
                bool originalPrompt = config.OutputFilePromptQuality;
                int originalQuality = config.OutputFileJpegQuality;
                bool originalReduceColors = config.OutputFileReduceColors;
                var settings = new SurfaceOutputSettings(format, 80, false) { ExportDpiPreset = ExportDpiPreset.Preserve, CustomDpi = 144 };
                var window = new QualityWindow(settings);

                Assert.NotEqual(true, RunQualityDialog(window, () =>
                {
                    Find<Slider>(window).Value = 55;
                    Find<CheckBox>(window).IsChecked = true;
                    Find<ComboBox>(window).SelectedValue = ExportDpiPreset.Custom;
                    Find<TextBox>(window).Text = input;
                    ((StackPanel)window.Content).Children.OfType<CheckBox>().Last().IsChecked = true;
                    ClickQualityOk(window);

                    Assert.True(window.IsVisible);
                    var error = ((StackPanel)window.Content).Children.OfType<TextBlock>().Single(block => block.Name == "CustomDpiError");
                    Assert.Equal(Visibility.Visible, error.Visibility);
                    Assert.Contains("2400", error.Text);
                    Assert.Equal(80, settings.JPGQuality);
                    Assert.False(settings.ReduceColors);
                    Assert.Equal(ExportDpiPreset.Preserve, settings.ExportDpiPreset);
                    Assert.Equal(144, settings.CustomDpi);
                    Assert.Equal(originalPreset, config.OutputFileDpiPreset);
                    Assert.Equal(originalCustomDpi, config.OutputFileCustomDpi);
                    Assert.Equal(originalPrompt, config.OutputFilePromptQuality);
                    Assert.Equal(originalQuality, config.OutputFileJpegQuality);
                    Assert.Equal(originalReduceColors, config.OutputFileReduceColors);
                }));
            });
        }

        [Fact]
        public void QualityWindow_CustomDpi_CancelKeepsExportAndPreferenceValues()
        {
            RunOnSta(() =>
            {
                var config = IniConfigHelper.EnsureSection<ICoreConfiguration>(() => new CoreConfigurationImpl());
                var originalPreset = config.OutputFileDpiPreset;
                int originalCustomDpi = config.OutputFileCustomDpi;
                bool originalPrompt = config.OutputFilePromptQuality;
                var settings = new SurfaceOutputSettings("png", 80, false) { ExportDpiPreset = ExportDpiPreset.Custom, CustomDpi = 144 };
                var window = new QualityWindow(settings);

                Assert.NotEqual(true, RunQualityDialog(window, () =>
                {
                    Find<TextBox>(window).Text = "300";
                    Find<ComboBox>(window).SelectedValue = ExportDpiPreset.Print;
                    ((StackPanel)window.Content).Children.OfType<CheckBox>().Last().IsChecked = true;
                    window.Close();
                }));

                Assert.Equal(ExportDpiPreset.Custom, settings.ExportDpiPreset);
                Assert.Equal(144, settings.CustomDpi);
                Assert.Equal(80, settings.JPGQuality);
                Assert.False(settings.ReduceColors);
                Assert.Equal(originalPreset, config.OutputFileDpiPreset);
                Assert.Equal(originalCustomDpi, config.OutputFileCustomDpi);
                Assert.Equal(originalPrompt, config.OutputFilePromptQuality);
            });
        }

        [Fact]
        public void QualityWindow_CustomDpi_CorrectingAnErrorKeepsCustomDistinctFromWeb()
        {
            RunOnSta(() =>
            {
                var settings = new SurfaceOutputSettings("png") { ExportDpiPreset = ExportDpiPreset.Preserve, CustomDpi = 144 };
                var window = new QualityWindow(settings);

                Assert.True(RunQualityDialog(window, () =>
                {
                    Find<ComboBox>(window).SelectedValue = ExportDpiPreset.Custom;
                    var input = Find<TextBox>(window);
                    input.Text = "0";
                    ClickQualityOk(window);
                    Assert.True(window.IsVisible);
                    input.Text = "72";
                    var error = ((StackPanel)window.Content).Children.OfType<TextBlock>().Single(block => block.Name == "CustomDpiError");
                    Assert.Equal(Visibility.Collapsed, error.Visibility);
                    ClickQualityOk(window);
                }));

                Assert.Equal(ExportDpiPreset.Custom, settings.ExportDpiPreset);
                Assert.Equal(72, settings.CustomDpi);
            });
        }

        [Theory]
        [InlineData(ExportDpiPreset.Preserve)]
        [InlineData(ExportDpiPreset.Web)]
        [InlineData(ExportDpiPreset.Print)]
        public void QualityWindow_DormantInvalidCustomTextDoesNotBlockOtherPresets(ExportDpiPreset preset)
        {
            RunOnSta(() =>
            {
                var settings = new SurfaceOutputSettings("png") { ExportDpiPreset = ExportDpiPreset.Custom, CustomDpi = 144 };
                var window = new QualityWindow(settings);

                Assert.True(RunQualityDialog(window, () =>
                {
                    Find<TextBox>(window).Text = "";
                    Find<ComboBox>(window).SelectedValue = preset;
                    Assert.False(Find<TextBox>(window).IsEnabled);
                    ClickQualityOk(window);
                }));

                Assert.Equal(preset, settings.ExportDpiPreset);
                Assert.Equal(144, settings.CustomDpi);
            });
        }

        [Theory]
        [InlineData("", false)]
        [InlineData("0", false)]
        [InlineData("-1", false)]
        [InlineData("2401", false)]
        [InlineData("1,000", false)]
        [InlineData("1e2", false)]
        [InlineData("1", true)]
        [InlineData(" 150 ", true)]
        [InlineData("2400", true)]
        public void ExportDpiValidationRule_UsesTheSharedCustomBounds(string input, bool expectedValid)
        {
            var rule = new ExportDpiValidationRule();
            Assert.Equal(expectedValid, rule.Validate(input, CultureInfo.InvariantCulture).IsValid);
        }

        [Theory]
        [InlineData(ExportDpiPreset.Preserve)]
        [InlineData(ExportDpiPreset.Web)]
        [InlineData(ExportDpiPreset.Print)]
        public void SettingsWindow_DormantCustomDpiResetsValidationAndKeepsTheStoredValue(ExportDpiPreset preset)
        {
            RunOnSta(() =>
            {
                var config = IniConfigHelper.EnsureSection<ICoreConfiguration>(() => new CoreConfigurationImpl());
                string originalFormat = config.OutputFileFormat;
                var originalPreset = config.OutputFileDpiPreset;
                int originalCustomDpi = config.OutputFileCustomDpi;
                SettingsWindow window = null;
                try
                {
                    config.OutputFileFormat = "png";
                    config.OutputFileDpiPreset = ExportDpiPreset.Custom;
                    config.OutputFileCustomDpi = 96;
                    window = new SettingsWindow();
                    window.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
                    var input = (TextBox)window.FindName("CustomDpiInput");
                    Assert.True(input.IsEnabled);
                    Assert.False(Validation.GetHasError(input));
                    input.Text = "";
                    input.GetBindingExpression(TextBox.TextProperty).UpdateSource();
                    Assert.True(Validation.GetHasError(input));
                    Assert.Equal(96, config.OutputFileCustomDpi);
                    Assert.False(((Button)window.FindName("SettingsOkButton")).IsEnabled);

                    config.OutputFileDpiPreset = preset;
                    window.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
                    Assert.False(input.IsEnabled);
                    Assert.False(Validation.GetHasError(input));
                    Assert.Equal("96", input.Text);
                    Assert.Equal(96, config.OutputFileCustomDpi);
                    Assert.True(((Button)window.FindName("SettingsOkButton")).IsEnabled);
                }
                finally
                {
                    window?.Close();
                    config.OutputFileFormat = originalFormat;
                    config.OutputFileDpiPreset = originalPreset;
                    config.OutputFileCustomDpi = originalCustomDpi;
                }
            });
        }

        [Theory]
        [InlineData("bmp")]
        [InlineData("gif")]
        [InlineData("tiff")]
        [InlineData("greenshot")]
        public void SettingsWindow_UnsupportedFormatDisablesCustomDpiAndItsValidation(string format)
        {
            RunOnSta(() =>
            {
                var config = IniConfigHelper.EnsureSection<ICoreConfiguration>(() => new CoreConfigurationImpl());
                string originalFormat = config.OutputFileFormat;
                var originalPreset = config.OutputFileDpiPreset;
                int originalCustomDpi = config.OutputFileCustomDpi;
                SettingsWindow window = null;
                try
                {
                    config.OutputFileFormat = "png";
                    config.OutputFileDpiPreset = ExportDpiPreset.Custom;
                    config.OutputFileCustomDpi = 96;
                    window = new SettingsWindow();
                    window.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
                    var input = (TextBox)window.FindName("CustomDpiInput");
                    input.Text = "";
                    input.GetBindingExpression(TextBox.TextProperty).UpdateSource();
                    Assert.True(Validation.GetHasError(input));

                    config.OutputFileFormat = format;
                    window.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
                    Assert.False(((ComboBox)window.FindName("ExportDpiPresetInput")).IsEnabled);
                    Assert.False(input.IsEnabled);
                    Assert.False(Validation.GetHasError(input));
                    Assert.Equal("96", input.Text);
                    Assert.Equal(96, config.OutputFileCustomDpi);
                    Assert.True(((Button)window.FindName("SettingsOkButton")).IsEnabled);
                }
                finally
                {
                    window?.Close();
                    config.OutputFileFormat = originalFormat;
                    config.OutputFileDpiPreset = originalPreset;
                    config.OutputFileCustomDpi = originalCustomDpi;
                }
            });
        }

        [Fact]
        public void SettingsWindow_ExplicitCustomDpiValidationRejectsInvalidInputBeforeSave()
        {
            RunOnSta(() =>
            {
                var config = IniConfigHelper.EnsureSection<ICoreConfiguration>(() => new CoreConfigurationImpl());
                string originalFormat = config.OutputFileFormat;
                var originalPreset = config.OutputFileDpiPreset;
                int originalCustomDpi = config.OutputFileCustomDpi;
                SettingsWindow window = null;
                try
                {
                    config.OutputFileFormat = "png";
                    config.OutputFileDpiPreset = ExportDpiPreset.Custom;
                    config.OutputFileCustomDpi = 96;
                    window = new SettingsWindow();
                    window.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
                    var input = (TextBox)window.FindName("CustomDpiInput");
                    input.Text = "2401";
                    var validate = typeof(SettingsWindow).GetMethod("ValidateCustomDpiInput", BindingFlags.Instance | BindingFlags.NonPublic);

                    Assert.False((bool)validate.Invoke(window, null));
                    Assert.True(Validation.GetHasError(input));
                    Assert.Equal(96, config.OutputFileCustomDpi);
                    Assert.Null(window.DialogResult);

                    config.OutputFileDpiPreset = ExportDpiPreset.Preserve;
                    window.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
                    Assert.True((bool)validate.Invoke(window, null));
                    Assert.Equal(96, config.OutputFileCustomDpi);
                }
                finally
                {
                    window?.Close();
                    config.OutputFileFormat = originalFormat;
                    config.OutputFileDpiPreset = originalPreset;
                    config.OutputFileCustomDpi = originalCustomDpi;
                }
            });
        }
    }
}
