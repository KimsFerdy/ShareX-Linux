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

#nullable enable

using SkiaSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace ShareX.HelpersLib;

/// <summary>
/// Window, pointer and screen queries on an X11 desktop. Window handles are X window IDs of top-level client windows,
/// as listed by the window manager in _NET_CLIENT_LIST.
/// </summary>
public static unsafe class X11Desktop
{
    private static readonly object initLock = new();
    private static readonly ConcurrentDictionary<string, uint> atoms = new();
    private static IntPtr connection;
    private static uint rootWindow;
    private static bool? xfixesAvailable;

    public static bool IsAvailable => TryGetConnection(out _);

    private static IntPtr Connection
    {
        get
        {
            if (!TryGetConnection(out IntPtr c)) throw new InvalidOperationException("Unable to connect to the X server.");
            return c;
        }
    }

    private static bool TryGetConnection(out IntPtr result)
    {
        if (connection == IntPtr.Zero)
        {
            lock (initLock)
            {
                if (connection == IntPtr.Zero && LinuxEnvironment.IsX11)
                {
                    IntPtr c = XcbNative.Connect(null, out int screenNumber);
                    if (XcbNative.ConnectionHasError(c) == 0)
                    {
                        XcbNative.ScreenIterator iterator = XcbNative.SetupRootsIterator(XcbNative.GetSetup(c));
                        for (int i = 0; i < screenNumber && iterator.Rem > 0; i++) XcbNative.ScreenNext(ref iterator);
                        rootWindow = *(uint*)iterator.Data;
                        connection = c;
                    }
                    else
                    {
                        XcbNative.Disconnect(c);
                    }
                }
            }
        }

        result = connection;
        return result != IntPtr.Zero;
    }

    public static IntPtr RootWindow
    {
        get
        {
            _ = Connection;
            return (IntPtr)rootWindow;
        }
    }

    #region Windows

    public static IntPtr GetActiveWindow()
    {
        uint[] values = GetCardinalProperty((uint)RootWindow, "_NET_ACTIVE_WINDOW", 1);
        return values.Length > 0 ? (IntPtr)values[0] : IntPtr.Zero;
    }

    /// <summary>Top-level client windows in stacking order, topmost first.</summary>
    public static List<IntPtr> GetWindowsTopmostFirst()
    {
        uint root = (uint)RootWindow;
        uint[] windows = GetCardinalProperty(root, "_NET_CLIENT_LIST_STACKING", 4096);
        if (windows.Length == 0) windows = GetCardinalProperty(root, "_NET_CLIENT_LIST", 4096);

        List<IntPtr> result = new(windows.Length);
        for (int i = windows.Length - 1; i >= 0; i--) result.Add((IntPtr)windows[i]);
        return result;
    }

    public static string GetWindowTitle(IntPtr window)
    {
        byte[]? title = GetPropertyBytes((uint)window, "_NET_WM_NAME", GetAtom("UTF8_STRING"), 4096);
        if (title == null || title.Length == 0) title = GetPropertyBytes((uint)window, "WM_NAME", XcbNative.AnyPropertyType, 4096);
        return title == null ? "" : Encoding.UTF8.GetString(title).TrimEnd('\0');
    }

    /// <summary>The class part of WM_CLASS, such as "Firefox".</summary>
    public static string GetWindowClass(IntPtr window)
    {
        byte[]? value = GetPropertyBytes((uint)window, "WM_CLASS", XcbNative.AnyPropertyType, 1024);
        if (value == null) return "";
        string[] parts = Encoding.UTF8.GetString(value).Split('\0', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 1 ? parts[1] : parts.Length == 1 ? parts[0] : "";
    }

    public static int GetWindowProcessId(IntPtr window)
    {
        uint[] values = GetCardinalProperty((uint)window, "_NET_WM_PID", 1);
        return values.Length > 0 ? (int)values[0] : 0;
    }

    public static bool HasWindowState(IntPtr window, string state)
    {
        uint atom = GetAtom(state);
        return Array.IndexOf(GetCardinalProperty((uint)window, "_NET_WM_STATE", 64), atom) >= 0;
    }

    public static bool HasWindowType(IntPtr window, string type)
    {
        uint atom = GetAtom(type);
        return Array.IndexOf(GetCardinalProperty((uint)window, "_NET_WM_WINDOW_TYPE", 64), atom) >= 0;
    }

    public static bool IsWindowMinimized(IntPtr window) => HasWindowState(window, "_NET_WM_STATE_HIDDEN");

    public static bool IsWindowMaximized(IntPtr window) =>
        HasWindowState(window, "_NET_WM_STATE_MAXIMIZED_VERT") && HasWindowState(window, "_NET_WM_STATE_MAXIMIZED_HORZ");

    public static bool IsWindowVisible(IntPtr window)
    {
        if (window == IntPtr.Zero) return false;

        IntPtr c = Connection;
        byte* reply = XcbNative.GetWindowAttributesReply(c, XcbNative.GetWindowAttributes(c, (uint)window), null);
        if (reply == null) return false;

        try
        {
            if (reply[26] != XcbNative.MapStateViewable) return false;
        }
        finally
        {
            XcbNative.Free(reply);
        }

        // Compositing window managers keep minimized windows and windows on other workspaces mapped.
        return !IsWindowMinimized(window) && IsOnCurrentDesktop(window);
    }

    public static bool IsOnCurrentDesktop(IntPtr window)
    {
        uint[] windowDesktop = GetCardinalProperty((uint)window, "_NET_WM_DESKTOP", 1);
        if (windowDesktop.Length == 0 || windowDesktop[0] == uint.MaxValue) return true; // Sticky or unmanaged.

        uint[] currentDesktop = GetCardinalProperty((uint)RootWindow, "_NET_CURRENT_DESKTOP", 1);
        return currentDesktop.Length == 0 || currentDesktop[0] == windowDesktop[0];
    }

    public static bool WindowExists(IntPtr window)
    {
        if (window == IntPtr.Zero) return false;

        IntPtr c = Connection;
        byte* reply = XcbNative.GetGeometryReply(c, XcbNative.GetGeometry(c, (uint)window), null);
        if (reply == null) return false;
        XcbNative.Free(reply);
        return true;
    }

    /// <summary>The window's client area in root coordinates, excluding window manager decorations.</summary>
    public static Rectangle GetClientRectangle(IntPtr window)
    {
        if (window == IntPtr.Zero) return Rectangle.Empty;

        IntPtr c = Connection;
        byte* geometry = XcbNative.GetGeometryReply(c, XcbNative.GetGeometry(c, (uint)window), null);
        if (geometry == null) return Rectangle.Empty;

        int width, height;
        try
        {
            width = *(ushort*)(geometry + 16);
            height = *(ushort*)(geometry + 18);
        }
        finally
        {
            XcbNative.Free(geometry);
        }

        byte* translated = XcbNative.TranslateCoordinatesReply(c, XcbNative.TranslateCoordinates(c, (uint)window, rootWindow, 0, 0), null);
        if (translated == null) return Rectangle.Empty;

        try
        {
            return new Rectangle(*(short*)(translated + 12), *(short*)(translated + 14), width, height);
        }
        finally
        {
            XcbNative.Free(translated);
        }
    }

    /// <summary>
    /// The visible window bounds: the client area grown by the window manager frame (_NET_FRAME_EXTENTS) and shrunk by
    /// the invisible shadow margins of client-side decorated windows (_GTK_FRAME_EXTENTS).
    /// </summary>
    public static Rectangle GetWindowRectangle(IntPtr window)
    {
        Rectangle rect = GetClientRectangle(window);
        if (rect.IsEmpty) return rect;

        uint[] frame = GetCardinalProperty((uint)window, "_NET_FRAME_EXTENTS", 4);
        if (frame.Length == 4)
        {
            rect = Rectangle.FromLTRB(rect.Left - (int)frame[0], rect.Top - (int)frame[2], rect.Right + (int)frame[1], rect.Bottom + (int)frame[3]);
        }

        uint[] shadow = GetCardinalProperty((uint)window, "_GTK_FRAME_EXTENTS", 4);
        if (shadow.Length == 4)
        {
            rect = Rectangle.FromLTRB(rect.Left + (int)shadow[0], rect.Top + (int)shadow[2], rect.Right - (int)shadow[1], rect.Bottom - (int)shadow[3]);
        }
        else if (frame.Length != 4 && !IsWindowMaximized(window))
        {
            // Some toolkits (Avalonia, Electron without GTK) draw their own frame inside a transparent margin without
            // describing it, so the margin is measured from the window's own alpha channel instead.
            rect = TrimTransparentMargins(window, rect);
        }

        return rect;
    }

    private const int MaxTransparentMargin = 64;

    /// <summary>Shrinks the rectangle past edge lines that are mostly transparent, such as shadows and resize borders.</summary>
    private static Rectangle TrimTransparentMargins(IntPtr window, Rectangle rect)
    {
        if (GetWindowDepth(window) != 32 || rect.Width < 4 || rect.Height < 4) return rect;

        int horizontalLimit = Math.Min(MaxTransparentMargin, rect.Width / 2);
        int verticalLimit = Math.Min(MaxTransparentMargin, rect.Height / 2);

        int left = MeasureMargin(window, new Rectangle(0, 0, horizontalLimit, rect.Height), vertical: true, fromEnd: false);
        int right = MeasureMargin(window, new Rectangle(rect.Width - horizontalLimit, 0, horizontalLimit, rect.Height), vertical: true, fromEnd: true);
        int top = MeasureMargin(window, new Rectangle(0, 0, rect.Width, verticalLimit), vertical: false, fromEnd: false);
        int bottom = MeasureMargin(window, new Rectangle(0, rect.Height - verticalLimit, rect.Width, verticalLimit), vertical: false, fromEnd: true);

        Rectangle trimmed = Rectangle.FromLTRB(rect.Left + left, rect.Top + top, rect.Right - right, rect.Bottom - bottom);
        return trimmed.Width > 0 && trimmed.Height > 0 ? trimmed : rect;
    }

    /// <summary>
    /// Counts the lines from the outer edge of the strip inward until one is mostly opaque. Soft shadows stay below the
    /// threshold, while rounded corners only make a few pixels of the window's first line transparent.
    /// </summary>
    private static int MeasureMargin(IntPtr window, Rectangle strip, bool vertical, bool fromEnd)
    {
        IntPtr c = Connection;
        byte* reply = XcbNative.GetImageReply(c, XcbNative.GetImage(c, XcbNative.ImageFormatZPixmap, (uint)window,
            (short)strip.X, (short)strip.Y, (ushort)strip.Width, (ushort)strip.Height, uint.MaxValue), null);
        if (reply == null) return 0;

        try
        {
            if (reply[1] != 32 || XcbNative.GetImageDataLength(reply) < strip.Width * strip.Height * 4) return 0;

            uint* pixels = (uint*)XcbNative.GetImageData(reply);
            int lineCount = vertical ? strip.Width : strip.Height;
            int lineLength = vertical ? strip.Height : strip.Width;

            for (int i = 0; i < lineCount; i++)
            {
                int line = fromEnd ? lineCount - 1 - i : i;
                int opaque = 0;

                for (int j = 0; j < lineLength; j++)
                {
                    uint pixel = vertical ? pixels[j * strip.Width + line] : pixels[line * strip.Width + j];
                    if (pixel >> 24 >= 128) opaque++;
                }

                if (opaque * 2 > lineLength) return i;
            }

            // No opaque line at all, so this is not a transparent margin; leave the rectangle alone.
            return 0;
        }
        finally
        {
            XcbNative.Free(reply);
        }
    }

    private static int GetWindowDepth(IntPtr window)
    {
        IntPtr c = Connection;
        byte* reply = XcbNative.GetGeometryReply(c, XcbNative.GetGeometry(c, (uint)window), null);
        if (reply == null) return 0;

        try
        {
            return reply[1];
        }
        finally
        {
            XcbNative.Free(reply);
        }
    }

    /// <summary>A window whose title is the given text, or else the topmost one whose title contains it.</summary>
    public static IntPtr SearchWindow(string title)
    {
        List<IntPtr> windows = GetWindowsTopmostFirst();
        foreach (IntPtr window in windows)
        {
            if (GetWindowTitle(window) == title) return window;
        }

        foreach (IntPtr window in windows)
        {
            if (GetWindowTitle(window).Contains(title, StringComparison.InvariantCultureIgnoreCase)) return window;
        }

        return IntPtr.Zero;
    }

    /// <summary>The topmost visible window whose bounds contain the point.</summary>
    public static IntPtr GetWindowAtPoint(Point point)
    {
        foreach (IntPtr window in GetWindowsTopmostFirst())
        {
            if (IsWindowVisible(window) && GetWindowRectangle(window).Contains(point)) return window;
        }

        return IntPtr.Zero;
    }

    /// <summary>Asks the window manager to add or remove an EWMH state, such as _NET_WM_STATE_ABOVE.</summary>
    public static void SetWindowState(IntPtr window, string state, bool enable)
    {
        if (window == IntPtr.Zero) return;

        IntPtr c = Connection;
        byte* evt = stackalloc byte[32];
        new Span<byte>(evt, 32).Clear();
        evt[0] = XcbNative.ClientMessage;
        evt[1] = 32;
        *(uint*)(evt + 4) = (uint)window;
        *(uint*)(evt + 8) = GetAtom("_NET_WM_STATE");
        *(uint*)(evt + 12) = enable ? 1u : 0u; // _NET_WM_STATE_ADD or _NET_WM_STATE_REMOVE
        *(uint*)(evt + 16) = GetAtom(state);
        *(uint*)(evt + 24) = 2; // Source indication: pager.
        XcbNative.SendEvent(c, 0, rootWindow, XcbNative.EventMaskSubstructureNotify | XcbNative.EventMaskSubstructureRedirect, evt);
        XcbNative.Flush(c);
    }

    /// <summary>The window opacity that compositing window managers apply, from 0 to 255.</summary>
    public static byte GetWindowOpacity(IntPtr window)
    {
        uint[] opacity = GetCardinalProperty((uint)window, "_NET_WM_WINDOW_OPACITY", 1);
        return opacity.Length == 0 ? (byte)255 : (byte)(opacity[0] >> 24);
    }

    public static void SetWindowOpacity(IntPtr window, byte opacity)
    {
        if (window == IntPtr.Zero) return;

        IntPtr c = Connection;
        uint property = GetAtom("_NET_WM_WINDOW_OPACITY");
        XcbNative.Cookie cookie;

        if (opacity == 255)
        {
            cookie = XcbNative.DeleteProperty(c, (uint)window, property);
        }
        else
        {
            // The property scales opacity to the full 32 bit range.
            uint value = (uint)(opacity * 0x01010101u);
            cookie = XcbNative.ChangeProperty(c, XcbNative.PropModeReplace, (uint)window, property, XcbNative.AtomCardinal, 32, 1, &value);
        }

        XcbNative.GenericError* error = XcbNative.RequestCheck(c, cookie);
        if (error != null) XcbNative.Free(error);
    }

    /// <summary>The window's _NET_WM_ICON image closest to the preferred size, preferring larger ones.</summary>
    public static SKBitmap? GetWindowIcon(IntPtr window, int preferredSize = 32)
    {
        uint[] data = GetCardinalProperty((uint)window, "_NET_WM_ICON", 1 << 20);
        int bestOffset = -1, bestWidth = 0, bestHeight = 0;

        // The property holds width, height and width * height ARGB pixels for each icon size.
        for (int offset = 0; offset + 2 <= data.Length;)
        {
            int width = (int)data[offset], height = (int)data[offset + 1];
            if (width <= 0 || height <= 0 || offset + 2 + (long)width * height > data.Length) break;

            bool better = bestOffset < 0 ||
                (bestWidth < preferredSize ? width > bestWidth : width >= preferredSize && width < bestWidth);
            if (better)
            {
                bestOffset = offset;
                bestWidth = width;
                bestHeight = height;
            }

            offset += 2 + width * height;
        }

        if (bestOffset < 0) return null;

        // _NET_WM_ICON pixels are unpremultiplied ARGB, which is BGRA in memory on little endian systems.
        SKBitmap icon = new(new SKImageInfo(bestWidth, bestHeight, SKColorType.Bgra8888, SKAlphaType.Unpremul));
        fixed (uint* source = &data[bestOffset + 2])
        {
            Buffer.MemoryCopy(source, (void*)icon.GetPixels(), icon.ByteCount, (long)bestWidth * bestHeight * 4);
        }

        return icon;
    }

    /// <summary>Asks the window manager to activate the window, as a pager would.</summary>
    public static void ActivateWindow(IntPtr window)
    {
        if (window == IntPtr.Zero) return;

        IntPtr c = Connection;
        byte* evt = stackalloc byte[32];
        new Span<byte>(evt, 32).Clear();
        evt[0] = XcbNative.ClientMessage;
        evt[1] = 32;
        *(uint*)(evt + 4) = (uint)window;
        *(uint*)(evt + 8) = GetAtom("_NET_ACTIVE_WINDOW");
        *(uint*)(evt + 12) = 2; // Source indication: pager, so the request is not treated as focus stealing.
        XcbNative.SendEvent(c, 0, rootWindow, XcbNative.EventMaskSubstructureNotify | XcbNative.EventMaskSubstructureRedirect, evt);
        XcbNative.Flush(c);
    }

    /// <summary>
    /// Limits where a window is drawn and receives input to the given rectangles, in window pixels, so the rest of it is
    /// see-through and clicks reach the windows below.
    /// </summary>
    public static bool SetWindowShape(IntPtr window, IReadOnlyList<Rectangle> rectangles) =>
        SetWindowShape(window, rectangles, XcbNative.ShapeBounding, XcbNative.ShapeInput);

    /// <summary>Limits where a window is drawn to the given rectangles and makes it fully click-through.</summary>
    public static bool SetWindowShapeClickThrough(IntPtr window, IReadOnlyList<Rectangle> rectangles) =>
        SetWindowShape(window, rectangles, XcbNative.ShapeBounding) && SetWindowShape(window, [], XcbNative.ShapeInput);

    /// <summary>Limits where a window receives input, in window pixels. No rectangles makes it fully click-through.</summary>
    public static bool SetWindowInputShape(IntPtr window, IReadOnlyList<Rectangle> rectangles) =>
        SetWindowShape(window, rectangles, XcbNative.ShapeInput);

    private static bool SetWindowShape(IntPtr window, IReadOnlyList<Rectangle> rectangles, params byte[] kinds)
    {
        if (window == IntPtr.Zero || !TryGetConnection(out IntPtr c)) return false;

        XcbNative.XcbRectangle* shape = stackalloc XcbNative.XcbRectangle[rectangles.Count];
        for (int i = 0; i < rectangles.Count; i++)
        {
            Rectangle r = rectangles[i];
            shape[i] = new XcbNative.XcbRectangle { X = (short)r.X, Y = (short)r.Y, Width = (ushort)Math.Max(r.Width, 0), Height = (ushort)Math.Max(r.Height, 0) };
        }

        bool success = true;
        foreach (byte kind in kinds)
        {
            XcbNative.GenericError* error = XcbNative.RequestCheck(c, XcbNative.ShapeRectangles(c, XcbNative.ShapeSet, kind, 0,
                (uint)window, 0, 0, (uint)rectangles.Count, shape));
            if (error != null)
            {
                success = false;
                XcbNative.Free(error);
            }
        }

        return success;
    }

    #endregion Windows

    #region Pointer

    public static Point GetCursorPosition()
    {
        if (!TryGetConnection(out IntPtr c)) return Point.Empty;

        byte* reply = XcbNative.QueryPointerReply(c, XcbNative.QueryPointer(c, rootWindow), null);
        if (reply == null) return Point.Empty;

        try
        {
            return new Point(*(short*)(reply + 16), *(short*)(reply + 18));
        }
        finally
        {
            XcbNative.Free(reply);
        }
    }

    /// <summary>The pointer position and the state of the primary, middle and secondary buttons.</summary>
    public static bool TryGetPointerState(out Point position, out bool primary, out bool middle, out bool secondary)
    {
        position = Point.Empty;
        primary = middle = secondary = false;
        if (!TryGetConnection(out IntPtr c)) return false;

        byte* reply = XcbNative.QueryPointerReply(c, XcbNative.QueryPointer(c, rootWindow), null);
        if (reply == null) return false;

        try
        {
            position = new Point(*(short*)(reply + 16), *(short*)(reply + 18));
            // Button masks follow the pointer mapping, so left-handed button swapping is already applied.
            ushort mask = *(ushort*)(reply + 24);
            primary = (mask & 0x100) != 0;
            middle = (mask & 0x200) != 0;
            secondary = (mask & 0x400) != 0;
            return true;
        }
        finally
        {
            XcbNative.Free(reply);
        }
    }

    public static void SetCursorPosition(int x, int y)
    {
        if (!TryGetConnection(out IntPtr c)) return;

        XcbNative.WarpPointer(c, 0, rootWindow, 0, 0, 0, 0, (short)x, (short)y);
        XcbNative.Flush(c);
    }

    #endregion Pointer

    #region Capture

    public static Color GetPixelColor(int x, int y)
    {
        using SKBitmap? bitmap = CaptureRectangle(new Rectangle(x, y, 1, 1), false);
        if (bitmap == null) return Color.Empty;
        SKColor color = bitmap.GetPixel(0, 0);
        return Color.FromArgb(color.Red, color.Green, color.Blue);
    }

    /// <summary>Like <see cref="CaptureRectangle"/>, but parts of the rectangle outside the root window are black.</summary>
    public static SKBitmap? CaptureRectanglePadded(Rectangle rect)
    {
        if (rect.Width <= 0 || rect.Height <= 0 || !TryGetConnection(out IntPtr c)) return null;

        byte* geometry = XcbNative.GetGeometryReply(c, XcbNative.GetGeometry(c, rootWindow), null);
        if (geometry == null) return null;
        Rectangle rootBounds;
        try
        {
            rootBounds = new Rectangle(0, 0, *(ushort*)(geometry + 16), *(ushort*)(geometry + 18));
        }
        finally
        {
            XcbNative.Free(geometry);
        }

        Rectangle visible = Rectangle.Intersect(rect, rootBounds);
        if (visible == rect) return CaptureRectangle(rect, false);

        SKBitmap result = new(new SKImageInfo(rect.Width, rect.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul));
        result.Erase(SKColors.Black);
        if (visible.Width > 0 && visible.Height > 0)
        {
            using SKBitmap? part = CaptureRectangle(visible, false);
            if (part != null)
            {
                using SKCanvas canvas = new(result);
                canvas.DrawBitmap(part, visible.X - rect.X, visible.Y - rect.Y);
            }
        }

        return result;
    }

    /// <summary>Copies a rectangle of the root window, which on a compositing window manager is what the user sees.</summary>
    public static SKBitmap? CaptureRectangle(Rectangle rect, bool captureCursor)
    {
        if (rect.Width <= 0 || rect.Height <= 0) return null;

        IntPtr c = Connection;
        XcbNative.GenericError* error;
        byte* reply = XcbNative.GetImageReply(c, XcbNative.GetImage(c, XcbNative.ImageFormatZPixmap, rootWindow,
            (short)rect.X, (short)rect.Y, (ushort)rect.Width, (ushort)rect.Height, uint.MaxValue), &error);

        if (reply == null)
        {
            int errorCode = error != null ? error->ErrorCode : 0;
            if (error != null) XcbNative.Free(error);
            throw new InvalidOperationException($"X11 screen capture failed (error {errorCode}) for {rect}.");
        }

        SKBitmap bitmap;
        try
        {
            int depth = reply[1];
            byte* data = XcbNative.GetImageData(reply);
            int length = XcbNative.GetImageDataLength(reply);
            int stride = rect.Width * 4;

            if ((depth != 24 && depth != 32) || length < stride * rect.Height)
            {
                throw new NotSupportedException($"Unsupported X11 screen format: depth {depth}, {length} bytes for {rect.Width}x{rect.Height}.");
            }

            // 24 and 32 bit ZPixmaps are BGRX in memory on little endian servers, with an undefined padding byte.
            bitmap = new SKBitmap(new SKImageInfo(rect.Width, rect.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul));
            uint* source = (uint*)data;
            uint* destination = (uint*)bitmap.GetPixels();
            int pixelCount = rect.Width * rect.Height;
            for (int i = 0; i < pixelCount; i++) destination[i] = source[i] | 0xFF000000;
        }
        finally
        {
            XcbNative.Free(reply);
        }

        if (captureCursor)
        {
            try
            {
                DrawCursor(bitmap, rect.Location);
            }
            catch (Exception e)
            {
                DebugHelper.WriteException(e, "Cursor capture failed.");
            }
        }

        return bitmap;
    }

    /// <summary>Draws the current cursor at its screen position, relative to the bitmap's offset on the screen.</summary>
    public static void DrawCursor(SKBitmap bitmap, Point offset)
    {
        X11CursorImage? cursor = GetCursorImage();
        if (cursor == null) return;

        using (cursor.Image)
        using (SKCanvas canvas = new(bitmap))
        {
            canvas.DrawBitmap(cursor.Image, cursor.DrawPosition.X - offset.X, cursor.DrawPosition.Y - offset.Y);
        }
    }

    /// <summary>The current cursor image from XFixes, converted from premultiplied ARGB to unpremultiplied BGRA.</summary>
    public static X11CursorImage? GetCursorImage()
    {
        if (!TryGetConnection(out IntPtr c) || !InitializeXfixes(c)) return null;

        byte* reply = XcbNative.XfixesGetCursorImageReply(c, XcbNative.XfixesGetCursorImage(c), null);
        if (reply == null) return null;

        try
        {
            int x = *(short*)(reply + 8);
            int y = *(short*)(reply + 10);
            int width = *(ushort*)(reply + 12);
            int height = *(ushort*)(reply + 14);
            int xHot = *(ushort*)(reply + 16);
            int yHot = *(ushort*)(reply + 18);
            if (width == 0 || height == 0) return null;

            SKImageInfo premultipliedInfo = new(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
            using SKPixmap premultiplied = new(premultipliedInfo, (IntPtr)XcbNative.XfixesGetCursorImageData(reply), width * 4);

            SKBitmap image = new(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Unpremul));
            if (!premultiplied.ReadPixels(image.Info, image.GetPixels(), image.RowBytes))
            {
                image.Dispose();
                return null;
            }

            return new X11CursorImage(image, new Point(x, y), new Point(xHot, yHot));
        }
        finally
        {
            XcbNative.Free(reply);
        }
    }

    private static bool InitializeXfixes(IntPtr c)
    {
        if (xfixesAvailable == null)
        {
            try
            {
                byte* reply = XcbNative.XfixesQueryVersionReply(c, XcbNative.XfixesQueryVersion(c, 4, 0), null);
                xfixesAvailable = reply != null;
                if (reply != null) XcbNative.Free(reply);
            }
            catch (DllNotFoundException)
            {
                xfixesAvailable = false;
            }
        }

        return xfixesAvailable.Value;
    }

    #endregion Capture

    #region Properties

    public static uint GetAtom(string name)
    {
        return atoms.GetOrAdd(name, static atomName =>
        {
            IntPtr c = Connection;
            byte[] bytes = Encoding.ASCII.GetBytes(atomName);
            fixed (byte* namePointer = bytes)
            {
                byte* reply = XcbNative.InternAtomReply(c, XcbNative.InternAtom(c, 0, (ushort)bytes.Length, namePointer), null);
                if (reply == null) return 0;
                try
                {
                    return *(uint*)(reply + 8);
                }
                finally
                {
                    XcbNative.Free(reply);
                }
            }
        });
    }

    private static uint[] GetCardinalProperty(uint window, string property, uint maxItems)
    {
        byte[]? bytes = GetPropertyBytes(window, property, XcbNative.AnyPropertyType, maxItems, 32);
        if (bytes == null) return [];

        uint[] values = new uint[bytes.Length / 4];
        Buffer.BlockCopy(bytes, 0, values, 0, values.Length * 4);
        return values;
    }

    private static byte[]? GetPropertyBytes(uint window, string property, uint type, uint maxLength, int requiredFormat = 0)
    {
        if (window == 0) return null;

        IntPtr c = Connection;
        // long_length is in 32 bit units.
        uint longLength = requiredFormat == 32 ? maxLength : (maxLength + 3) / 4;
        byte* reply = XcbNative.GetPropertyReply(c, XcbNative.GetProperty(c, 0, window, GetAtom(property), type, 0, longLength), null);
        if (reply == null) return null;

        try
        {
            int format = reply[1];
            if (format == 0 || (requiredFormat != 0 && format != requiredFormat)) return null;

            int length = XcbNative.GetPropertyValueLength(reply); // Already in bytes.
            byte[] result = new byte[length];
            new ReadOnlySpan<byte>(XcbNative.GetPropertyValue(reply), length).CopyTo(result);
            return result;
        }
        finally
        {
            XcbNative.Free(reply);
        }
    }

    #endregion Properties
}

/// <summary>A cursor image with its hotspot position on the screen and the hotspot offset within the image.</summary>
public sealed record X11CursorImage(SKBitmap Image, Point Position, Point Hotspot)
{
    public Point DrawPosition => new(Position.X - Hotspot.X, Position.Y - Hotspot.Y);
}
