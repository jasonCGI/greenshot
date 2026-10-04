/*
 * Greenshot - a free and open source screenshot tool
 * Copyright (C) 2007-2026 Thomas Braun, Jens Klingen, Robin Krom
 * Licensed under the GNU General Public License, version 1 or later.
 * See https://www.gnu.org/licenses/ and https://getgreenshot.org/.
 */

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using Greenshot.Base.Core;

namespace Greenshot.Base.Wpf
{
    /// <summary>Scale application content, retaining vector text and accessible scrolling on small screens.</summary>
    public static class UiScaleManager
    {
        private static int _initialized;
        private static readonly ConditionalWeakTable<Window, WindowScale> Windows = new();
        private static readonly ConditionalWeakTable<ComboBox, object> ComboBoxes = new();

        public static void Initialize()
        {
            if (Interlocked.Exchange(ref _initialized, 1) != 0) return;
            EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent,
                new RoutedEventHandler((sender, args) =>
                {
                    if (sender is Window window && ReferenceEquals(args.OriginalSource, window) &&
                        window.GetType().Assembly.GetName().Name.StartsWith("Greenshot", StringComparison.Ordinal))
                    {
                        Attach(window, IniConfigHelper.EnsureSection<ICoreConfiguration>(() => new CoreConfigurationImpl()));
                    }
                }));
            EventManager.RegisterClassHandler(typeof(ComboBox), FrameworkElement.LoadedEvent,
                new RoutedEventHandler((sender, args) =>
                {
                    if (!(sender is ComboBox combo) || !ReferenceEquals(args.OriginalSource, combo)) return;
                    ComboBoxes.GetValue(combo, key =>
                    {
                        key.DropDownOpened += (s, e) =>
                        {
                            var popup = key.Template?.FindName("PART_Popup", key) as Popup ??
                                key.Template?.FindName("Popup", key) as Popup;
                            if (popup?.Child is FrameworkElement child)
                            {
                                var config = IniConfigHelper.EnsureSection<ICoreConfiguration>(() => new CoreConfigurationImpl());
                                double factor = UiScaleSettings.Factor(config.UiScalePercent);
                                child.LayoutTransform = new ScaleTransform(factor, factor);
                            }
                        };
                        return new object();
                    });
                }));
        }

        public static void Attach(Window window, ICoreConfiguration configuration)
        {
            if (!(window.Content is FrameworkElement)) return;
            Windows.GetValue(window, key => new WindowScale(key, configuration)).Apply();
        }

        private sealed class WindowScale
        {
            private readonly Window _window;
            private readonly ICoreConfiguration _configuration;
            private readonly FrameworkElement _content;
            private readonly Transform _originalTransform;
            private readonly double _width, _height, _minWidth, _minHeight;
            private readonly double _contentWidth, _contentHeight;
            private readonly SizeToContent _sizeToContent;
            private readonly PropertyChangedEventHandler _changed;
            private ScrollViewer _scroll;
            private double _factor = 1;
            private bool _applying;

            public WindowScale(Window window, ICoreConfiguration configuration)
            {
                _window = window;
                _configuration = configuration;
                _content = (FrameworkElement)window.Content;
                _originalTransform = _content.LayoutTransform;
                _width = window.Width;
                _height = window.Height;
                _minWidth = window.MinWidth;
                _minHeight = window.MinHeight;
                _contentWidth = _content.Width;
                _contentHeight = _content.Height;
                _sizeToContent = window.SizeToContent;
                // Install once, before a user changes the preference. Reparenting during a
                // binding update can reset ComboBox selection and keyboard focus.
                if (_sizeToContent == SizeToContent.Manual) InstallScrollContainer();
                var weak = new WeakReference<WindowScale>(this);
                _changed = (sender, args) =>
                {
                    if (args.PropertyName != nameof(ICoreConfiguration.UiScalePercent)) return;
                    if (!weak.TryGetTarget(out var state)) return;
                    if (state._window.Dispatcher.HasShutdownStarted) return;
                    if (state._window.Dispatcher.CheckAccess()) state.Apply();
                    else _ = state._window.Dispatcher.BeginInvoke(new Action(state.Apply));
                };
                configuration.PropertyChanged += _changed;
                window.Closed += (sender, args) => configuration.PropertyChanged -= _changed;
                window.SizeChanged += (sender, args) => { FitContent(); KeepVisible(); };
                window.LocationChanged += (sender, args) => ConstrainToScreen();
            }

            public void Apply()
            {
                if (_applying) return;
                _applying = true;
                try
                {
                    _factor = UiScaleSettings.Factor(_configuration.UiScalePercent);
                    // Leave the ordinary 100% small-dialog layout intact.
                    if (_factor == 1 && _scroll == null) return;
                    if (_scroll == null) InstallScrollContainer();
                    if (_factor == 1)
                    {
                        _content.LayoutTransform = _originalTransform;
                        _content.Width = _contentWidth;
                        _content.Height = _contentHeight;
                    }
                    else
                    {
                        var transforms = new TransformGroup();
                        transforms.Children.Add(_originalTransform);
                        transforms.Children.Add(new ScaleTransform(_factor, _factor));
                        _content.LayoutTransform = transforms;
                    }
                    ConstrainToScreen();
                    if (!double.IsNaN(_width)) _window.Width = Math.Min(_window.MaxWidth, _width * _factor);
                    if (!double.IsNaN(_height)) _window.Height = Math.Min(_window.MaxHeight, _height * _factor);
                    FitContent();
                    KeepVisible();
                }
                finally { _applying = false; }
            }

            private void ConstrainToScreen()
            {
                var bounds = WorkingArea(_window);
                _window.MinWidth = Math.Min(_minWidth * _factor, bounds.Width);
                _window.MinHeight = Math.Min(_minHeight * _factor, bounds.Height);
                _window.MaxWidth = bounds.Width;
                _window.MaxHeight = bounds.Height;
                if (_scroll != null)
                {
                    _scroll.MaxWidth = bounds.Width;
                    _scroll.MaxHeight = bounds.Height;
                }
            }

            private void InstallScrollContainer()
            {
                _window.Content = null;
                _scroll = new ScrollViewer
                {
                    Content = _content,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    CanContentScroll = false
                };
                _window.Content = _scroll;
                _scroll.ScrollChanged += (sender, args) => FitContent();
            }

            private void FitContent()
            {
                if (_scroll == null) return;
                // Preserve the original minimum logical layout; scroll when the screen cannot fit it.
                double width = _window.ActualWidth > 0 ? _window.ActualWidth : _window.Width;
                double height = _window.ActualHeight > 0 ? _window.ActualHeight : _window.Height;
                if (_scroll.ViewportWidth > 0) width = _scroll.ViewportWidth;
                if (_scroll.ViewportHeight > 0) height = _scroll.ViewportHeight;
                if (_sizeToContent != SizeToContent.Width && _sizeToContent != SizeToContent.WidthAndHeight && !double.IsNaN(width))
                    _content.Width = Math.Max(_minWidth,
                        (width - _window.BorderThickness.Left - _window.BorderThickness.Right) / _factor -
                        _content.Margin.Left - _content.Margin.Right);
                if (_sizeToContent == SizeToContent.Manual && !double.IsNaN(height)) _content.Height = Math.Max(_minHeight,
                    (height - _window.BorderThickness.Top - _window.BorderThickness.Bottom) / _factor);
            }

            private void KeepVisible()
            {
                if (!_window.IsVisible) return;
                var bounds = WorkingArea(_window);
                if (!double.IsNaN(_window.Left))
                    _window.Left = Math.Max(bounds.Left, Math.Min(_window.Left, bounds.Right - _window.ActualWidth));
                if (!double.IsNaN(_window.Top))
                    _window.Top = Math.Max(bounds.Top, Math.Min(_window.Top, bounds.Bottom - _window.ActualHeight));
            }
        }

        private static Rect WorkingArea(Window window)
        {
            var source = PresentationSource.FromVisual(window);
            if (source?.CompositionTarget == null) return SystemParameters.WorkArea;
            var screen = System.Windows.Forms.Screen.FromHandle(new WindowInteropHelper(window).Handle).WorkingArea;
            var transform = source.CompositionTarget.TransformFromDevice;
            var size = transform.Transform(new Vector(screen.Width, screen.Height));
            var origin = transform.Transform(new Point(screen.Left, screen.Top));
            return new Rect(origin.X, origin.Y, size.X, size.Y);
        }
    }
}
