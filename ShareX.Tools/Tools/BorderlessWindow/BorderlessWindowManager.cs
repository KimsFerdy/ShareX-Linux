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

using ShareX.HelpersLib;
using System.Collections.Concurrent;
using System.Drawing;

namespace ShareX.Tools;

public static class BorderlessWindowManager
{
    private static readonly ConcurrentDictionary<IntPtr, BorderlessWindowInfo> BorderlessWindows = new();
    private static readonly ConcurrentDictionary<IntPtr, X11BorderlessWindowInfo> X11BorderlessWindows = new();

    /// <summary>What an X11 window looked like before it was made borderless.</summary>
    private sealed record X11BorderlessWindowInfo(bool Fullscreen, Rectangle ClientBounds, uint[]? MotifHints);

    public static bool ToggleBorderlessWindow(string windowTitle, bool useWorkingArea = false)
    {
        if (string.IsNullOrWhiteSpace(windowTitle))
        {
            return false;
        }

        IntPtr handle = CaptureHelpers.SearchWindow(windowTitle);
        if (handle == IntPtr.Zero)
        {
            return false;
        }

        ToggleBorderlessWindow(handle, useWorkingArea);
        return true;
    }

    public static void ToggleBorderlessWindow(IntPtr handle, bool useWorkingArea = false)
    {
        if (!OperatingSystem.IsWindows())
        {
            ToggleX11BorderlessWindow(handle, useWorkingArea);
            return;
        }

        if (BorderlessWindows.TryGetValue(handle, out BorderlessWindowInfo? info))
        {
            RestoreWindow(handle, info);
        }
        else
        {
            MakeWindowBorderless(handle, useWorkingArea);
        }
    }

    private static void ToggleX11BorderlessWindow(IntPtr handle, bool useWorkingArea)
    {
        WindowInfo window = new(handle);
        if (window.IsMinimized)
        {
            window.Restore();
        }

        if (X11BorderlessWindows.TryRemove(handle, out X11BorderlessWindowInfo? info))
        {
            if (info.Fullscreen)
            {
                X11Desktop.SetWindowState(handle, "_NET_WM_STATE_FULLSCREEN", false);
            }
            else
            {
                X11Desktop.SetMotifHints(handle, info.MotifHints);
                X11Desktop.MoveResizeWindow(handle, info.ClientBounds);
            }

            return;
        }

        info = new X11BorderlessWindowInfo(!useWorkingArea, CaptureHelpers.GetClientRectangle(handle), X11Desktop.GetMotifHints(handle));

        if (info.Fullscreen)
        {
            // The window manager removes the frame and covers the monitor exactly.
            X11Desktop.SetWindowState(handle, "_NET_WM_STATE_FULLSCREEN", true);
        }
        else
        {
            uint[] hints = info.MotifHints is { } current ? (uint[])current.Clone() : new uint[5];
            hints[0] |= 2; // MWM_HINTS_DECORATIONS
            hints[2] = 0; // No decorations.
            X11Desktop.SetMotifHints(handle, hints);
            X11Desktop.MoveResizeWindow(handle, CaptureHelpers.GetScreenBounds(handle, true));
        }

        X11BorderlessWindows.TryAdd(handle, info);
    }

    private static void RestoreWindow(IntPtr handle, BorderlessWindowInfo info)
    {
        WindowInfo window = new(handle);
        if (window.IsMinimized)
        {
            window.Restore();
        }

        window.Style = info.Style;
        window.ExStyle = info.ExStyle;
        window.SetWindowPos(info.Rectangle, SetWindowPosFlags.SWP_FRAMECHANGED |
            SetWindowPosFlags.SWP_NOOWNERZORDER | SetWindowPosFlags.SWP_NOZORDER);
        BorderlessWindows.TryRemove(handle, out _);
    }

    private static void MakeWindowBorderless(IntPtr handle, bool useWorkingArea)
    {
        WindowInfo window = new(handle);
        if (window.IsMinimized)
        {
            window.Restore();
        }

        BorderlessWindowInfo info = new(window);
        window.Style &= ~(WindowStyles.WS_CAPTION | WindowStyles.WS_MAXIMIZEBOX |
            WindowStyles.WS_SYSMENU | WindowStyles.WS_THICKFRAME);
        window.ExStyle &= ~(WindowStyles.WS_EX_CLIENTEDGE | WindowStyles.WS_EX_DLGMODALFRAME |
            WindowStyles.WS_EX_STATICEDGE);

        Rectangle bounds = CaptureHelpers.GetScreenBounds(handle, useWorkingArea);
        SetWindowPosFlags flags = SetWindowPosFlags.SWP_FRAMECHANGED |
            SetWindowPosFlags.SWP_NOOWNERZORDER | SetWindowPosFlags.SWP_NOZORDER;

        if (bounds.IsEmpty)
        {
            flags |= SetWindowPosFlags.SWP_NOMOVE | SetWindowPosFlags.SWP_NOSIZE;
        }

        window.SetWindowPos(bounds, flags);
        BorderlessWindows.TryAdd(handle, info);
    }
}
