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
using System.Windows.Input;
using System.Windows.Interop;
using Greenshot.Base.Core;
using Greenshot.Base.Effects;
using Greenshot.Base.Languages;
using log4net;

namespace Greenshot.Editor.Forms
{
    public partial class ResizeSettingsWindow : Window
    {
        private const int MaximumDimension = ImageHelper.MaximumResizeDimension;
        private const long MaximumPixelCount = ImageHelper.MaximumResizePixelCount;
        private static readonly ILog LOG = LogManager.GetLogger(typeof(ResizeSettingsWindow));
        private readonly ResizeEffect _effect;
        private readonly int _originalWidth;
        private readonly int _originalHeight;
        private readonly string _valuePixel;
        private readonly string _valuePercent;
        private string Inches => Texts.Editor.ResizeInches;
        private string Centimeters => Texts.Editor.ResizeCentimeters;
        private int _dpi = 300;
        private double _newWidth;
        private double _newHeight;
        private bool _isUpdating;
        private bool _isInitializing = true;

        public ResizeSettingsWindow() : this(new ResizeEffect(100, 100, true))
        {
        }

        public ResizeSettingsWindow(ResizeEffect effect)
        {
            _effect = effect ?? new ResizeEffect(100, 100, true);
            _originalWidth = _effect.Width;
            _originalHeight = _effect.Height;
            _valuePixel = Texts.Editor.ResizePixel;
            _valuePercent = Texts.Editor.ResizePercent;

            InitializeComponent();
            try
            {
                Icon = ImageHelper.ToBitmapSource(GreenshotResources.GetGreenshotIcon());
            }
            catch (Exception ex)
            {
                LOG.Debug("Could not set window icon", ex);
            }

            WidthUnitComboBox.Items.Add(_valuePixel);
            WidthUnitComboBox.Items.Add(_valuePercent);
            WidthUnitComboBox.Items.Add(Inches);
            WidthUnitComboBox.Items.Add(Centimeters);
            WidthUnitComboBox.SelectedItem = _valuePixel;

            HeightUnitComboBox.Items.Add(_valuePixel);
            HeightUnitComboBox.Items.Add(_valuePercent);
            HeightUnitComboBox.Items.Add(Inches);
            HeightUnitComboBox.Items.Add(Centimeters);
            HeightUnitComboBox.SelectedItem = _valuePixel;

            _newWidth = _effect.Width;
            _newHeight = _effect.Height;

            MaintainAspectRatioCheckBox.IsChecked = _effect.MaintainAspectRatio;

            DisplayWidth();
            DisplayHeight();

            WidthTextBox.TextChanged += WidthTextBox_TextChanged;
            HeightTextBox.TextChanged += HeightTextBox_TextChanged;
            WidthUnitComboBox.SelectionChanged += UnitComboBox_SelectionChanged;
            HeightUnitComboBox.SelectionChanged += UnitComboBox_SelectionChanged;
            MaintainAspectRatioCheckBox.Checked += MaintainAspectRatioCheckBox_Changed;
            MaintainAspectRatioCheckBox.Unchecked += MaintainAspectRatioCheckBox_Changed;

            DpiTextBox.TextChanged += DpiTextBox_TextChanged;
            AllowUpscaleCheckBox.Checked += (s, e) => UpdateValidation();
            AllowUpscaleCheckBox.Unchecked += (s, e) => UpdateValidation();
            _isInitializing = false;
            UpdateValidation();
        }

        private void DisplayWidth()
        {
            if (WidthTextBox == null || WidthUnitComboBox == null || _valuePercent == null) return;
            _isUpdating = true;
            try
            {
                double displayValue = _valuePercent.Equals(WidthUnitComboBox.SelectedItem)
                    ? (_originalWidth > 0 ? (_newWidth / _originalWidth * 100.0) : 0)
                    : ToDisplay(_newWidth, WidthUnitComboBox);
                WidthTextBox.Text = FormatDimension(displayValue);
            }
            finally
            {
                _isUpdating = false;
            }
        }

        private void DisplayHeight()
        {
            if (HeightTextBox == null || HeightUnitComboBox == null || _valuePercent == null) return;
            _isUpdating = true;
            try
            {
                double displayValue = _valuePercent.Equals(HeightUnitComboBox.SelectedItem)
                    ? (_originalHeight > 0 ? (_newHeight / _originalHeight * 100.0) : 0)
                    : ToDisplay(_newHeight, HeightUnitComboBox);
                HeightTextBox.Text = FormatDimension(displayValue);
            }
            finally
            {
                _isUpdating = false;
            }
        }

        private void WidthTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isInitializing || _isUpdating) return;

            if (!TryParseInput(WidthTextBox.Text, WidthUnitComboBox,
                _originalWidth, out _newWidth))
            {
                UpdateValidation();
                return;
            }

            if (MaintainAspectRatioCheckBox.IsChecked == true && _originalWidth > 0)
            {
                _newHeight = _originalHeight * _newWidth / _originalWidth;
                DisplayHeight();
            }
            UpdateValidation();
        }

        private void HeightTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isInitializing || _isUpdating) return;

            if (!TryParseInput(HeightTextBox.Text, HeightUnitComboBox,
                _originalHeight, out _newHeight))
            {
                UpdateValidation();
                return;
            }

            if (MaintainAspectRatioCheckBox.IsChecked == true && _originalHeight > 0)
            {
                _newWidth = _originalWidth * _newHeight / _originalHeight;
                DisplayWidth();
            }
            UpdateValidation();
        }

        private void UnitComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            if (ReferenceEquals(sender, WidthUnitComboBox) && IsPositiveFinite(_newWidth))
            {
                DisplayWidth();
            }
            else if (ReferenceEquals(sender, HeightUnitComboBox) && IsPositiveFinite(_newHeight))
            {
                DisplayHeight();
            }
            UpdateValidation();
        }

        private void MaintainAspectRatioCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            if (MaintainAspectRatioCheckBox.IsChecked == true && _originalWidth > 0 && IsPositiveFinite(_newWidth))
            {
                _newHeight = _originalHeight * _newWidth / _originalWidth;
                DisplayHeight();
            }
            UpdateValidation();
        }

        private bool IsPhysical(ComboBox unit) => Equals(unit.SelectedItem, Inches) || Equals(unit.SelectedItem, Centimeters);
        private bool UsesPhysical => IsPhysical(WidthUnitComboBox) || IsPhysical(HeightUnitComboBox);

        private double ToDisplay(double pixels, ComboBox unit)
        {
            return IsPhysical(unit) ? pixels / _dpi * (Equals(unit.SelectedItem, Centimeters) ? 2.54 : 1) : pixels;
        }

        private bool TryParseInput(string text, ComboBox unit, int originalSize, out double pixels)
        {
            if (!TryParseDimension(text, _valuePercent.Equals(unit.SelectedItem), originalSize, out pixels)) return false;
            if (IsPhysical(unit)) pixels = pixels * _dpi / (Equals(unit.SelectedItem, Centimeters) ? 2.54 : 1);
            return IsPositiveFinite(pixels);
        }

        private void DpiTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isInitializing || _isUpdating) return;
            if (ExportDpiSettings.TryParseCustomDpi(DpiTextBox.Text, out int dpi))
            {
                _dpi = dpi;
                WidthTextBox_TextChanged(WidthTextBox, null);
                if (MaintainAspectRatioCheckBox.IsChecked != true) HeightTextBox_TextChanged(HeightTextBox, null);
            }
            UpdateValidation();
        }

        private static string FormatDimension(double value)
        {
            return IsPositiveFinite(value)
                ? value.ToString("0.#################", CultureInfo.CurrentCulture)
                : string.Empty;
        }

        private static bool IsPositiveFinite(double value)
        {
            return value > 0 && !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static bool TryParseDimension(string text, bool isPercent, int originalSize, out double pixels)
        {
            const NumberStyles Styles = NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign |
                NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite;
            pixels = double.NaN;
            if ((!double.TryParse(text, Styles, CultureInfo.CurrentCulture, out double parsed) &&
                 !double.TryParse(text, Styles, CultureInfo.InvariantCulture, out parsed)) || !IsPositiveFinite(parsed))
            {
                return false;
            }

            pixels = isPercent ? originalSize * parsed / 100.0 : parsed;
            return IsPositiveFinite(pixels);
        }

        private bool TryGetDimensions(out int width, out int height, out string error)
        {
            width = 0;
            height = 0;
            error = Texts.Editor.ResizeValidationInvalid;
            if (UsesPhysical && !ExportDpiSettings.TryParseCustomDpi(DpiTextBox.Text, out _))
            {
                error = Texts.Editor.ResizeDpiInvalid;
                return false;
            }
            if (!TryParseInput(WidthTextBox.Text, WidthUnitComboBox,
                    _originalWidth, out double pixelWidth) ||
                !TryParseInput(HeightTextBox.Text, HeightUnitComboBox,
                    _originalHeight, out double pixelHeight))
            {
                return false;
            }

            double roundedWidth = Math.Round(pixelWidth);
            double roundedHeight = Math.Round(pixelHeight);
            error = Texts.Editor.ResizeValidationLimits;
            // Dialog guardrails limit accidental allocations; they do not guarantee available memory.
            if (roundedWidth < 1 || roundedHeight < 1 || roundedWidth > MaximumDimension ||
                roundedHeight > MaximumDimension || roundedWidth * roundedHeight > MaximumPixelCount)
            {
                return false;
            }

            if (_originalWidth > 0 && _originalHeight > 0)
            {
                error = Texts.Editor.ResizeValidationTooSmall;
                try
                {
                    // Use the same validated rounding as the production resize path.
                    ImageHelper.GetResizeSize(new System.Drawing.Size(_originalWidth, _originalHeight),
                        MaintainAspectRatioCheckBox.IsChecked == true, (int)roundedWidth, (int)roundedHeight);
                }
                catch (ArgumentOutOfRangeException)
                {
                    return false;
                }
            }

            width = (int)roundedWidth;
            height = (int)roundedHeight;
            error = null;
            return true;
        }

        private void UpdateValidation()
        {
            bool valid = TryGetDimensions(out int width, out int height, out string error);
            DpiTextBox.IsEnabled = UsesPhysical;
            var finalSize = valid ? ImageHelper.GetResizeSize(new System.Drawing.Size(_originalWidth, _originalHeight),
                MaintainAspectRatioCheckBox.IsChecked == true, width, height) : System.Drawing.Size.Empty;
            bool upscale = valid && (finalSize.Width > _originalWidth || finalSize.Height > _originalHeight);
            AllowUpscaleCheckBox.Visibility = upscale ? Visibility.Visible : Visibility.Collapsed;
            ResizeSummary.Text = valid ? $"{finalSize.Width} x {finalSize.Height} pixels. " + (UsesPhysical ? $"Resample and set {_dpi} DPI. " : "Resample; preserve source DPI. ") +
                (MaintainAspectRatioCheckBox.IsChecked == true ? "Keep aspect ratio." : "Unlocked: proportions may change.") : string.Empty;
            if (upscale && AllowUpscaleCheckBox.IsChecked != true)
            {
                valid = false;
                error = Texts.Editor.ResizeUpscaleWarning;
            }
            OkButton.IsEnabled = valid;
            ValidationMessage.Text = error ?? string.Empty;
            ValidationMessage.Visibility = valid ? Visibility.Collapsed : Visibility.Visible;
        }

        public bool? ShowDialog(System.Windows.Forms.IWin32Window owner)
        {
            if (owner != null)
            {
                new WindowInteropHelper(this) { Owner = owner.Handle };
            }
            return ShowDialog();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1)
            {
                DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            // Validate the current text again, including clicks raised while the button is disabled.
            if (!TryGetDimensions(out int width, out int height, out _) ||
                ((width > _originalWidth || height > _originalHeight) && AllowUpscaleCheckBox.IsChecked != true))
            {
                UpdateValidation();
                return;
            }

            if (width != _effect.Width || height != _effect.Height ||
                _effect.MaintainAspectRatio != (MaintainAspectRatioCheckBox.IsChecked == true) ||
                (UsesPhysical && _effect.ResolutionDpi != _dpi))
            {
                _effect.ResolutionDpi = UsesPhysical ? _dpi : (int?)null;
                _effect.Width = width;
                _effect.Height = height;
                _effect.MaintainAspectRatio = MaintainAspectRatioCheckBox.IsChecked == true;
                DialogResult = true;
            }
            else
            {
                DialogResult = false;
            }
            Close();
        }
    }
}
