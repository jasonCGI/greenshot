/*
 * Greenshot - a free and open source screenshot tool
 * Copyright (C) 2007-2026 Thomas Braun, Jens Klingen, Robin Krom
 * Licensed under the GNU General Public License, version 1 or later.
 * See https://www.gnu.org/licenses/ and https://getgreenshot.org/.
 */

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Greenshot.Base.Core;
using Greenshot.Base.Languages;

namespace Greenshot.Base.Wpf
{
    /// <summary>Returns a validated value; no preference changes occur inside this prompt.</summary>
    public sealed class CustomDpiWindow : Window
    {
        public int Value { get; private set; }

        public CustomDpiWindow(int initialValue)
        {
            Value = initialValue;
            string title = Texts.Settings.ExportdpiCustomValue;
            ThemedControls.ApplyDialogLook(this, title);
            Width = 380;
            SizeToContent = SizeToContent.Height;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            var input = new TextBox
            {
                Name = "CustomDpiValue", Text = initialValue.ToString(CultureInfo.InvariantCulture),
                Foreground = WpfThemeHelper.TextPrimary, Background = ThemeManager.Instance.TextBoxBackgroundBrush,
                BorderBrush = WpfThemeHelper.CardBorder, Padding = new Thickness(6, 4, 6, 4),
                Width = 110, HorizontalAlignment = HorizontalAlignment.Left
            };
            var error = new TextBlock
            {
                Name = "CustomDpiValidation", Text = Texts.Settings.ExportdpiCustomInvalid,
                Foreground = WpfThemeHelper.ErrorText, TextWrapping = TextWrapping.Wrap,
                Visibility = Visibility.Collapsed, Margin = new Thickness(0, 4, 0, 0)
            };
            input.TextChanged += (sender, args) => error.Visibility = Visibility.Collapsed;
            var ok = ThemedControls.CreateButton(Texts.Core.Ok, true, false);
            ok.Name = "CustomDpiOk";
            ok.Click += (sender, args) =>
            {
                if (!ExportDpiSettings.TryParseCustomDpi(input.Text, out int dpi))
                {
                    error.Visibility = Visibility.Visible;
                    input.Focus();
                    return;
                }
                Value = dpi;
                DialogResult = true;
            };
            var cancel = ThemedControls.CreateButton(Texts.Core.Cancel, false, true);
            cancel.Name = "CustomDpiCancel";
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 14, 0, 0) };
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);
            var root = new StackPanel { Margin = new Thickness(20, 16, 20, 16) };
            root.Children.Add(ThemedControls.CreateHeader(this, title));
            root.Children.Add(new Label { Content = Texts.Settings.ExportdpiCustomValue, Target = input,
                Foreground = WpfThemeHelper.TextPrimary });
            root.Children.Add(input);
            root.Children.Add(error);
            root.Children.Add(new TextBlock { Text = Texts.Settings.ExportdpiDescription, TextWrapping = TextWrapping.Wrap,
                Foreground = WpfThemeHelper.TextPrimary, Margin = new Thickness(0, 8, 0, 0) });
            root.Children.Add(buttons);
            Content = root;
            Loaded += (sender, args) => { input.Focus(); input.SelectAll(); };
        }
    }
}
