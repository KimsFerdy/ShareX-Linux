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

using System;
using System.Runtime.InteropServices;

namespace ShareX.HelpersLib;

/// <summary>
/// Simulated keyboard and mouse input through the XTest extension, on a private Xlib connection. The requests only use
/// keycodes and buttons that exist, so they do not reach Xlib's process wide error handler.
/// </summary>
public static partial class X11Input
{
    private const string LibX11 = "libX11.so.6";
    private const string LibXtst = "libXtst.so.6";

    private static readonly object sync = new();
    private static IntPtr display;

    private static bool TryGetDisplay(out IntPtr result)
    {
        lock (sync)
        {
            if (display == IntPtr.Zero && LinuxEnvironment.IsX11)
            {
                try
                {
                    display = XOpenDisplay(null);
                }
                catch (DllNotFoundException e)
                {
                    DebugHelper.WriteException(e, "Simulated input requires libXtst.");
                }
            }

            result = display;
            return result != IntPtr.Zero;
        }
    }

    public static bool SendKey(VirtualKeyCode keyCode, bool pressed)
    {
        uint keysym = X11KeyMap.ToKeysym(ToInputKey(keyCode));
        return keysym != 0 && SendKeysym(keysym, pressed);
    }

    /// <summary>Types text whose characters have keys on the current layout, such as Latin-1 text.</summary>
    public static bool SendText(string text)
    {
        foreach (char character in text)
        {
            // Latin-1 characters share their code with their keysym.
            uint keysym = character switch
            {
                '\n' => 0xff0d,
                '\t' => 0xff09,
                _ when character <= 0xff => character,
                _ => 0x01000000u + character
            };

            if (!SendKeysym(keysym, true) || !SendKeysym(keysym, false)) return false;
        }

        return true;
    }

    public static bool SendMouseButton(InputMouseButton button, bool pressed)
    {
        uint x11Button = button switch
        {
            InputMouseButton.Left => 1,
            InputMouseButton.Middle => 2,
            InputMouseButton.Right => 3,
            InputMouseButton.XButton1 => 8,
            InputMouseButton.XButton2 => 9,
            _ => 0
        };

        return x11Button != 0 && SendButton(x11Button, pressed);
    }

    public static bool SendMouseMove(int x, int y)
    {
        lock (sync)
        {
            if (!TryGetDisplay(out IntPtr d)) return false;
            bool result = XTestFakeMotionEvent(d, -1, x, y, 0) != 0;
            XFlush(d);
            return result;
        }
    }

    /// <summary>Scrolls by Windows wheel units, 120 per notch, where positive scrolls up.</summary>
    public static bool SendMouseWheel(int delta)
    {
        uint button = delta > 0 ? 4u : 5u;
        int notches = Math.Max(1, (int)Math.Round(Math.Abs(delta) / 120d));

        for (int i = 0; i < notches; i++)
        {
            if (!SendButton(button, true) || !SendButton(button, false)) return false;
        }

        return true;
    }

    private static bool SendButton(uint button, bool pressed)
    {
        lock (sync)
        {
            if (!TryGetDisplay(out IntPtr d)) return false;
            bool result = XTestFakeButtonEvent(d, button, pressed ? 1 : 0, 0) != 0;
            XFlush(d);
            return result;
        }
    }

    private static bool SendKeysym(uint keysym, bool pressed)
    {
        lock (sync)
        {
            if (!TryGetDisplay(out IntPtr d)) return false;
            byte keycode = XKeysymToKeycode(d, keysym);
            if (keycode == 0) return false;
            bool result = XTestFakeKeyEvent(d, keycode, pressed ? 1 : 0, 0) != 0;
            XFlush(d);
            return result;
        }
    }

    // VirtualKeyCode and InputKey both use Windows virtual-key codes, except for the modifier keys that only
    // VirtualKeyCode distinguishes by side.
    private static InputKey ToInputKey(VirtualKeyCode keyCode) => keyCode switch
    {
        VirtualKeyCode.LSHIFT or VirtualKeyCode.RSHIFT => InputKey.ShiftKey,
        VirtualKeyCode.LCONTROL or VirtualKeyCode.RCONTROL => InputKey.ControlKey,
        VirtualKeyCode.LMENU or VirtualKeyCode.RMENU => InputKey.Menu,
        _ => (InputKey)(int)keyCode
    };

    [LibraryImport(LibX11, StringMarshalling = StringMarshalling.Utf8)]
    private static partial IntPtr XOpenDisplay(string? displayName);

    [LibraryImport(LibX11)]
    private static partial int XFlush(IntPtr display);

    [LibraryImport(LibX11)]
    private static partial byte XKeysymToKeycode(IntPtr display, nuint keysym);

    [LibraryImport(LibXtst)]
    private static partial int XTestFakeKeyEvent(IntPtr display, uint keycode, int isPress, nuint delay);

    [LibraryImport(LibXtst)]
    private static partial int XTestFakeButtonEvent(IntPtr display, uint button, int isPress, nuint delay);

    [LibraryImport(LibXtst)]
    private static partial int XTestFakeMotionEvent(IntPtr display, int screen, int x, int y, nuint delay);
}
