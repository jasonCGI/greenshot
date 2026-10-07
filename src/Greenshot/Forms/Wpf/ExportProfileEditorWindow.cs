/* Greenshot, GNU General Public License version 1 or later. */
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Dapplo.Ini;
using Greenshot.Base.Core;
using Greenshot.Base.Core.Enums;
using Greenshot.Base.Core.FileFormat;
using Greenshot.Base.Wpf;

namespace Greenshot.Forms.Wpf
{
    public sealed class ExportProfileEditorWindow : Window
    {
        public ExportProfile Result { get; private set; }
        public ExportProfileEditorWindow(ExportProfile original)
        {
            ThemedControls.ApplyDialogLook(this, "Edit export profile");
            Width = 460; SizeToContent = SizeToContent.Height;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            var root = new StackPanel { Margin = new Thickness(20) };
            root.Children.Add(new TextBlock { Text = "Edit the saved profile. Output defaults stay unchanged.", TextWrapping = TextWrapping.Wrap });
            TextBox Input(string label, string value)
            {
                root.Children.Add(new Label { Content = label });
                var input = new TextBox { Text = value, Margin = new Thickness(0, 0, 0, 4) };
                System.Windows.Automation.AutomationProperties.SetName(input, label);
                root.Children.Add(input); return input;
            }
            CheckBox Check(string label, bool value)
            {
                var input = ThemedControls.CreateCheckBox(label, value);
                root.Children.Add(input); return input;
            }
            var name = Input("Profile name", original.Name); name.MaxLength = 64;
            root.Children.Add(new Label { Content = "Image format" });
            var format = new ComboBox { ItemsSource = SimpleServiceProvider.Current.GetInstance<IFileFormatRegistry>()
                .GetSaveableFileFormats().Select(f => f.Id).ToArray(), SelectedItem = original.Format };
            root.Children.Add(format);
            root.Children.Add(new Label { Content = "PNG/JPEG DPI preset" });
            var dpi = new ComboBox { ItemsSource = Enum.GetValues(typeof(ExportDpiPreset)), SelectedItem = original.DpiPreset };
            root.Children.Add(dpi);
            var custom = Input("Custom DPI (1 to 2400)", original.CustomDpi.ToString());
            var jpeg = Input("JPEG quality (0 to 100)", original.JpegQuality.ToString());
            var webp = Input("WebP lossy quality (0 to 100)", original.WebpQuality.ToString());
            var lossless = Check("Lossless WebP", original.WebpLossless);
            var reduce = Check("Reduce colors", original.ReduceColors);
            var auto = Check("Automatically reduce colors", original.AutoReduceColors);
            var prompt = Check("Review settings before saving", original.PromptQuality);
            void UpdateActiveControls()
            {
                string selected = format.SelectedItem as string;
                bool densitySupported = selected == "png" || selected == "jpg";
                bool isWebp = selected == "webp";
                dpi.IsEnabled = densitySupported;
                custom.IsEnabled = densitySupported && Equals(dpi.SelectedItem, ExportDpiPreset.Custom);
                jpeg.IsEnabled = selected == "jpg";
                lossless.IsEnabled = isWebp; webp.IsEnabled = isWebp && lossless.IsChecked != true;
                reduce.IsEnabled = !isWebp; auto.IsEnabled = !isWebp;
            }
            format.SelectionChanged += (s, e) => UpdateActiveControls();
            dpi.SelectionChanged += (s, e) => UpdateActiveControls();
            lossless.Checked += (s, e) => UpdateActiveControls();
            lossless.Unchecked += (s, e) => UpdateActiveControls();
            UpdateActiveControls();
            root.Children.Add(new TextBlock { Text = "DPI presets apply to PNG and JPEG. WebP ignores DPI presets and color reduction.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) });
            var error = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
            root.Children.Add(error);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var save = ThemedControls.CreateButton("Save profile", true, false);
            save.Click += (s, e) =>
            {
                try
                {
                    if (!int.TryParse(custom.Text, out int customValue) || !int.TryParse(jpeg.Text, out int jpegValue) || !int.TryParse(webp.Text, out int webpValue))
                        throw new ArgumentException("DPI and quality must be whole numbers.");
                    var edited = original.Copy(name.Text);
                    edited.Format = format.SelectedItem as string;
                    edited.DpiPreset = dpi.SelectedItem is ExportDpiPreset preset ? preset : ExportDpiPreset.Preserve;
                    edited.CustomDpi = customValue; edited.JpegQuality = jpegValue; edited.WebpQuality = webpValue;
                    edited.WebpLossless = lossless.IsChecked == true; edited.ReduceColors = reduce.IsChecked == true;
                    edited.AutoReduceColors = auto.IsChecked == true; edited.PromptQuality = prompt.IsChecked == true;
                    edited.Validate(); Result = edited; DialogResult = true;
                }
                catch (ArgumentException ex) { error.Text = ex.Message; }
            };
            buttons.Children.Add(save); buttons.Children.Add(ThemedControls.CreateButton("Cancel", false, true));
            root.Children.Add(buttons); Content = root;
            UiScaleManager.Attach(this, IniConfigRegistry.GetSection<ICoreConfiguration>());
        }
    }
}
