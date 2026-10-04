/*
 * Greenshot - a free and open source screenshot tool
 * Copyright (C) 2007-2026 Thomas Braun, Jens Klingen, Robin Krom
 * Licensed under the GNU General Public License, version 1 or later.
 * See https://www.gnu.org/licenses/ and https://getgreenshot.org/.
 */

namespace Greenshot.Base.Core
{
    /// <summary>UI magnification is additional to the monitor's normal DPI handling.</summary>
    public static class UiScaleSettings
    {
        public static bool IsSupported(int percent) => percent == 100 || percent == 125 || percent == 150 || percent == 200;
        public static double Factor(int percent) => IsSupported(percent) ? percent / 100.0 : 1.0;
    }
}
