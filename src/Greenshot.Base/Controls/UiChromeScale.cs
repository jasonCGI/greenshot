/*
 * Greenshot - a free and open source screenshot tool
 * Copyright (C) 2007-2026 Thomas Braun, Jens Klingen, Robin Krom
 * Licensed under the GNU General Public License, version 1 or later.
 * See https://www.gnu.org/licenses/ and https://getgreenshot.org/.
 */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using Greenshot.Base.Core;
using Greenshot.Base.Threading;

namespace Greenshot.Base.Controls
{
    /// <summary>Magnify menus and toolbars without scaling the editor canvas or capture overlays.</summary>
    public sealed class UiChromeScale : IDisposable
    {
        private readonly Form _form;
        private readonly ICoreConfiguration _configuration;
        private readonly List<ToolStrip> _strips = new();
        private readonly ConditionalWeakTable<ToolStrip, StripBaseline> _baselines = new();
        private readonly ConditionalWeakTable<ToolStripItem, ItemBaseline> _items = new();
        private readonly PropertyChangedEventHandler _changed;
        private bool _disposed;
        private bool _refreshing;

        public UiChromeScale(Form form, ICoreConfiguration configuration)
        {
            _form = form;
            _configuration = configuration;
            _changed = (sender, args) =>
            {
                if (args.PropertyName != nameof(ICoreConfiguration.UiScalePercent)) return;
                if (_disposed || form.IsDisposed) return;
                UiDispatcher.Current.InvokeAsync(Refresh).FireAndLog("Apply UI size to menus and toolbars");
            };
            configuration.PropertyChanged += _changed;
            form.FormClosed += (sender, args) => Dispose();
            form.DpiChanged += (sender, args) =>
            {
                if (form.IsHandleCreated) form.BeginInvoke(new Action(Refresh));
            };
            Discover(form);
        }

        private void Discover(Control control)
        {
            if (control is ToolStrip strip) Add(strip);
            if (control.ContextMenuStrip != null) Add(control.ContextMenuStrip);
            foreach (Control child in control.Controls) Discover(child);
        }

        public void Add(ToolStrip strip)
        {
            if (_strips.Contains(strip)) return;
            _strips.Add(strip);
            var baseline = new StripBaseline(strip, _form.DeviceDpi);
            _baselines.Add(strip, baseline);
            strip.Disposed += (sender, args) => baseline.Dispose();
            strip.ItemAdded += (sender, args) => Refresh();
            strip.VisibleChanged += (sender, args) => Refresh();
            Refresh();
        }

        public void Refresh()
        {
            if (_disposed || _form.IsDisposed || _refreshing) return;
            _refreshing = true;
            try
            {
            double factor = UiScaleSettings.Factor(_configuration.UiScalePercent);
            foreach (var strip in _strips)
            {
                if (strip.IsDisposed) continue;
                var baseline = _baselines.GetValue(strip, key => new StripBaseline(key, _form.DeviceDpi));
                double dpiFactor = _form.DeviceDpi / (double)baseline.Dpi;
                // Snapshot item fonts before changing their inherited strip font.
                RememberItems(strip.Items, baseline.Font);
                strip.SuspendLayout();
                strip.Font = baseline.FontFor(factor);
                strip.ImageScalingSize = Scale(baseline.Images, factor * dpiFactor);
                ApplyItems(strip.Items, baseline.Font, factor, dpiFactor);
                strip.ResumeLayout(true);
            }
            }
            finally { _refreshing = false; }
        }

        private void RememberItems(ToolStripItemCollection items, Font inheritedFont)
        {
            foreach (ToolStripItem item in items)
            {
                _items.GetValue(item, key =>
                {
                    var baseline = new ItemBaseline(key,
                        key.Font.Equals(key.Owner?.Font) ? inheritedFont : key.Font);
                    key.Disposed += (sender, args) => baseline.Dispose();
                    return baseline;
                });
                if (item is ToolStripDropDownItem dropDown)
                    RememberItems(dropDown.DropDownItems, inheritedFont);
            }
        }

        private void ApplyItems(ToolStripItemCollection items, Font inheritedFont, double factor, double dpiFactor)
        {
            foreach (ToolStripItem item in items)
            {
                var baseline = _items.GetValue(item, key => new ItemBaseline(key, inheritedFont));
                item.Font = baseline.FontFor(factor);
                item.Padding = Scale(baseline.Padding, factor * dpiFactor);
                item.Margin = Scale(baseline.Margin, factor * dpiFactor);
                if (!item.AutoSize || item is ToolStripControlHost)
                    item.Size = Scale(baseline.Size, factor * dpiFactor);
                if (item is ToolStripControlHost host) host.Control.Font = item.Font;
                if (item is ToolStripDropDownItem dropDown)
                {
                    dropDown.DropDown.Font = item.Font;
                    dropDown.DropDown.ImageScalingSize = Scale(baseline.DropDownImages, factor * dpiFactor);
                    ApplyItems(dropDown.DropDownItems, inheritedFont, factor, dpiFactor);
                }
            }
        }

        private static Size Scale(Size value, double factor) => new(
            Math.Max(1, (int)Math.Round(value.Width * factor)), Math.Max(1, (int)Math.Round(value.Height * factor)));
        private static Padding Scale(Padding value, double factor) => new(
            (int)Math.Round(value.Left * factor), (int)Math.Round(value.Top * factor),
            (int)Math.Round(value.Right * factor), (int)Math.Round(value.Bottom * factor));

        private class FontBaseline
        {
            public Font Font { get; }
            private Font _scaled;
            private double _factor = 1;
            public FontBaseline(Font font) { Font = font; }
            public Font FontFor(double factor)
            {
                if (_factor == factor) return _scaled ?? Font;
                var previous = _scaled;
                _scaled = factor == 1 ? null : new Font(Font.FontFamily, (float)(Font.Size * factor), Font.Style, Font.Unit);
                _factor = factor;
                previous?.Dispose();
                return _scaled ?? Font;
            }
            public void Dispose() => _scaled?.Dispose();
        }

        private sealed class StripBaseline : FontBaseline
        {
            public Size Images { get; }
            public int Dpi { get; }
            public StripBaseline(ToolStrip strip, int dpi) : base(strip.Font)
            { Images = strip.ImageScalingSize; Dpi = dpi; }
        }

        private sealed class ItemBaseline : FontBaseline
        {
            public Size Size { get; }
            public Padding Padding { get; }
            public Padding Margin { get; }
            public Size DropDownImages { get; }
            public ItemBaseline(ToolStripItem item, Font inheritedFont) : base(inheritedFont)
            {
                Size = item.Size; Padding = item.Padding; Margin = item.Margin;
                DropDownImages = (item as ToolStripDropDownItem)?.DropDown.ImageScalingSize ?? new Size(16, 16);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _configuration.PropertyChanged -= _changed;
        }
    }
}
