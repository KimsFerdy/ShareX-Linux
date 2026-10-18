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

    private const string LibXcbXfixes = "libxcb-xfixes.so.0";

    public const byte ImageFormatZPixmap = 2;
    public const byte MapStateViewable = 2;
    public const byte ClientMessage = 33;
    public const uint AnyPropertyType = 0;
    public const uint EventMaskSubstructureNotify = 1 << 19;
    public const uint EventMaskSubstructureRedirect = 1 << 20;

    [LibraryImport(LibXcb, EntryPoint = "xcb_intern_atom")]
    public static partial Cookie InternAtom(IntPtr connection, byte onlyIfExists, ushort nameLength, byte* name);

    [LibraryImport(LibXcb, EntryPoint = "xcb_intern_atom_reply")]
    public static partial byte* InternAtomReply(IntPtr connection, Cookie cookie, GenericError** error);

    [LibraryImport(LibXcb, EntryPoint = "xcb_get_property")]
    public static partial Cookie GetProperty(IntPtr connection, byte delete, uint window, uint property, uint type,
        uint longOffset, uint longLength);

    [LibraryImport(LibXcb, EntryPoint = "xcb_get_property_reply")]
    public static partial byte* GetPropertyReply(IntPtr connection, Cookie cookie, GenericError** error);

    [LibraryImport(LibXcb, EntryPoint = "xcb_get_property_value")]
    public static partial byte* GetPropertyValue(byte* reply);

    [LibraryImport(LibXcb, EntryPoint = "xcb_get_property_value_length")]
    public static partial int GetPropertyValueLength(byte* reply);

    [LibraryImport(LibXcb, EntryPoint = "xcb_get_geometry")]
    public static partial Cookie GetGeometry(IntPtr connection, uint drawable);

    [LibraryImport(LibXcb, EntryPoint = "xcb_get_geometry_reply")]
    public static partial byte* GetGeometryReply(IntPtr connection, Cookie cookie, GenericError** error);

    [LibraryImport(LibXcb, EntryPoint = "xcb_translate_coordinates")]
    public static partial Cookie TranslateCoordinates(IntPtr connection, uint sourceWindow, uint destinationWindow, short x, short y);

    [LibraryImport(LibXcb, EntryPoint = "xcb_translate_coordinates_reply")]
    public static partial byte* TranslateCoordinatesReply(IntPtr connection, Cookie cookie, GenericError** error);

    [LibraryImport(LibXcb, EntryPoint = "xcb_get_window_attributes")]
    public static partial Cookie GetWindowAttributes(IntPtr connection, uint window);

    [LibraryImport(LibXcb, EntryPoint = "xcb_get_window_attributes_reply")]
    public static partial byte* GetWindowAttributesReply(IntPtr connection, Cookie cookie, GenericError** error);

    [LibraryImport(LibXcb, EntryPoint = "xcb_query_pointer")]
    public static partial Cookie QueryPointer(IntPtr connection, uint window);

    [LibraryImport(LibXcb, EntryPoint = "xcb_query_pointer_reply")]
    public static partial byte* QueryPointerReply(IntPtr connection, Cookie cookie, GenericError** error);

    [LibraryImport(LibXcb, EntryPoint = "xcb_warp_pointer")]
    public static partial Cookie WarpPointer(IntPtr connection, uint sourceWindow, uint destinationWindow, short sourceX, short sourceY,
        ushort sourceWidth, ushort sourceHeight, short destinationX, short destinationY);

    [LibraryImport(LibXcb, EntryPoint = "xcb_send_event")]
    public static partial Cookie SendEvent(IntPtr connection, byte propagate, uint destination, uint eventMask, byte* evt);

    [LibraryImport(LibXcb, EntryPoint = "xcb_get_image")]
    public static partial Cookie GetImage(IntPtr connection, byte format, uint drawable, short x, short y, ushort width, ushort height,
        uint planeMask);

    [LibraryImport(LibXcb, EntryPoint = "xcb_get_image_reply")]
    public static partial byte* GetImageReply(IntPtr connection, Cookie cookie, GenericError** error);

    [LibraryImport(LibXcb, EntryPoint = "xcb_get_image_data")]
    public static partial byte* GetImageData(byte* reply);

    [LibraryImport(LibXcb, EntryPoint = "xcb_get_image_data_length")]
    public static partial int GetImageDataLength(byte* reply);

    [LibraryImport(LibXcbXfixes, EntryPoint = "xcb_xfixes_query_version")]
    public static partial Cookie XfixesQueryVersion(IntPtr connection, uint majorVersion, uint minorVersion);

    [LibraryImport(LibXcbXfixes, EntryPoint = "xcb_xfixes_query_version_reply")]
    public static partial byte* XfixesQueryVersionReply(IntPtr connection, Cookie cookie, GenericError** error);

    [LibraryImport(LibXcbXfixes, EntryPoint = "xcb_xfixes_get_cursor_image")]
    public static partial Cookie XfixesGetCursorImage(IntPtr connection);

    [LibraryImport(LibXcbXfixes, EntryPoint = "xcb_xfixes_get_cursor_image_reply")]
    public static partial byte* XfixesGetCursorImageReply(IntPtr connection, Cookie cookie, GenericError** error);

    [LibraryImport(LibXcbXfixes, EntryPoint = "xcb_xfixes_get_cursor_image_cursor_image")]
    public static partial uint* XfixesGetCursorImageData(byte* reply);

    [LibraryImport(LibC, EntryPoint = "free")]
    public static partial void Free(void* pointer);

    [LibraryImport(LibC, EntryPoint = "poll", SetLastError = true)]
    public static partial int Poll(PollFd* fds, nuint count, int timeout);

    /// <summary>Offsets into xcb_setup_t for the keycode range.</summary>
    public const int SetupMinKeycodeOffset = 34;
    public const int SetupMaxKeycodeOffset = 35;
}
