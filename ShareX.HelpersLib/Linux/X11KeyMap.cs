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

namespace ShareX.HelpersLib;

/// <summary>Maps the Windows virtual-key codes that hotkeys are saved with to X11 keysyms.</summary>
internal static class X11KeyMap
{
    public static uint ToKeysym(InputKey key)
    {
        int code = (int)key;

        // Letters map to their lowercase keysyms, which are the unshifted level on every layout.
        if (key >= InputKey.A && key <= InputKey.Z) return (uint)(code + 0x20);
        if (key >= InputKey.D0 && key <= InputKey.D9) return (uint)code;
        if (key >= InputKey.F1 && key <= InputKey.F24) return 0xffbe + (uint)(key - InputKey.F1);
        if (key >= InputKey.NumPad0 && key <= InputKey.NumPad9) return 0xffb0 + (uint)(key - InputKey.NumPad0);

        return key switch
        {
            InputKey.Back => 0xff08,
            InputKey.Tab => 0xff09,
            InputKey.Clear => 0xff0b,
            InputKey.Return => 0xff0d,
            InputKey.Pause => 0xff13,
            InputKey.Scroll => 0xff14,
            InputKey.Escape => 0xff1b,
            InputKey.Home => 0xff50,
            InputKey.Left => 0xff51,
            InputKey.Up => 0xff52,
            InputKey.Right => 0xff53,
            InputKey.Down => 0xff54,
            InputKey.PageUp => 0xff55,
            InputKey.PageDown => 0xff56,
            InputKey.End => 0xff57,
            InputKey.PrintScreen => 0xff61,
            InputKey.Insert => 0xff63,
            InputKey.Apps => 0xff67,
            InputKey.NumLock => 0xff7f,
            InputKey.Multiply => 0xffaa,
            InputKey.Add => 0xffab,
            InputKey.Separator => 0xffac,
            InputKey.Subtract => 0xffad,
            InputKey.Decimal => 0xffae,
            InputKey.Divide => 0xffaf,
            InputKey.CapsLock => 0xffe5,
            InputKey.Delete => 0xffff,
            InputKey.Space => 0x20,
            InputKey.OemQuotes => 0x27,
            InputKey.Oemcomma => 0x2c,
            InputKey.OemMinus => 0x2d,
            InputKey.OemPeriod => 0x2e,
            InputKey.OemQuestion => 0x2f,
            InputKey.OemSemicolon => 0x3b,
            InputKey.Oemplus => 0x3d,
            InputKey.OemBackslash => 0x3c,
            InputKey.OemOpenBrackets => 0x5b,
            InputKey.OemPipe => 0x5c,
            InputKey.OemCloseBrackets => 0x5d,
            InputKey.Oemtilde => 0x60,
            InputKey.VolumeDown => 0x1008ff11,
            InputKey.VolumeMute => 0x1008ff12,
            InputKey.VolumeUp => 0x1008ff13,
            InputKey.MediaPlayPause => 0x1008ff14,
            InputKey.MediaStop => 0x1008ff15,
            InputKey.MediaPreviousTrack => 0x1008ff16,
            InputKey.MediaNextTrack => 0x1008ff17,
            _ => 0
        };
    }
}
