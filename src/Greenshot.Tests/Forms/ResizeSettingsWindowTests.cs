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
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using Greenshot.Base.Effects;
using Greenshot.Base.Languages;
using Greenshot.Editor.Forms;
using Xunit;

namespace Greenshot.Tests.Forms
{
    [Collection(TestCollections.WpfThemeState)]
    public class ResizeSettingsWindowTests
    {
        public ResizeSettingsWindowTests()
        {
            TestEnvironment.EnsureInitialized();
        }

        [Theory]
        [InlineData(100)]
        [InlineData(125)]
        [InlineData(150)]
        [InlineData(200)]
        public void ResizeAndSummaryDialogs_RenderAndUpdateAtEachUiSize(int percent)
        {
            RunOnSta(() =>
            {
                var config = Dapplo.Ini.IniConfigRegistry.GetSection<Greenshot.Base.Core.ICoreConfiguration>();
                int original = config.UiScalePercent;
                config.UiScalePercent = percent;
                var resize = new ResizeSettingsWindow(new ResizeEffect(2400, 1600, false));
                var settings = new Greenshot.Base.Interfaces.Plugin.SurfaceOutputSettings("png")
                {
                    PreviewSize = new System.Drawing.Size(1200, 900), PreviewDpiX = 144, PreviewDpiY = 144,
                    ExportDpiPreset = Greenshot.Base.Core.Enums.ExportDpiPreset.Preserve
                };
                var quality = new Greenshot.Base.Wpf.QualityWindow(settings);
                try
                {
                    Find<ComboBox>(resize, "WidthUnitComboBox").SelectedItem = "Inches";
                    Find<ComboBox>(resize, "HeightUnitComboBox").SelectedItem = "Inches";
                    Find<TextBox>(resize, "WidthTextBox").Text = "4";
                    Find<TextBox>(resize, "HeightTextBox").Text = "3";
                    var root = (StackPanel)quality.Content;
                    var preset = root.Children.OfType<ComboBox>().Single();
                    preset.SelectedValue = Greenshot.Base.Core.Enums.ExportDpiPreset.Print;
                    var summary = root.Children.OfType<TextBlock>().Single(t => t.Name == "ExportSummaryText");
                    Assert.Contains("4 x 3 in", summary.Text);
                    Assert.Equal(Greenshot.Base.Core.Enums.ExportDpiPreset.Preserve, settings.ExportDpiPreset);
                    RenderDialog(resize, config, $"resize-{percent}");
                    RenderDialog(quality, config, $"export-summary-{percent}");
                }
                finally { resize.Close(); quality.Close(); config.UiScalePercent = original; }
            });
        }

        private static void RenderDialog(Window window, Greenshot.Base.Core.ICoreConfiguration config, string filename)
        {
            Greenshot.Base.Wpf.UiScaleManager.Attach(window, config);
            var root = (FrameworkElement)window.Content;
            root.Measure(new System.Windows.Size(window.Width, 1000));
            var size = new System.Windows.Size(window.Width, Math.Min(1000, root.DesiredSize.Height));
            root.Arrange(new Rect(size));
            root.UpdateLayout();
            string output = Environment.GetEnvironmentVariable("GREENSHOT_UI_QA_DIRECTORY");
            if (string.IsNullOrEmpty(output)) return;
            System.IO.Directory.CreateDirectory(output);
            var image = new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(size.Width),
                (int)Math.Ceiling(size.Height), 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            var background = new System.Windows.Media.DrawingVisual();
            using (var drawing = background.RenderOpen()) drawing.DrawRectangle(window.Background, null, new Rect(size));
            image.Render(background);
            image.Render(root);
            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image));
            using (var stream = System.IO.File.Create(System.IO.Path.Combine(output, filename + ".png"))) encoder.Save(stream);
        }

        [Theory]
        [InlineData("Inches", "4", "3")]
        [InlineData("Centimeters", "10.16", "7.62")]
        public void PhysicalSize_ResamplesAndSetsDpi(string unit, string width, string height)
        {
            RunOnSta(() =>
            {
                var effect = new ResizeEffect(2400, 1600, false);
                var window = new ResizeSettingsWindow(effect);
                Assert.True(RunDialog(window, () =>
                {
                    Find<ComboBox>(window, "WidthUnitComboBox").SelectedItem = unit;
                    Find<ComboBox>(window, "HeightUnitComboBox").SelectedItem = unit;
                    Find<TextBox>(window, "WidthTextBox").Text = width;
                    Find<TextBox>(window, "HeightTextBox").Text = height;
                    Assert.Contains("1200 x 900", Find<TextBlock>(window, "ResizeSummary").Text);
                    Find<Button>(window, "OkButton").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                }) == true);
                Assert.Equal(1200, effect.Width);
                Assert.Equal(900, effect.Height);
                Assert.Equal(300, effect.ResolutionDpi);
                using (var source = new Bitmap(2400, 1600))
                using (var resized = effect.Apply(source, null))
                {
                    Assert.Equal(new System.Drawing.Size(1200, 900), resized.Size);
                    Assert.Equal(300, resized.HorizontalResolution);
                }
            });
        }

        [Fact]
        public void Upscaling_RequiresAcknowledgment_AndCancelKeepsEffect()
        {
            RunOnSta(() =>
            {
                var effect = new ResizeEffect(400, 200, true);
                var window = new ResizeSettingsWindow(effect);
                Assert.False(RunDialog(window, () =>
                {
                    Find<TextBox>(window, "WidthTextBox").Text = "800";
                    Assert.False(Find<Button>(window, "OkButton").IsEnabled);
                    Find<Button>(window, "OkButton").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                    Assert.Equal(400, effect.Width);
                    Find<CheckBox>(window, "AllowUpscaleCheckBox").IsChecked = true;
                    Assert.True(Find<Button>(window, "OkButton").IsEnabled);
                    window.DialogResult = false;
                }) == true);
                Assert.Equal(400, effect.Width);
                Assert.Null(effect.ResolutionDpi);
            });
        }

        [Theory]
        [InlineData("")]
        [InlineData("0")]
        [InlineData("2401")]
        [InlineData("300.5")]
        public void PhysicalSize_InvalidDpiCannotApply(string dpi)
        {
            RunOnSta(() =>
            {
                var effect = new ResizeEffect(400, 200, true);
                var window = new ResizeSettingsWindow(effect);
                Find<ComboBox>(window, "WidthUnitComboBox").SelectedItem = "Inches";
                Find<TextBox>(window, "DpiTextBox").Text = dpi;
                Assert.False(Find<Button>(window, "OkButton").IsEnabled);
                Find<Button>(window, "OkButton").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                Assert.Null(effect.ResolutionDpi);
                window.Close();
            });
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("not a number")]
        [InlineData("0")]
        [InlineData("-1")]
        [InlineData("0.1")]
        [InlineData("0.5")]
        [InlineData("NaN")]
        [InlineData("Infinity")]
        [InlineData("-Infinity")]
        [InlineData("1e2")]
        [InlineData("1,000")]
        [InlineData("32,000.5")]
        [InlineData("32768")]
        [InlineData("2147483648")]
        [InlineData("99999999999999999999999999999999999999999999999999999999999999")]
        public void InvalidText_DisablesOkAndCannotApplyStaleDimensions(string invalidText)
        {
            RunOnSta(() =>
            {
                foreach (bool editWidth in new[] { true, false })
                {
                    var effect = new ResizeEffect(400, 200, false);
                    var window = new ResizeSettingsWindow(effect);
                Find<CheckBox>(window, "AllowUpscaleCheckBox").IsChecked = true;
                    var input = Find<TextBox>(window, editWidth ? "WidthTextBox" : "HeightTextBox");
                    var ok = Find<Button>(window, "OkButton");
                    var message = Find<TextBlock>(window, "ValidationMessage");

                    input.Text = "150";
                    Assert.True(ok.IsEnabled);
                    input.Text = invalidText;

                    Assert.False(ok.IsEnabled);
                    Assert.Equal(Visibility.Visible, message.Visibility);
                    Assert.False(string.IsNullOrWhiteSpace(message.Text));
                    // Raising a disabled click still cannot apply the earlier valid value.
                    ok.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                    Assert.Equal(400, effect.Width);
                    Assert.Equal(200, effect.Height);
                    Assert.False(effect.MaintainAspectRatio);
                    window.Close();
                }
            });
        }

        [Theory]
        [InlineData("32767", "1", true)]
        [InlineData("1", "32767", true)]
        [InlineData("10000", "10000", true)]
        [InlineData("10001", "10000", false)]
        [InlineData("32768", "1", false)]
        [InlineData("1", "32768", false)]
        [InlineData("32767", "32767", false)]
        [InlineData("0.49", "100", false)]
        [InlineData("0.51", "100", true)]
        public void OutputDimensions_UsePerAxisAndTotalPixelGuardrails(string width, string height, bool valid)
        {
            RunOnSta(() =>
            {
                var window = new ResizeSettingsWindow(new ResizeEffect(100, 100, false));
                Find<CheckBox>(window, "AllowUpscaleCheckBox").IsChecked = true;
                Find<TextBox>(window, "WidthTextBox").Text = width;
                Find<TextBox>(window, "HeightTextBox").Text = height;

                Assert.Equal(valid, Find<Button>(window, "OkButton").IsEnabled);
                var message = Find<TextBlock>(window, "ValidationMessage");
                Assert.Equal(valid ? Visibility.Collapsed : Visibility.Visible, message.Visibility);
                if (!valid)
                {
                    Assert.Equal(Texts.Editor.ResizeValidationLimits, message.Text);
                    Assert.Contains("32767", message.Text);
                    Assert.Contains("100,000,000", message.Text);
                }
                window.Close();
            });
        }

        [Theory]
        [InlineData("150.5", "75.5", 150, 76)]
        [InlineData("150.6", "75.4", 151, 75)]
        [InlineData(" 150.6 ", "+75.4", 151, 75)]
        [InlineData("32767", "1", 32767, 1)]
        [InlineData("10000", "10000", 10000, 10000)]
        public void Ok_AppliesValidatedDimensionsWithExistingPixelRounding(string width, string height, int expectedWidth, int expectedHeight)
        {
            RunOnSta(() =>
            {
                var effect = new ResizeEffect(400, 200, false);
                var window = new ResizeSettingsWindow(effect);
                Find<CheckBox>(window, "AllowUpscaleCheckBox").IsChecked = true;
                bool? result = RunDialog(window, () =>
                {
                    Find<TextBox>(window, "WidthTextBox").Text = width;
                    Find<TextBox>(window, "HeightTextBox").Text = height;
                    Assert.True(Find<Button>(window, "OkButton").IsEnabled);
                    Assert.Equal(400, effect.Width);
                    Assert.Equal(200, effect.Height);
                    Find<Button>(window, "OkButton").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                });

                Assert.True(result == true);
                Assert.Equal(expectedWidth, effect.Width);
                Assert.Equal(expectedHeight, effect.Height);
            });
        }

        [Theory]
        [InlineData(109, 54)]
        [InlineData(115, 58)]
        public void PercentDimensions_UseStableToEvenRounding(int originalWidth, int expectedWidth)
        {
            RunOnSta(() =>
            {
                var effect = new ResizeEffect(originalWidth, 100, false);
                var window = new ResizeSettingsWindow(effect);
                Find<CheckBox>(window, "AllowUpscaleCheckBox").IsChecked = true;
                Assert.True(RunDialog(window, () =>
                {
                    Find<ComboBox>(window, "WidthUnitComboBox").SelectedIndex = 1;
                    Find<TextBox>(window, "WidthTextBox").Text = "50";
                    Assert.True(Find<Button>(window, "OkButton").IsEnabled);
                    Find<Button>(window, "OkButton").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                }));

                Assert.Equal(expectedWidth, effect.Width);
                Assert.Equal(100, effect.Height);
                using var source = new Bitmap(originalWidth, 100);
                using System.Drawing.Image resized = effect.Apply(source, null);
                Assert.Equal(expectedWidth, resized.Width);
                Assert.Equal(100, resized.Height);
            });
        }

        [Fact]
        public void PercentAndPixelUnits_PreserveFractionalPercentAndAspectRatio()
        {
            RunOnSta(() =>
            {
                var effect = new ResizeEffect(8000, 4000, true);
                var window = new ResizeSettingsWindow(effect);
                Find<CheckBox>(window, "AllowUpscaleCheckBox").IsChecked = true;
                bool? result = RunDialog(window, () =>
                {
                    var width = Find<TextBox>(window, "WidthTextBox");
                    var height = Find<TextBox>(window, "HeightTextBox");
                    var widthUnit = Find<ComboBox>(window, "WidthUnitComboBox");
                    var heightUnit = Find<ComboBox>(window, "HeightUnitComboBox");
                    widthUnit.SelectedIndex = 1;
                    width.Text = "50.25";
                    Assert.Equal("2010", height.Text);
                    widthUnit.SelectedIndex = 0;
                    Assert.Equal("4020", width.Text);
                    widthUnit.SelectedIndex = 1;
                    Assert.Equal("50.25", width.Text);
                    heightUnit.SelectedIndex = 1;
                    Assert.Equal("50.25", height.Text);
                    height.Text = "12.5";
                    Assert.Equal("12.5", width.Text);
                    Assert.True(Find<Button>(window, "OkButton").IsEnabled);
                    Find<Button>(window, "OkButton").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                });

                Assert.True(result == true);
                Assert.Equal(1000, effect.Width);
                Assert.Equal(500, effect.Height);
                Assert.True(effect.MaintainAspectRatio);
            });
        }

        [Theory]
        [InlineData("0")]
        [InlineData("-0.5")]
        [InlineData("NaN")]
        [InlineData("1e2")]
        [InlineData("0.01")]
        [InlineData("32768")]
        public void PercentInput_ValidatesConvertedOutputInsteadOfAcceptingStaleValues(string invalidPercent)
        {
            RunOnSta(() =>
            {
                var effect = new ResizeEffect(100, 100, true);
                var window = new ResizeSettingsWindow(effect);
                Find<CheckBox>(window, "AllowUpscaleCheckBox").IsChecked = true;
                Find<ComboBox>(window, "WidthUnitComboBox").SelectedIndex = 1;
                Find<TextBox>(window, "WidthTextBox").Text = invalidPercent;

                var ok = Find<Button>(window, "OkButton");
                Assert.False(ok.IsEnabled);
                ok.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                Assert.Equal(100, effect.Width);
                Assert.Equal(100, effect.Height);
                window.Close();
            });
        }

        [Fact]
        public void InvalidInput_StaysInvalidAfterUnitAndAspectChanges()
        {
            RunOnSta(() =>
            {
                var window = new ResizeSettingsWindow(new ResizeEffect(400, 200, false));
                Find<CheckBox>(window, "AllowUpscaleCheckBox").IsChecked = true;
                var width = Find<TextBox>(window, "WidthTextBox");
                width.Text = "bad input";
                Find<ComboBox>(window, "WidthUnitComboBox").SelectedIndex = 1;
                Find<CheckBox>(window, "MaintainAspectRatioCheckBox").IsChecked = true;

                Assert.Equal("bad input", width.Text);
                Assert.False(Find<Button>(window, "OkButton").IsEnabled);
                window.Close();
            });
        }

        [Fact]
        public void ChangingOneUnit_DoesNotReplaceInvalidTextInOtherDimension()
        {
            RunOnSta(() =>
            {
                var window = new ResizeSettingsWindow(new ResizeEffect(400, 200, false));
                Find<CheckBox>(window, "AllowUpscaleCheckBox").IsChecked = true;
                var height = Find<TextBox>(window, "HeightTextBox");
                height.Text = "bad input";
                Find<ComboBox>(window, "WidthUnitComboBox").SelectedIndex = 1;

                Assert.Equal("bad input", height.Text);
                Assert.False(Find<Button>(window, "OkButton").IsEnabled);
                window.Close();
            });
        }

        [Fact]
        public void AspectLock_CanBeTurnedOffAndRestored()
        {
            RunOnSta(() =>
            {
                var window = new ResizeSettingsWindow(new ResizeEffect(400, 200, true));
                Find<CheckBox>(window, "AllowUpscaleCheckBox").IsChecked = true;
                var width = Find<TextBox>(window, "WidthTextBox");
                var height = Find<TextBox>(window, "HeightTextBox");
                var aspect = Find<CheckBox>(window, "MaintainAspectRatioCheckBox");
                width.Text = "200";
                Assert.Equal("100", height.Text);
                aspect.IsChecked = false;
                height.Text = "75";
                Assert.Equal("200", width.Text);
                aspect.IsChecked = true;

                Assert.Equal("100", height.Text);
                Assert.True(Find<Button>(window, "OkButton").IsEnabled);
                window.Close();
            });
        }

        [Theory]
        [InlineData("de-DE", "50,25")]
        [InlineData("de-DE", "50.25")]
        [InlineData("en-US", "50.25")]
        public void PercentInput_AcceptsCurrentCultureAndInvariantDecimals(string culture, string percent)
        {
            RunOnSta(() =>
            {
                var effect = new ResizeEffect(8000, 4000, true);
                var window = new ResizeSettingsWindow(effect);
                Find<CheckBox>(window, "AllowUpscaleCheckBox").IsChecked = true;
                bool? result = RunDialog(window, () =>
                {
                    Find<ComboBox>(window, "WidthUnitComboBox").SelectedIndex = 1;
                    Find<TextBox>(window, "WidthTextBox").Text = percent;
                    Assert.True(Find<Button>(window, "OkButton").IsEnabled);
                    Find<Button>(window, "OkButton").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                });

                Assert.True(result == true);
                Assert.Equal(4020, effect.Width);
                Assert.Equal(2010, effect.Height);
            }, culture);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void CancelOrClose_NeverChangesEffect(bool closeWindow)
        {
            RunOnSta(() =>
            {
                var effect = new ResizeEffect(400, 200, true);
                var window = new ResizeSettingsWindow(effect);
                Find<CheckBox>(window, "AllowUpscaleCheckBox").IsChecked = true;
                bool? result = RunDialog(window, () =>
                {
                    Find<TextBox>(window, "WidthTextBox").Text = "300";
                    Find<CheckBox>(window, "MaintainAspectRatioCheckBox").IsChecked = false;
                    Find<TextBox>(window, "HeightTextBox").Text = "120";
                    if (closeWindow)
                    {
                        window.Close();
                    }
                    else
                    {
                        FindCancelButton(window).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                    }
                });

                Assert.False(result == true);
                Assert.Equal(400, effect.Width);
                Assert.Equal(200, effect.Height);
                Assert.True(effect.MaintainAspectRatio);
            });
        }

        [Fact]
        public void CorrectingInvalidInput_ClearsValidationMessage()
        {
            RunOnSta(() =>
            {
                var window = new ResizeSettingsWindow(new ResizeEffect(400, 200, false));
                Find<CheckBox>(window, "AllowUpscaleCheckBox").IsChecked = true;
                var width = Find<TextBox>(window, "WidthTextBox");
                width.Text = string.Empty;
                Assert.False(Find<Button>(window, "OkButton").IsEnabled);
                width.Text = "150";

                Assert.True(Find<Button>(window, "OkButton").IsEnabled);
                var message = Find<TextBlock>(window, "ValidationMessage");
                Assert.Equal(Visibility.Collapsed, message.Visibility);
                Assert.Equal(string.Empty, message.Text);
                window.Close();
            });
        }

        [Theory]
        [InlineData(16, 1, true)]
        [InlineData(1, 16, false)]
        public void AspectLockedTinyInput_RejectsDimensionsThatRoundBelowOnePixel(int sourceWidth, int sourceHeight, bool editWidth)
        {
            RunOnSta(() =>
            {
                var effect = new ResizeEffect(sourceWidth, sourceHeight, true);
                var window = new ResizeSettingsWindow(effect);
                Find<CheckBox>(window, "AllowUpscaleCheckBox").IsChecked = true;
                Find<TextBox>(window, editWidth ? "WidthTextBox" : "HeightTextBox").Text = "1";

                var ok = Find<Button>(window, "OkButton");
                Assert.False(ok.IsEnabled);
                Assert.Equal(Texts.Editor.ResizeValidationLimits, Find<TextBlock>(window, "ValidationMessage").Text);
                ok.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                Assert.Equal(sourceWidth, effect.Width);
                Assert.Equal(sourceHeight, effect.Height);
                window.Close();
            });
        }

        [Theory]
        [InlineData(16, 9, "2", "1", true, 2, 1)]
        [InlineData(9, 16, "1", "2", true, 1, 2)]
        [InlineData(16, 9, "4", "2", true, 4, 2)]
        [InlineData(16, 9, "1", "1", false, 1, 1)]
        [InlineData(9, 16, "1", "1", false, 1, 1)]
        [InlineData(16, 9, "1", "1", true, 1, 1)]
        [InlineData(9, 16, "1", "1", true, 1, 1)]
        [InlineData(41, 41, "2", "2", false, 2, 2)]
        [InlineData(41, 41, "2", "2", true, 2, 2)]
        public void SafeTinyDimensions_ApplyConsistentRoundedResizeEffect(int sourceWidth, int sourceHeight,
            string width, string height, bool aspectLocked, int expectedWidth, int expectedHeight)
        {
            RunOnSta(() =>
            {
                var effect = new ResizeEffect(sourceWidth, sourceHeight, aspectLocked);
                var window = new ResizeSettingsWindow(effect);
                Find<CheckBox>(window, "AllowUpscaleCheckBox").IsChecked = true;
                bool? result = RunDialog(window, () =>
                {
                    Find<TextBox>(window, "WidthTextBox").Text = width;
                    if (!aspectLocked || sourceHeight > sourceWidth)
                    {
                        Find<TextBox>(window, "HeightTextBox").Text = height;
                    }
                    Assert.True(Find<Button>(window, "OkButton").IsEnabled);
                    Find<Button>(window, "OkButton").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                });

                Assert.True(result == true);
                using var source = new Bitmap(sourceWidth, sourceHeight);
                using System.Drawing.Image resized = effect.Apply(source, null);
                Assert.Equal(expectedWidth, resized.Width);
                Assert.Equal(expectedHeight, resized.Height);
                Assert.True(resized.Width >= 1);
                Assert.True(resized.Height >= 1);
            });
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void OnePixelResize_NoLongerLosesAPixelToFloatTruncation(bool aspectLocked)
        {
            RunOnSta(() =>
            {
                var effect = new ResizeEffect(41, 41, aspectLocked);
                var window = new ResizeSettingsWindow(effect);
                Find<CheckBox>(window, "AllowUpscaleCheckBox").IsChecked = true;
                Assert.True(RunDialog(window, () =>
                {
                    Find<TextBox>(window, "WidthTextBox").Text = "1";
                    Find<TextBox>(window, "HeightTextBox").Text = "1";
                    Assert.True(Find<Button>(window, "OkButton").IsEnabled);
                    Find<Button>(window, "OkButton").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                }));
                Assert.Equal(1, effect.Width);
                Assert.Equal(1, effect.Height);
                using var source = new Bitmap(41, 41);
                using System.Drawing.Image resized = effect.Apply(source, null);
                Assert.Equal(1, resized.Width);
                Assert.Equal(1, resized.Height);
            });
        }

        [Theory]
        [InlineData(22, 11, true, "15", "7.5", 15, 8, 15, 8)]
        [InlineData(14, 7, true, "29", "14.5", 29, 14, 28, 14)]
        [InlineData(2, 1, true, "5", "2.5", 5, 2, 4, 2)]
        [InlineData(11, 22, false, "15", "7.5", 8, 15, 8, 15)]
        [InlineData(7, 14, false, "29", "14.5", 14, 29, 14, 28)]
        public void AspectCounterpart_UsesStableToEvenRoundingAndFitWithinRoundedBounds(int sourceWidth, int sourceHeight,
            bool editWidth, string input, string counterpart, int requestedWidth, int requestedHeight,
            int expectedWidth, int expectedHeight)
        {
            RunOnSta(() =>
            {
                var effect = new ResizeEffect(sourceWidth, sourceHeight, true);
                var window = new ResizeSettingsWindow(effect);
                Find<CheckBox>(window, "AllowUpscaleCheckBox").IsChecked = true;
                Assert.True(RunDialog(window, () =>
                {
                    Find<TextBox>(window, editWidth ? "WidthTextBox" : "HeightTextBox").Text = input;
                    Assert.Equal(counterpart, Find<TextBox>(window, editWidth ? "HeightTextBox" : "WidthTextBox").Text);
                    Assert.True(Find<Button>(window, "OkButton").IsEnabled);
                    Find<Button>(window, "OkButton").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                }));

                Assert.Equal(requestedWidth, effect.Width);
                Assert.Equal(requestedHeight, effect.Height);
                using var source = new Bitmap(sourceWidth, sourceHeight);
                source.SetResolution(144, 96);
                using System.Drawing.Image resized = effect.Apply(source, null);
                Assert.Equal(expectedWidth, resized.Width);
                Assert.Equal(expectedHeight, resized.Height);
                Assert.Equal(144f, resized.HorizontalResolution);
                Assert.Equal(96f, resized.VerticalResolution);
            });
        }

        private static T Find<T>(ResizeSettingsWindow window, string name) where T : FrameworkElement
        {
            var control = window.FindName(name) as T;
            Assert.NotNull(control);
            return control;
        }

        private static Button FindCancelButton(DependencyObject parent)
        {
            foreach (var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
            {
                if (child is Button button && button.IsCancel)
                {
                    return button;
                }

                Button nested = FindCancelButton(child);
                if (nested != null)
                {
                    return nested;
                }
            }
            return null;
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
                throw new Exception("Resize dialog interaction failed", interactionException);
            }
            return result;
        }

        private static void RunOnSta(Action action, string culture = "en-US")
        {
            Exception threadException = null;
            Dispatcher dispatcher = null;
            var thread = new Thread(() =>
            {
                try
                {
                    Volatile.Write(ref dispatcher, Dispatcher.CurrentDispatcher);
                    Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo(culture);
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
                // Fail without aborting the STA thread. Request normal dispatcher shutdown,
                // and keep a stalled background thread from holding the test host open.
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
                throw new TimeoutException("Resize STA test did not complete within 30 seconds. Normal dispatcher shutdown was requested without aborting the thread.");
            }
            if (threadException != null)
            {
                throw new Exception("Resize test failed on the STA thread", threadException);
            }
        }
    }
}
