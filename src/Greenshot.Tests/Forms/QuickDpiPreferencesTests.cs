/*
 * Greenshot - a free and open source screenshot tool
 * Copyright (C) 2007-2026 Thomas Braun, Jens Klingen, Robin Krom
 * Licensed under the GNU General Public License, version 1 or later.
 * See https://www.gnu.org/licenses/ and https://getgreenshot.org/.
 */

using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Greenshot.Base.Core;
using Greenshot.Base.Core.Enums;
using Greenshot.Base.Wpf;
using Greenshot.Helpers;
using Xunit;
using MenuItem = System.Windows.Forms.ToolStripMenuItem;

namespace Greenshot.Tests.Forms
{
    [Collection(TestCollections.WpfThemeState)]
    public class QuickDpiPreferencesTests
    {
        public QuickDpiPreferencesTests() => TestEnvironment.EnsureInitialized();

        [Theory]
        [InlineData(ExportDpiPreset.Preserve)]
        [InlineData(ExportDpiPreset.Web)]
        [InlineData(ExportDpiPreset.Print)]
        public void Presets_UseTheSharedPreferenceAndKeepCustomNumber(ExportDpiPreset preset)
        {
            RunOnSta(() =>
            {
                var config = CreateConfig();
                using var menu = ExportDpiQuickPreferences.Create(config, value => throw new Exception("Unexpected prompt"));
                Assert.Equal(4, menu.DropDownItems.Count);
                Assert.True(Item(menu, ExportDpiPreset.Preserve).Checked);
                Item(menu, preset).PerformClick();
                Assert.Equal(preset, config.OutputFileDpiPreset);
                Assert.Equal(96, config.OutputFileCustomDpi);
                Assert.Single(menu.DropDownItems.OfType<MenuItem>(), item => item.Checked);
                Assert.True(Item(menu, preset).Checked);
            });
        }

        [Theory]
        [InlineData(1)]
        [InlineData(72)]
        [InlineData(300)]
        [InlineData(2400)]
        public void Custom_StaysCustomEvenWhenNumberMatchesAPreset(int value)
        {
            RunOnSta(() =>
            {
                var config = CreateConfig();
                using var menu = ExportDpiQuickPreferences.Create(config, initial =>
                {
                    Assert.Equal(96, initial);
                    Assert.Equal(ExportDpiPreset.Preserve, config.OutputFileDpiPreset);
                    return value;
                });
                Item(menu, ExportDpiPreset.Custom).PerformClick();
                Assert.Equal(ExportDpiPreset.Custom, config.OutputFileDpiPreset);
                Assert.Equal(value, config.OutputFileCustomDpi);
                Assert.True(Item(menu, ExportDpiPreset.Custom).Checked);
                Assert.Contains(value + " DPI", Item(menu, ExportDpiPreset.Custom).Text);
            });
        }

        [Theory]
        [InlineData(null)]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(2401)]
        public void CancelOrInvalidCustom_ChangesNothing(int? value)
        {
            RunOnSta(() =>
            {
                var config = CreateConfig();
                using var menu = ExportDpiQuickPreferences.Create(config, initial => value);
                Item(menu, ExportDpiPreset.Custom).PerformClick();
                Assert.Equal(ExportDpiPreset.Preserve, config.OutputFileDpiPreset);
                Assert.Equal(96, config.OutputFileCustomDpi);
                Assert.True(Item(menu, ExportDpiPreset.Preserve).Checked);
            });
        }

        [Theory]
        [InlineData("webp", false)]
        [InlineData("gif", false)]
        [InlineData("png", true)]
        public void DisabledQuickSettingsOrUnsupportedFormat_CannotChangeValues(string format, bool disabled)
        {
            RunOnSta(() =>
            {
                var config = CreateConfig();
                config.OutputFileFormat = format;
                config.DisableQuickSettings = disabled;
                using var menu = ExportDpiQuickPreferences.Create(config, initial => throw new Exception("Unexpected prompt"));
                Assert.False(menu.Enabled);
                Item(menu, ExportDpiPreset.Web).PerformClick();
                Item(menu, ExportDpiPreset.Custom).PerformClick();
                Assert.Equal(ExportDpiPreset.Preserve, config.OutputFileDpiPreset);
                Assert.Equal(96, config.OutputFileCustomDpi);
            });
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Custom_RechecksEligibilityAfterThePrompt(bool disableQuickSettings)
        {
            RunOnSta(() =>
            {
                var config = CreateConfig();
                using var menu = ExportDpiQuickPreferences.Create(config, initial =>
                {
                    if (disableQuickSettings) config.DisableQuickSettings = true;
                    else config.OutputFileFormat = "webp";
                    return 600;
                });
                Item(menu, ExportDpiPreset.Custom).PerformClick();
                Assert.Equal(ExportDpiPreset.Preserve, config.OutputFileDpiPreset);
                Assert.Equal(96, config.OutputFileCustomDpi);
            });
        }

        [Fact]
        public void Refresh_ReflectsFormatAndOutputPreferenceChanges()
        {
            RunOnSta(() =>
            {
                var config = CreateConfig();
                config.OutputFileFormat = "webp";
                using var menu = ExportDpiQuickPreferences.Create(config, initial => null);
                Assert.False(menu.Enabled);
                config.OutputFileFormat = "jpg";
                config.OutputFileDpiPreset = ExportDpiPreset.Print;
                ExportDpiQuickPreferences.Refresh(menu, config);
                Assert.True(menu.Enabled);
                Assert.True(Item(menu, ExportDpiPreset.Print).Checked);
                Assert.False(Item(menu, ExportDpiPreset.Preserve).Checked);
                config.OutputFileFormat = "webp";
                ExportDpiQuickPreferences.Refresh(menu, config);
                Assert.False(menu.Enabled);
            });
        }

        [Theory]
        [InlineData(false, ExportDpiPreset.Preserve, ExportDpiPreset.Web, true)]
        [InlineData(true, ExportDpiPreset.Preserve, ExportDpiPreset.Preserve, true)]
        [InlineData(true, ExportDpiPreset.Preserve, ExportDpiPreset.Web, false)]
        [InlineData(true, ExportDpiPreset.Custom, ExportDpiPreset.Custom, true)]
        [InlineData(true, ExportDpiPreset.Custom, ExportDpiPreset.Print, false)]
        public void FixedPresetPolicy_PreventsSelectingAnotherPreset(bool fixedPreset, ExportDpiPreset current,
            ExportDpiPreset selected, bool allowed)
            => Assert.Equal(allowed, ExportDpiQuickPreferences.CanSelectPreset(fixedPreset, current, selected));

        [Theory]
        [InlineData("")]
        [InlineData("abc")]
        [InlineData("0")]
        [InlineData("-1")]
        [InlineData("+72")]
        [InlineData("72.5")]
        [InlineData("2401")]
        public void Prompt_InvalidInputStaysOpenAndPreservesInitialValue(string text)
        {
            RunOnSta(() =>
            {
                var window = new CustomDpiWindow(96);
                Assert.False(RunDialog(window, () =>
                {
                    Find<TextBox>(window, "CustomDpiValue").Text = text;
                    Click(Find<Button>(window, "CustomDpiOk"));
                    Assert.True(window.IsVisible);
                    Assert.Equal(96, window.Value);
                    Assert.Equal(Visibility.Visible, Find<TextBlock>(window, "CustomDpiValidation").Visibility);
                    window.Close();
                }) == true);
                Assert.Equal(96, window.Value);
            });
        }

        [Theory]
        [InlineData("1", 1)]
        [InlineData("72", 72)]
        [InlineData("2400", 2400)]
        public void Prompt_ValidInputReturnsTheChosenValue(string text, int value)
        {
            RunOnSta(() =>
            {
                var window = new CustomDpiWindow(96);
                Assert.True(RunDialog(window, () =>
                {
                    Find<TextBox>(window, "CustomDpiValue").Text = text;
                    Click(Find<Button>(window, "CustomDpiOk"));
                    Assert.False(window.IsVisible);
                }));
                Assert.Equal(value, window.Value);
            });
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Prompt_CancelAndCloseReturnNoAcceptedChange(bool closeWindow)
        {
            RunOnSta(() =>
            {
                var window = new CustomDpiWindow(96);
                Assert.False(RunDialog(window, () =>
                {
                    Find<TextBox>(window, "CustomDpiValue").Text = "600";
                    if (closeWindow) window.Close();
                    else Click(Find<Button>(window, "CustomDpiCancel"));
                    Assert.False(window.IsVisible);
                }) == true);
                Assert.Equal(96, window.Value);
            });
        }

        private static CoreConfigurationImpl CreateConfig()
        {
            var config = new CoreConfigurationImpl();
            config.ResetToDefaults();
            config.OutputFileFormat = "png";
            config.OutputFileDpiPreset = ExportDpiPreset.Preserve;
            config.OutputFileCustomDpi = 96;
            config.DisableQuickSettings = false;
            return config;
        }

        private static MenuItem Item(MenuItem menu, ExportDpiPreset preset)
            => (MenuItem)menu.DropDownItems["QuickExportDpi" + preset];

        private static T Find<T>(DependencyObject parent, string name) where T : FrameworkElement
        {
            foreach (var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
            {
                if (child is T control && control.Name == name) return control;
                T nested = Find<T>(child, name);
                if (nested != null) return nested;
            }
            return null;
        }

        private static void Click(Button button)
        {
            Assert.NotNull(button);
            typeof(Button).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(button, null);
        }

        private static bool? RunDialog(Window window, Action interact)
        {
            Exception error = null;
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = -5000;
            window.Top = -5000;
            window.ShowActivated = false;
            window.ShowInTaskbar = false;
            window.Dispatcher.BeginInvoke(new Action(() =>
            {
                try { interact(); }
                catch (Exception exception) { error = exception; }
                finally { if (window.IsVisible) window.Close(); }
            }), DispatcherPriority.Background);
            bool? result = window.ShowDialog();
            if (error != null) throw new Exception("Custom DPI prompt interaction failed", error);
            return result;
        }

        private static void RunOnSta(Action action)
        {
            Exception error = null;
            Dispatcher dispatcher = null;
            var thread = new Thread(() =>
            {
                try
                {
                    Volatile.Write(ref dispatcher, Dispatcher.CurrentDispatcher);
                    Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
                    action();
                }
                catch (Exception exception) { error = exception; }
                finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
            }) { IsBackground = true };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            if (!thread.Join(TimeSpan.FromSeconds(30)))
            {
                Dispatcher active = Volatile.Read(ref dispatcher);
                try { if (active != null && !active.HasShutdownStarted) active.BeginInvokeShutdown(DispatcherPriority.Send); }
                catch (InvalidOperationException) { }
                throw new TimeoutException("Quick DPI test exceeded 30 seconds; normal dispatcher shutdown was requested.");
            }
            if (error != null) throw new Exception("Quick DPI STA test failed", error);
        }
    }
}
