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

/// <summary>The subset of libxcb used by the X11 backends.</summary>
internal static unsafe partial class XcbNative
{
    private const string LibXcb = "libxcb.so.1";
    private const string LibC = "libc";

    public const byte KeyPress = 2;
    public const byte MappingNotify = 34;
    public const byte GrabModeAsync = 1;

    public const ushort ShiftMask = 1 << 0;
    public const ushort LockMask = 1 << 1;
    public const ushort ControlMask = 1 << 2;
    public const ushort Mod1Mask = 1 << 3; // Alt
    public const ushort Mod2Mask = 1 << 4; // Num Lock
    public const ushort Mod4Mask = 1 << 6; // Super

    [StructLayout(LayoutKind.Sequential)]
    public struct Cookie
    {
        public uint Sequence;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ScreenIterator
    {
        public IntPtr Data;
        public int Rem;
        public int Index;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GenericError
    {
        public byte ResponseType;
        public byte ErrorCode;
        public ushort Sequence;
        public uint ResourceId;
        public ushort MinorCode;
        public byte MajorCode;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KeyPressEvent
    {
        public byte ResponseType;
        public byte Detail;
        public ushort Sequence;
        public uint Time;
        public uint Root;
        public uint Event;
        public uint Child;
        public short RootX;
        public short RootY;
        public short EventX;
        public short EventY;
        public ushort State;
        public byte SameScreen;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PollFd
    {
        public int Fd;
        public short Events;
        public short Revents;
    }

    public const short PollIn = 0x001;

    [LibraryImport(LibXcb, EntryPoint = "xcb_connect", StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr Connect(string? displayName, out int screen);

    [LibraryImport(LibXcb, EntryPoint = "xcb_connection_has_error")]
    public static partial int ConnectionHasError(IntPtr connection);

    [LibraryImport(LibXcb, EntryPoint = "xcb_disconnect")]
    public static partial void Disconnect(IntPtr connection);

    [LibraryImport(LibXcb, EntryPoint = "xcb_get_setup")]
    public static partial byte* GetSetup(IntPtr connection);

    [LibraryImport(LibXcb, EntryPoint = "xcb_setup_roots_iterator")]
    public static partial ScreenIterator SetupRootsIterator(byte* setup);

    [LibraryImport(LibXcb, EntryPoint = "xcb_screen_next")]
    public static partial void ScreenNext(ref ScreenIterator iterator);

    [LibraryImport(LibXcb, EntryPoint = "xcb_get_file_descriptor")]
    public static partial int GetFileDescriptor(IntPtr connection);

    [LibraryImport(LibXcb, EntryPoint = "xcb_flush")]
    public static partial int Flush(IntPtr connection);

    [LibraryImport(LibXcb, EntryPoint = "xcb_poll_for_event")]
    public static partial byte* PollForEvent(IntPtr connection);

    [LibraryImport(LibXcb, EntryPoint = "xcb_request_check")]
    public static partial GenericError* RequestCheck(IntPtr connection, Cookie cookie);

    [LibraryImport(LibXcb, EntryPoint = "xcb_grab_key_checked")]
    public static partial Cookie GrabKeyChecked(IntPtr connection, byte ownerEvents, uint grabWindow, ushort modifiers,
        byte key, byte pointerMode, byte keyboardMode);

    [LibraryImport(LibXcb, EntryPoint = "xcb_ungrab_key_checked")]
    public static partial Cookie UngrabKeyChecked(IntPtr connection, byte key, uint grabWindow, ushort modifiers);

    [LibraryImport(LibXcb, EntryPoint = "xcb_get_keyboard_mapping")]
    public static partial Cookie GetKeyboardMapping(IntPtr connection, byte firstKeycode, byte count);

    [LibraryImport(LibXcb, EntryPoint = "xcb_get_keyboard_mapping_reply")]
    public static partial byte* GetKeyboardMappingReply(IntPtr connection, Cookie cookie, GenericError** error);

    [LibraryImport(LibXcb, EntryPoint = "xcb_get_keyboard_mapping_keysyms")]
    public static partial uint* GetKeyboardMappingKeysyms(byte* reply);

    [LibraryImport(LibXcb, EntryPoint = "xcb_get_keyboard_mapping_keysyms_length")]
    public static partial int GetKeyboardMappingKeysymsLength(byte* reply);

    [LibraryImport(LibC, EntryPoint = "free")]
    public static partial void Free(void* pointer);

    [LibraryImport(LibC, EntryPoint = "poll", SetLastError = true)]
    public static partial int Poll(PollFd* fds, nuint count, int timeout);

    /// <summary>Offsets into xcb_setup_t for the keycode range.</summary>
    public const int SetupMinKeycodeOffset = 34;
    public const int SetupMaxKeycodeOffset = 35;
}
