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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Dapplo.Ini;
using Greenshot.Base.Core;
using Greenshot.Base.Core.Enums;
using Greenshot.Base.Core.FileFormat;
using Greenshot.Base.Interfaces.Plugin;
using Greenshot.Base.Languages;

namespace Greenshot.Base.Wpf
{
    /// <summary>
    /// Asks for JPEG or WebP quality, color reduction, and PNG/JPEG export resolution metadata.
    /// OK takes the values; Cancel, Escape or closing the window means the user doesn't want to save (the settings stay as they were).
    /// </summary>
    public sealed class QualityWindow : Window
    {
        private static ICoreConfiguration CoreConfig => IniConfigHelper.EnsureSection<ICoreConfiguration>(() => new CoreConfigurationImpl());

        private readonly SurfaceOutputSettings _settings;
        private readonly TextBlock _summary;
        private readonly bool _isWebp;
        private readonly Slider _qualitySlider;
        private readonly CheckBox _reduceColors;
        private readonly CheckBox _dontAskAgain;
        private readonly ComboBox _dpiPreset;
        private readonly TextBox _customDpi;
        private readonly TextBlock _dpiError;
        private readonly ComboBox _webpMode;
        private readonly Slider _webpQualitySlider;
        private readonly DockPanel _webpQualityRow;

        public QualityWindow(SurfaceOutputSettings settings)
        {
            _settings = settings;
            string title = Texts.Core.QualitydialogTitle;
            ThemedControls.ApplyDialogLook(this, title);
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Topmost = true;
            Width = 380;
            SizeToContent = SizeToContent.Height;
            try
            {
                Icon = ImageHelper.ToBitmapSource(GreenshotResources.GetGreenshotIcon());
            }
            catch
            {
                // The window works without an icon
            }

            bool isJpeg = WellKnownFileFormats.IsEqualFormat(WellKnownFileFormats.Jpg, settings.Format);
            _isWebp = WellKnownFileFormats.IsEqualFormat(WellKnownFileFormats.Webp, settings.Format);

            _reduceColors = ThemedControls.CreateCheckBox(Texts.Settings.Reducecolors, !_isWebp && settings.ReduceColors);
            _reduceColors.Name = "ReduceColorsInput";
            _reduceColors.IsEnabled = !_isWebp;

            var dpiLabel = new TextBlock
            {
                Text = Texts.Settings.Exportdpi,
                Foreground = WpfThemeHelper.TextPrimary,
                Margin = new Thickness(0, 10, 0, 4)
            };
            _dpiPreset = new ComboBox
            {
                Name = "ExportDpiPresetInput",
                SelectedValuePath = nameof(ComboBoxItem.Tag),
                IsEnabled = isJpeg || WellKnownFileFormats.IsEqualFormat(WellKnownFileFormats.Png, settings.Format)
            };
            _dpiPreset.Items.Add(new ComboBoxItem { Content = Texts.Settings.ExportdpiPreserve, Tag = ExportDpiPreset.Preserve });
            _dpiPreset.Items.Add(new ComboBoxItem { Content = Texts.Settings.ExportdpiWeb, Tag = ExportDpiPreset.Web });
            _dpiPreset.Items.Add(new ComboBoxItem { Content = Texts.Settings.ExportdpiPrint, Tag = ExportDpiPreset.Print });
            _dpiPreset.Items.Add(new ComboBoxItem { Content = Texts.Settings.ExportdpiCustom, Tag = ExportDpiPreset.Custom });
            _dpiPreset.SelectedValue = settings.ExportDpiPreset;
            _customDpi = new TextBox
            {
                Name = "CustomDpiInput",
                Text = settings.CustomDpi.ToString(CultureInfo.InvariantCulture),
                Width = 100,
                HorizontalAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(6, 4, 6, 4),
                Foreground = WpfThemeHelper.TextPrimary,
                Background = ThemeManager.Instance.TextBoxBackgroundBrush,
                BorderBrush = WpfThemeHelper.CardBorder
            };
            var customDpiLabel = new Label
            {
                Content = Texts.Settings.ExportdpiCustomValue,
                Target = _customDpi,
                Foreground = WpfThemeHelper.TextPrimary,
                Padding = new Thickness(0, 0, 12, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            var customDpiRow = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 6, 0, 0) };
            DockPanel.SetDock(customDpiLabel, Dock.Left);
            customDpiRow.Children.Add(customDpiLabel);
            customDpiRow.Children.Add(_customDpi);
            _dpiError = new TextBlock
            {
                Name = "CustomDpiError",
                Text = Texts.Settings.ExportdpiCustomInvalid,
                TextWrapping = TextWrapping.Wrap,
                Foreground = WpfThemeHelper.ErrorText,
                Margin = new Thickness(0, 4, 0, 0),
                Visibility = Visibility.Collapsed
            };
            _dpiPreset.SelectionChanged += (s, e) => UpdateCustomDpiState();
            _customDpi.TextChanged += (s, e) => _dpiError.Visibility = Visibility.Collapsed;
            UpdateCustomDpiState();
            var dpiDescription = new TextBlock
            {
                Text = Texts.Settings.ExportdpiDescription,
                TextWrapping = TextWrapping.Wrap,
                Foreground = WpfThemeHelper.TextPrimary,
                Margin = new Thickness(0, 4, 0, 0)
            };

            var qualityLabel = new TextBlock
            {
                Text = Texts.Core.JpegqualitydialogChoosejpegquality,
                TextWrapping = TextWrapping.Wrap,
                Foreground = WpfThemeHelper.TextPrimary,
                Margin = new Thickness(0, 10, 0, 4)
            };
            _qualitySlider = new Slider
            {
                Name = "JpegQualityInput",
                Minimum = 0,
                Maximum = 100,
                Value = settings.JPGQuality,
                SmallChange = 1,
                LargeChange = 10,
                TickFrequency = 10,
                TickPlacement = System.Windows.Controls.Primitives.TickPlacement.BottomRight,
                IsSnapToTickEnabled = false,
                VerticalAlignment = VerticalAlignment.Center
            };
            var qualityValue = new TextBlock
            {
                Width = 36,
                TextAlignment = TextAlignment.Right,
                Foreground = WpfThemeHelper.TextPrimary,
                VerticalAlignment = VerticalAlignment.Center,
                Text = settings.JPGQuality.ToString()
            };
            _qualitySlider.ValueChanged += (s, e) => qualityValue.Text = ((int)e.NewValue).ToString();
            var qualityRow = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(qualityValue, Dock.Right);
            qualityRow.Children.Add(qualityValue);
            qualityRow.Children.Add(_qualitySlider);
            // The quality only matters for JPEG
            qualityLabel.IsEnabled = qualityRow.IsEnabled = isJpeg;
            qualityLabel.Opacity = qualityRow.Opacity = isJpeg ? 1 : 0.5;
            qualityLabel.Visibility = qualityRow.Visibility = _isWebp ? Visibility.Collapsed : Visibility.Visible;

            var webpOptions = new StackPanel
            {
                Visibility = _isWebp ? Visibility.Visible : Visibility.Collapsed,
                Margin = new Thickness(0, 10, 0, 0)
            };
            _webpMode = new ComboBox
            {
                Name = "WebpModeInput",
                SelectedValuePath = nameof(ComboBoxItem.Tag),
                IsEnabled = _isWebp
            };
            _webpMode.Items.Add(new ComboBoxItem { Content = Texts.Settings.WebpLossy, Tag = false });
            _webpMode.Items.Add(new ComboBoxItem { Content = Texts.Settings.WebpLossless, Tag = true });
            _webpMode.SelectedValue = settings.WebpLossless;
            var webpModeLabel = new Label
            {
                Content = Texts.Settings.WebpMode,
                Target = _webpMode,
                Foreground = WpfThemeHelper.TextPrimary,
                Padding = new Thickness(0, 0, 0, 4)
            };
            _webpQualitySlider = new Slider
            {
                Name = "WebpQualityInput",
                Minimum = 0,
                Maximum = 100,
                Value = settings.WebpQuality,
                SmallChange = 1,
                LargeChange = 10,
                TickFrequency = 10,
                TickPlacement = System.Windows.Controls.Primitives.TickPlacement.BottomRight,
                IsSnapToTickEnabled = false,
                VerticalAlignment = VerticalAlignment.Center
            };
            var webpQualityValue = new TextBlock
            {
                Width = 36,
                TextAlignment = TextAlignment.Right,
                Foreground = WpfThemeHelper.TextPrimary,
                VerticalAlignment = VerticalAlignment.Center,
                Text = ((int)_webpQualitySlider.Value).ToString(CultureInfo.InvariantCulture)
            };
            _webpQualitySlider.ValueChanged += (s, e) => webpQualityValue.Text = ((int)e.NewValue).ToString(CultureInfo.InvariantCulture);
            var webpQualityLabel = new Label
            {
                Content = Texts.Settings.WebpQuality,
                Target = _webpQualitySlider,
                Foreground = WpfThemeHelper.TextPrimary,
                Padding = new Thickness(0, 0, 12, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            _webpQualityRow = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 6, 0, 0) };
            DockPanel.SetDock(webpQualityLabel, Dock.Left);
            DockPanel.SetDock(webpQualityValue, Dock.Right);
            _webpQualityRow.Children.Add(webpQualityLabel);
            _webpQualityRow.Children.Add(webpQualityValue);
            _webpQualityRow.Children.Add(_webpQualitySlider);
            _webpMode.SelectionChanged += (s, e) => UpdateWebpQualityState();
            UpdateWebpQualityState();
            webpOptions.Children.Add(webpModeLabel);
            webpOptions.Children.Add(_webpMode);
            webpOptions.Children.Add(_webpQualityRow);
            webpOptions.Children.Add(new TextBlock
            {
                Text = Texts.Settings.WebpDescription,
                TextWrapping = TextWrapping.Wrap,
                Foreground = WpfThemeHelper.TextPrimary,
                Margin = new Thickness(0, 4, 0, 0)
            });

            _dontAskAgain = ThemedControls.CreateCheckBox(Texts.Core.QualitydialogDontaskagain, false);
            _dontAskAgain.Name = "DontAskAgainInput";
            _dontAskAgain.Margin = new Thickness(0, 10, 0, 0);

            var ok = ThemedControls.CreateButton(Texts.Core.Ok, true, false);
            ok.Name = "QualityOkButton";
            ok.Click += (s, e) =>
            {
                if (Apply())
                {
                    DialogResult = true;
                }
            };
            var cancel = ThemedControls.CreateButton(Texts.Core.Cancel, false, true);
            cancel.Name = "QualityCancelButton";
            var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) };
            buttonRow.Children.Add(ok);
            buttonRow.Children.Add(cancel);

            var root = new StackPanel { Margin = new Thickness(20, 16, 20, 16) };
            root.Children.Add(ThemedControls.CreateHeader(this, title));
            root.Children.Add(_reduceColors);
            root.Children.Add(dpiLabel);
            root.Children.Add(_dpiPreset);
            root.Children.Add(customDpiRow);
            root.Children.Add(_dpiError);
            root.Children.Add(dpiDescription);
            root.Children.Add(qualityLabel);
            root.Children.Add(qualityRow);
            root.Children.Add(webpOptions);
            root.Children.Add(_dontAskAgain);
            root.Children.Add(buttonRow);
            _summary = new TextBlock { Name = "ExportSummaryText", TextWrapping = TextWrapping.Wrap,
                Foreground = WpfThemeHelper.TextPrimary, Margin = new Thickness(0, 12, 0, 0) };
            root.Children.Insert(root.Children.Count - 2, _summary);
            _dpiPreset.SelectionChanged += (s, e) => UpdateSummary();
            _customDpi.TextChanged += (s, e) => UpdateSummary();
            _qualitySlider.ValueChanged += (s, e) => UpdateSummary();
            _webpMode.SelectionChanged += (s, e) => UpdateSummary();
            _webpQualitySlider.ValueChanged += (s, e) => UpdateSummary();
            _reduceColors.Checked += (s, e) => UpdateSummary();
            _reduceColors.Unchecked += (s, e) => UpdateSummary();
            UpdateSummary();
            Content = root;
            Loaded += (s, e) =>
            {
                Activate();
                ok.Focus();
            };
        }

        /// <summary>
        /// The settings, changed when the user pressed OK
        /// </summary>
        public SurfaceOutputSettings Settings => _settings;

        private void UpdateCustomDpiState()
        {
            bool isCustom = _dpiPreset.IsEnabled && Equals(_dpiPreset.SelectedValue, ExportDpiPreset.Custom);
            _customDpi.IsEnabled = isCustom;
            _customDpi.Opacity = isCustom ? 1 : 0.5;
            if (!isCustom)
            {
                _dpiError.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateSummary()
        {
            var preview = new SurfaceOutputSettings(_settings.Format)
            {
                PreviewSize = _settings.PreviewSize, PreviewDpiX = _settings.PreviewDpiX, PreviewDpiY = _settings.PreviewDpiY,
                ExportDpiPreset = _dpiPreset.SelectedValue is ExportDpiPreset preset ? preset : _settings.ExportDpiPreset,
                CustomDpi = ExportDpiSettings.TryParseCustomDpi(_customDpi.Text, out int custom) ? custom : 0,
                JPGQuality = (int)_qualitySlider.Value, WebpLossless = Equals(_webpMode.SelectedValue, true),
                WebpQuality = (int)_webpQualitySlider.Value, ReduceColors = _reduceColors.IsChecked == true,
                DisableReduceColors = _settings.DisableReduceColors
            };
            _summary.Text = ExportSummary.Describe(preview);
        }

        private bool Apply()
        {
            if (_isWebp)
            {
                _settings.WebpLossless = Equals(_webpMode.SelectedValue, true);
                if (!_settings.WebpLossless)
                {
                    _settings.WebpQuality = (int)_webpQualitySlider.Value;
                }
                if (_dontAskAgain.IsChecked == true)
                {
                    CoreConfig.OutputFileWebpLossless = _settings.WebpLossless;
                    if (!_settings.WebpLossless)
                    {
                        CoreConfig.OutputFileWebpQuality = _settings.WebpQuality;
                    }
                    CoreConfig.OutputFilePromptQuality = false;
                }
                return true;
            }

            int customDpi = _settings.CustomDpi;
            if (_customDpi.IsEnabled && !ExportDpiSettings.TryParseCustomDpi(_customDpi.Text, out customDpi))
            {
                _dpiError.Visibility = Visibility.Visible;
                _customDpi.Focus();
                return false;
            }

            _settings.JPGQuality = (int)_qualitySlider.Value;
            _settings.ReduceColors = _reduceColors.IsChecked == true;
            if (_dpiPreset.IsEnabled && _dpiPreset.SelectedValue is ExportDpiPreset preset)
            {
                _settings.ExportDpiPreset = preset;
                if (preset == ExportDpiPreset.Custom)
                {
                    _settings.CustomDpi = customDpi;
                }
            }
            if (_dontAskAgain.IsChecked == true)
            {
                CoreConfig.OutputFileJpegQuality = _settings.JPGQuality;
                CoreConfig.OutputFilePromptQuality = false;
                CoreConfig.OutputFileReduceColors = _settings.ReduceColors;
                CoreConfig.OutputFileDpiPreset = _settings.ExportDpiPreset;
                if (_settings.ExportDpiPreset == ExportDpiPreset.Custom && _dpiPreset.IsEnabled)
                {
                    CoreConfig.OutputFileCustomDpi = _settings.CustomDpi;
                }
            }
            return true;
        }

        private void UpdateWebpQualityState()
        {
            bool isLossy = _isWebp && Equals(_webpMode.SelectedValue, false);
            _webpQualityRow.IsEnabled = isLossy;
            _webpQualityRow.Opacity = isLossy ? 1 : 0.5;
        }
    }

    /// <summary>
    /// Validate the active Preferences custom resolution before updating its configuration value.
    /// </summary>
    public sealed class ExportDpiValidationRule : ValidationRule
    {
        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            return ExportDpiSettings.TryParseCustomDpi(value as string, out _)
                ? ValidationResult.ValidResult
                : new ValidationResult(false, Texts.Settings.ExportdpiCustomInvalid);
        }
    }

    /// <summary>
    /// Export resolution controls apply only to the supported metadata formats.
    /// </summary>
    public sealed class ExportDpiFormatConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is string format &&
                   (WellKnownFileFormats.IsEqualFormat(WellKnownFileFormats.Png, format) ||
                    WellKnownFileFormats.IsEqualFormat(WellKnownFileFormats.Jpg, format));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }

    /// <summary>
    /// WebP-specific options do not change the settings for other image formats.
    /// </summary>
    public sealed class WebpFormatConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is string format && WellKnownFileFormats.IsEqualFormat(WellKnownFileFormats.Webp, format);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
