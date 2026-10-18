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
using SkiaSharp;

namespace ShareX.AvaloniaUI.Integration;

/// <summary>
/// Pointer and screen pixel access for windows in this library on platforms where they do not call Win32 directly.
/// The host application provides the implementations at startup.
/// </summary>
public static class ScreenAccess
{
    private static Func<PixelPoint?>? _getCursorPosition;
    private static Func<PixelRect, SKBitmap?>? _captureRectangle;

    public static bool IsAvailable => _getCursorPosition != null && _captureRectangle != null;

    public static void Register(Func<PixelPoint?> getCursorPosition, Func<PixelRect, SKBitmap?> captureRectangle)
    {
        _getCursorPosition = getCursorPosition;
        _captureRectangle = captureRectangle;
    }

    /// <summary>The pointer position in physical screen pixels.</summary>
    public static PixelPoint? GetCursorPosition() => _getCursorPosition?.Invoke();

    /// <summary>A copy of the screen in physical pixels, as opaque BGRA. Parts outside every screen are black.</summary>
    public static SKBitmap? CaptureRectangle(PixelRect rectangle) => _captureRectangle?.Invoke(rectangle);
}
