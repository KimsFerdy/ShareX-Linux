#region License Information (GPL v3)

/*
    ShareX - A program that allows you to take screenshots and share any file type
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using ShareX.HelpersLib;
using SkiaSharp;
using System.Drawing;

namespace ShareX.Tools;

/// <summary>
/// A transparent, always on top window over one monitor. Its X11 input shape is empty, so clicks reach the windows
/// below. Each frame is recorded on the UI thread and replayed on Avalonia's render thread.
/// </summary>
internal sealed class MouseHighlighterX11Overlay : IMouseHighlighterOverlay
{
    private readonly MouseHighlighterService _service;
    private readonly Rectangle _screenBounds;
    private readonly OverlayCanvas _canvas;
    private readonly Window _window;
    private bool _hadEffects;

    public MouseHighlighterX11Overlay(MouseHighlighterService service, PixelRect bounds, double scaling)
    {
        _service = service;
        _screenBounds = new Rectangle(bounds.X, bounds.Y, bounds.Width, bounds.Height);
        _canvas = new OverlayCanvas(_screenBounds, scaling);
        _window = new Window
        {
            Title = "ShareX - Mouse highlighter overlay",
            WindowDecorations = WindowDecorations.None,
            ShowInTaskbar = false,
            ShowActivated = false,
            Topmost = true,
            CanResize = false,
            Focusable = false,
            TransparencyLevelHint = [WindowTransparencyLevel.Transparent],
            Background = Avalonia.Media.Brushes.Transparent,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Position = bounds.Position,
            Width = bounds.Width / scaling,
            Height = bounds.Height / scaling,
            Content = _canvas
        };
        _window.Opened += OnOpened;
        _window.Show();
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        IntPtr handle = _window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (!X11Desktop.SetWindowInputShape(handle, []))
        {
            DebugHelper.WriteLine("Mouse highlighter overlay could not be made click-through.");
        }
    }

    public void Refresh()
    {
        MouseHighlighterOptions options = _service.Options;
        Rectangle effects = MouseHighlighterOverlayWindow.GetEffectBounds(_service, options, _screenBounds);
        bool hasEffects = effects.Width > 0 && effects.Height > 0;

        // A last empty frame clears the previous effects; after that the window stays idle.
        if (hasEffects || _hadEffects)
        {
            SKPicture? picture = null;
            if (hasEffects)
            {
                using SKPictureRecorder recorder = new();
                SKCanvas canvas = recorder.BeginRecording(SKRect.Create(_screenBounds.X, _screenBounds.Y, _screenBounds.Width, _screenBounds.Height));
                MouseHighlighterOverlayWindow.Draw(canvas, _service, options);
                picture = recorder.EndRecording();
            }

            _canvas.SetPicture(picture);
        }

        _hadEffects = hasEffects;
    }

    public void Dispose()
    {
        _window.Opened -= OnOpened;
        _window.Close();
        _canvas.SetPicture(null);
    }

    /// <summary>A picture shared by the canvas and the draw operations that render it, disposed by whichever releases it last.</summary>
    private sealed class SharedPicture(SKPicture picture)
    {
        private int _references = 1;

        public SKPicture Picture => picture;

        public SharedPicture Acquire()
        {
            Interlocked.Increment(ref _references);
            return this;
        }

        public void Release()
        {
            if (Interlocked.Decrement(ref _references) == 0) picture.Dispose();
        }
    }

    private sealed class OverlayCanvas(Rectangle screenBounds, double scaling) : Control
    {
        private SharedPicture? _picture;

        public void SetPicture(SKPicture? picture)
        {
            _picture?.Release();
            _picture = picture == null ? null : new SharedPicture(picture);
            InvalidateVisual();
        }

        public override void Render(DrawingContext context)
        {
            if (_picture != null)
            {
                // Rendering happens later on the render thread, possibly after the next frame replaces this one.
                context.Custom(new PictureDrawOperation(new Rect(Bounds.Size), _picture.Acquire(), screenBounds, scaling));
            }
        }
    }

    private sealed class PictureDrawOperation(Rect bounds, SharedPicture picture, Rectangle screenBounds, double scaling) : ICustomDrawOperation
    {
        public Rect Bounds => bounds;

        public bool HitTest(Avalonia.Point p) => false;

        public bool Equals(ICustomDrawOperation? other) => false;

        public void Render(ImmediateDrawingContext context)
        {
            ISkiaSharpApiLeaseFeature? leaseFeature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            if (leaseFeature == null) return;

            using ISkiaSharpApiLease lease = leaseFeature.Lease();
            SKCanvas canvas = lease.SkCanvas;
            canvas.Save();
            canvas.Scale((float)(1 / scaling));
            canvas.Translate(-screenBounds.X, -screenBounds.Y);
            canvas.DrawPicture(picture.Picture);
            canvas.Restore();
        }

        public void Dispose() => picture.Release();
    }
}
