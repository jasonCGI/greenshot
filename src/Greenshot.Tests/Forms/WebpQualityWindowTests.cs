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
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using Dapplo.Ini;
using Greenshot.Base.Core;
using Greenshot.Base.Core.Enums;
using Greenshot.Base.Interfaces.Plugin;
using Greenshot.Base.Languages;
using Greenshot.Base.Wpf;
using Greenshot.Forms.Wpf;
using Xunit;

namespace Greenshot.Tests.Forms
{
    [Collection(TestCollections.WpfThemeState)]
    public class WebpQualityWindowTests
    {
        public WebpQualityWindowTests()
        {
            TestEnvironment.EnsureInitialized();
        }

        [Fact]
        public void WebpControls_KeepCompressionModesSeparateFromDpiAndColorReduction()
        {
            RunOnSta(() =>
            {
                var settings = CreateWebpSettings();
                var window = new QualityWindow(settings);
                try
                {
                    var mode = Find<ComboBox>(window, "WebpModeInput");
                    var quality = Find<Slider>(window, "WebpQualityInput");
                    Assert.True(mode.IsEnabled);
                    Assert.Equal(false, mode.SelectedValue);
                    Assert.Equal(Texts.Settings.WebpLossy, ((ComboBoxItem)mode.Items[0]).Content);
                    Assert.Equal(Texts.Settings.WebpLossless, ((ComboBoxItem)mode.Items[1]).Content);
                    Assert.True(quality.IsEnabled);
                    Assert.Equal(0, quality.Minimum);
                    Assert.Equal(100, quality.Maximum);
                    Assert.False(Find<ComboBox>(window, "ExportDpiPresetInput").IsEnabled);
                    Assert.False(Find<TextBox>(window, "CustomDpiInput").IsEnabled);
                    Assert.False(Find<Slider>(window, "JpegQualityInput").IsEnabled);
                    var reduceColors = Find<CheckBox>(window, "ReduceColorsInput");
                    Assert.False(reduceColors.IsEnabled);
                    Assert.False(reduceColors.IsChecked);

                    mode.SelectedValue = true;
                    Assert.False(quality.IsEnabled);
                    mode.SelectedValue = false;
                    Assert.True(quality.IsEnabled);
                    Assert.False(settings.WebpLossless);
                    Assert.Equal(80, settings.WebpQuality);
                }
                finally
                {
                    window.Close();
                }
            });
        }

        [Theory]
        [InlineData(false, 0)]
        [InlineData(false, 37)]
        [InlineData(false, 100)]
        [InlineData(true, 37)]
        public void Ok_ChangesOnlyActiveWebpSettings(bool lossless, int quality)
        {
            RunOnSta(() =>
            {
                using var snapshot = new ConfigurationSnapshot();
                snapshot.SetDistinctDefaults();
                var settings = CreateWebpSettings();
                var window = new QualityWindow(settings);
                Assert.True(RunDialog(window, () =>
                {
                    Find<ComboBox>(window, "WebpModeInput").SelectedValue = lossless;
                    Find<Slider>(window, "WebpQualityInput").Value = quality;
                    // Even artificial changes to disabled controls must not cross format boundaries.
                    Find<Slider>(window, "JpegQualityInput").Value = 10;
                    Find<ComboBox>(window, "ExportDpiPresetInput").SelectedValue = ExportDpiPreset.Print;
                    Find<TextBox>(window, "CustomDpiInput").Text = "invalid";
                    Find<CheckBox>(window, "ReduceColorsInput").IsChecked = false;
                    Assert.False(settings.WebpLossless);
                    Assert.Equal(80, settings.WebpQuality);
                    ClickOk(window);
                }));

                Assert.Equal(lossless, settings.WebpLossless);
                Assert.Equal(lossless ? 80 : quality, settings.WebpQuality);
                AssertOtherOutputSettingsUnchanged(settings);
                snapshot.AssertDistinctDefaults();
            });
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void CancelOrClose_NeverChangesOutputOrRememberedDefaults(bool closeWindow)
        {
            RunOnSta(() =>
            {
                using var snapshot = new ConfigurationSnapshot();
                snapshot.SetDistinctDefaults();
                var settings = CreateWebpSettings();
                var window = new QualityWindow(settings);
                Assert.False(RunDialog(window, () =>
                {
                    Find<ComboBox>(window, "WebpModeInput").SelectedValue = true;
                    Find<Slider>(window, "WebpQualityInput").Value = 20;
                    Find<CheckBox>(window, "DontAskAgainInput").IsChecked = true;
                    if (closeWindow)
                    {
                        window.Close();
                    }
                    else
                    {
                        var cancel = Find<Button>(window, "QualityCancelButton");
                        // IsCancel closes the dialog from Button.OnClick, not from a manually raised routed event.
                        var onClick = typeof(Button).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic);
                        Assert.NotNull(onClick);
                        onClick.Invoke(cancel, null);
                    }
                    Assert.False(window.IsVisible);
                }) == true);

                Assert.False(settings.WebpLossless);
                Assert.Equal(80, settings.WebpQuality);
                AssertOtherOutputSettingsUnchanged(settings);
                snapshot.AssertDistinctDefaults();
            });
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void SuccessfulOk_RemembersOnlyWebpDefaultsWhenRequested(bool lossless, bool dontAskAgain)
        {
            RunOnSta(() =>
            {
                using var snapshot = new ConfigurationSnapshot();
                snapshot.SetDistinctDefaults();
                var settings = CreateWebpSettings();
                var window = new QualityWindow(settings);
                Assert.True(RunDialog(window, () =>
                {
                    Find<ComboBox>(window, "WebpModeInput").SelectedValue = lossless;
                    Find<Slider>(window, "WebpQualityInput").Value = 42;
                    Find<CheckBox>(window, "DontAskAgainInput").IsChecked = dontAskAgain;
                    snapshot.AssertDistinctDefaults();
                    ClickOk(window);
                }));

                Assert.Equal(lossless, settings.WebpLossless);
                Assert.Equal(lossless ? 80 : 42, settings.WebpQuality);
                Assert.Equal(dontAskAgain && lossless, snapshot.Config.OutputFileWebpLossless);
                Assert.Equal(dontAskAgain && !lossless ? settings.WebpQuality : 63, snapshot.Config.OutputFileWebpQuality);
                Assert.Equal(!dontAskAgain, snapshot.Config.OutputFilePromptQuality);
                snapshot.AssertOtherDefaultsUnchanged();
                AssertOtherOutputSettingsUnchanged(settings);
            });
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(101)]
        [InlineData(int.MaxValue)]
        public void LosslessOkWithDontAskAgain_IgnoresAnInvalidDormantQuality(int invalidQuality)
        {
            RunOnSta(() =>
            {
                using var snapshot = new ConfigurationSnapshot();
                snapshot.SetDistinctDefaults();
                var settings = CreateWebpSettings();
                settings.WebpLossless = true;
                settings.WebpQuality = invalidQuality;
                var window = new QualityWindow(settings);
                Assert.True(RunDialog(window, () =>
                {
                    Assert.False(Find<Slider>(window, "WebpQualityInput").IsEnabled);
                    Find<CheckBox>(window, "DontAskAgainInput").IsChecked = true;
                    ClickOk(window);
                }));
                Assert.True(settings.WebpLossless);
                Assert.Equal(invalidQuality, settings.WebpQuality);
                Assert.True(snapshot.Config.OutputFileWebpLossless);
                Assert.Equal(63, snapshot.Config.OutputFileWebpQuality);
                Assert.False(snapshot.Config.OutputFilePromptQuality);
                snapshot.AssertOtherDefaultsUnchanged();
                AssertOtherOutputSettingsUnchanged(settings);
            });
        }

        [Theory]
        [InlineData("jpg")]
        [InlineData("png")]
        public void OtherFormats_IgnoreInactiveWebpControlsAndDefaults(string format)
        {
            RunOnSta(() =>
            {
                using var snapshot = new ConfigurationSnapshot();
                snapshot.SetDistinctDefaults();
                var settings = new SurfaceOutputSettings(format, 70, false)
                {
                    WebpLossless = true,
                    WebpQuality = 62,
                    ExportDpiPreset = ExportDpiPreset.Preserve
                };
                var window = new QualityWindow(settings);
                Assert.True(RunDialog(window, () =>
                {
                    var mode = Find<ComboBox>(window, "WebpModeInput");
                    var quality = Find<Slider>(window, "WebpQualityInput");
                    Assert.False(mode.IsEnabled);
                    Assert.False(quality.IsEnabled);
                    mode.SelectedValue = false;
                    quality.Value = 12;
                    Find<Slider>(window, "JpegQualityInput").Value = 55;
                    Find<CheckBox>(window, "DontAskAgainInput").IsChecked = true;
                    ClickOk(window);
                }));

                Assert.True(settings.WebpLossless);
                Assert.Equal(62, settings.WebpQuality);
                Assert.False(snapshot.Config.OutputFileWebpLossless);
                Assert.Equal(63, snapshot.Config.OutputFileWebpQuality);
                Assert.Equal(55, settings.JPGQuality);
            });
        }

        [Theory]
        [InlineData("webp", true)]
        [InlineData("jpg", false)]
        [InlineData("png", false)]
        [InlineData("gif", false)]
        public void Preferences_EnableWebpOptionsOnlyForWebp(string format, bool enabled)
        {
            RunOnSta(() =>
            {
                using var snapshot = new ConfigurationSnapshot();
                snapshot.SetDistinctDefaults();
                snapshot.Config.OutputFileFormat = format;
                SettingsWindow window = null;
                try
                {
                    window = new SettingsWindow();
                    FlushBindings(window);
                    var mode = (ComboBox)window.FindName("WebpModeInput");
                    var quality = (Slider)window.FindName("WebpQualityInput");
                    Assert.Equal(enabled, mode.IsEnabled);
                    Assert.Equal(enabled, quality.IsEnabled);
                    Assert.Equal(0, quality.Minimum);
                    Assert.Equal(100, quality.Maximum);
                    if (enabled)
                    {
                        Assert.False(((ComboBox)window.FindName("ExportDpiPresetInput")).IsEnabled);
                        Assert.False(((CheckBox)window.FindName("ReduceColorsInput")).IsEnabled);
                    }
                    Assert.True(snapshot.Config.OutputFileReduceColors);
                    snapshot.Config.OutputFileWebpLossless = true;
                    FlushBindings(window);
                    Assert.False(quality.IsEnabled);
                    snapshot.Config.OutputFileWebpLossless = false;
                    FlushBindings(window);
                    Assert.Equal(enabled, quality.IsEnabled);
                    Assert.Equal(63, snapshot.Config.OutputFileWebpQuality);
                    snapshot.AssertOtherDefaultsUnchanged();
                }
                finally
                {
                    window?.Close();
                }
            });
        }

        [Fact]
        public void Preferences_WebpBindingsLeaveJpegAndDpiDefaultsUnchanged()
        {
            RunOnSta(() =>
            {
                using var snapshot = new ConfigurationSnapshot();
                snapshot.SetDistinctDefaults();
                snapshot.Config.OutputFileFormat = "webp";
                SettingsWindow window = null;
                try
                {
                    window = new SettingsWindow();
                    FlushBindings(window);
                    var mode = (ComboBox)window.FindName("WebpModeInput");
                    var quality = (Slider)window.FindName("WebpQualityInput");
                    mode.SelectedValue = true;
                    mode.GetBindingExpression(ComboBox.SelectedValueProperty).UpdateSource();
                    FlushBindings(window);
                    Assert.True(snapshot.Config.OutputFileWebpLossless);
                    Assert.False(quality.IsEnabled);
                    Assert.Equal(63, snapshot.Config.OutputFileWebpQuality);
                    mode.SelectedValue = false;
                    mode.GetBindingExpression(ComboBox.SelectedValueProperty).UpdateSource();
                    FlushBindings(window);
                    quality.Value = 37;
                    quality.GetBindingExpression(Slider.ValueProperty).UpdateSource();
                    Assert.False(snapshot.Config.OutputFileWebpLossless);
                    Assert.Equal(37, snapshot.Config.OutputFileWebpQuality);
                    snapshot.AssertOtherDefaultsUnchanged();

                    snapshot.Config.OutputFileFormat = "png";
                    FlushBindings(window);
                    Assert.False(mode.IsEnabled);
                    Assert.False(quality.IsEnabled);
                    Assert.True(((CheckBox)window.FindName("ReduceColorsInput")).IsEnabled);
                    Assert.Equal(37, snapshot.Config.OutputFileWebpQuality);
                }
                finally
                {
                    window?.Close();
                }
            });
        }

        private static SurfaceOutputSettings CreateWebpSettings()
        {
            return new SurfaceOutputSettings("webp")
            {
                WebpLossless = false,
                WebpQuality = 80,
                JPGQuality = 71,
                ReduceColors = true,
                ExportDpiPreset = ExportDpiPreset.Custom,
                CustomDpi = 144
            };
        }

        private static void AssertOtherOutputSettingsUnchanged(SurfaceOutputSettings settings)
        {
            Assert.Equal(71, settings.JPGQuality);
            Assert.True(settings.ReduceColors);
            Assert.Equal(ExportDpiPreset.Custom, settings.ExportDpiPreset);
            Assert.Equal(144, settings.CustomDpi);
        }

        private static void FlushBindings(Window window)
        {
            window.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
        }

        private static T Find<T>(DependencyObject parent, string name) where T : FrameworkElement
        {
            T control = FindControl<T>(parent, name);
            Assert.NotNull(control);
            return control;
        }

        private static T FindControl<T>(DependencyObject parent, string name) where T : FrameworkElement
        {
            foreach (var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
            {
                if (child is T control && control.Name == name)
                {
                    return control;
                }
                T nested = FindControl<T>(child, name);
                if (nested != null)
                {
                    return nested;
                }
            }
            return null;
        }

        private static void ClickOk(QualityWindow window)
        {
            Find<Button>(window, "QualityOkButton").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        }

        private static bool? RunDialog(Window window, Action interact)
        {
            Exception interactionException = null;
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = -5000;
            window.Top = -5000;
            window.ShowActivated = false;
            window.ShowInTaskbar = false;
            window.Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    interact();
                }
                catch (Exception exception)
                {
                    interactionException = exception;
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
            if (interactionException != null)
            {
                throw new Exception("WebP dialog interaction failed", interactionException);
            }
            return result;
        }

        private static void RunOnSta(Action action)
        {
            Exception threadException = null;
            Dispatcher dispatcher = null;
            var thread = new Thread(() =>
            {
                try
                {
                    Volatile.Write(ref dispatcher, Dispatcher.CurrentDispatcher);
                    Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
                    action();
                }
                catch (Exception exception)
                {
                    threadException = exception;
                }
                finally
                {
                    Dispatcher.CurrentDispatcher.InvokeShutdown();
                }
            }) { IsBackground = true };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            if (!thread.Join(TimeSpan.FromSeconds(30)))
            {
                Dispatcher activeDispatcher = Volatile.Read(ref dispatcher);
                try
                {
                    if (activeDispatcher != null && !activeDispatcher.HasShutdownStarted)
                    {
                        activeDispatcher.BeginInvokeShutdown(DispatcherPriority.Send);
                    }
                }
                catch (InvalidOperationException)
                {
                    // The dispatcher can finish shutting down after the timeout check.
                }
                throw new TimeoutException("WebP STA test did not complete within 30 seconds. Normal dispatcher shutdown was requested without aborting the thread.");
            }
            if (threadException != null)
            {
                throw new Exception("WebP test failed on the STA thread", threadException);
            }
        }

        private sealed class ConfigurationSnapshot : IDisposable
        {
            private readonly string _format;
            private readonly bool _webpLossless;
            private readonly int _webpQuality;
            private readonly int _jpegQuality;
            private readonly bool _reduceColors;
            private readonly bool _promptQuality;
            private readonly ExportDpiPreset _dpiPreset;
            private readonly int _customDpi;

            public ConfigurationSnapshot()
            {
                Config = IniConfigHelper.EnsureSection<ICoreConfiguration>(() => new CoreConfigurationImpl());
                _format = Config.OutputFileFormat;
                _webpLossless = Config.OutputFileWebpLossless;
                _webpQuality = Config.OutputFileWebpQuality;
                _jpegQuality = Config.OutputFileJpegQuality;
                _reduceColors = Config.OutputFileReduceColors;
                _promptQuality = Config.OutputFilePromptQuality;
                _dpiPreset = Config.OutputFileDpiPreset;
                _customDpi = Config.OutputFileCustomDpi;
            }

            public ICoreConfiguration Config { get; }

            public void SetDistinctDefaults()
            {
                Config.OutputFileWebpLossless = false;
                Config.OutputFileWebpQuality = 63;
                Config.OutputFileJpegQuality = 91;
                Config.OutputFileReduceColors = true;
                Config.OutputFilePromptQuality = true;
                Config.OutputFileDpiPreset = ExportDpiPreset.Print;
                Config.OutputFileCustomDpi = 600;
            }

            public void AssertDistinctDefaults()
            {
                Assert.False(Config.OutputFileWebpLossless);
                Assert.Equal(63, Config.OutputFileWebpQuality);
                Assert.True(Config.OutputFilePromptQuality);
                AssertOtherDefaultsUnchanged();
            }

            public void AssertOtherDefaultsUnchanged()
            {
                Assert.Equal(91, Config.OutputFileJpegQuality);
                Assert.True(Config.OutputFileReduceColors);
                Assert.Equal(ExportDpiPreset.Print, Config.OutputFileDpiPreset);
                Assert.Equal(600, Config.OutputFileCustomDpi);
            }

            public void Dispose()
            {
                Config.OutputFileFormat = _format;
                Config.OutputFileWebpLossless = _webpLossless;
                Config.OutputFileWebpQuality = _webpQuality;
                Config.OutputFileJpegQuality = _jpegQuality;
                Config.OutputFileReduceColors = _reduceColors;
                Config.OutputFilePromptQuality = _promptQuality;
                Config.OutputFileDpiPreset = _dpiPreset;
                Config.OutputFileCustomDpi = _customDpi;
            }
        }
    }
}
