/* Greenshot, licensed under the GNU General Public License, version 1 or later. */
using System.Drawing;
using System.Globalization;
using Greenshot.Base.Interfaces.Plugin;

namespace Greenshot.Base.Core
{
    public static class ExportMenuSummary
    {
        public static string Describe(SurfaceOutputSettings settings, Size size)
        {
            string density;
            if (settings.Format != "png" && settings.Format != "jpg" && settings.Format != "jpeg")
                density = "DPI preset unavailable";
            else
            {
                float? preset = ExportDpiSettings.GetResolution(settings);
                density = preset.HasValue ? preset.Value.ToString("0.##", CultureInfo.InvariantCulture) + " DPI"
                    : "Preserve source DPI";
            }
            return settings.Format.ToUpperInvariant() + " · " + density +
                (size.IsEmpty ? "" : $" · {size.Width} × {size.Height} px");
        }
    }
}
