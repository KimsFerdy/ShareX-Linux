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

using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace ShareX.HelpersLib;

/// <summary>
/// X11 global hotkeys. Uses a private XCB connection so that grab failures are reported per request instead of
/// through Xlib's process-wide error handler, which Avalonia owns.
/// </summary>
public sealed unsafe class X11HotkeyHost : IHotkeyHost
{
    // Lock keys change the modifier state, so each hotkey is also grabbed with every combination of them.
    private static readonly ushort[] lockModifierCombinations =
    [
        0, XcbNative.LockMask, XcbNative.Mod2Mask, XcbNative.LockMask | XcbNative.Mod2Mask
    ];

    private const ushort RelevantModifiers = XcbNative.ShiftMask | XcbNative.ControlMask | XcbNative.Mod1Mask | XcbNative.Mod4Mask;

    private readonly int threadId = Environment.CurrentManagedThreadId;
    private readonly Stopwatch repeatLimitTimer = Stopwatch.StartNew();
    private readonly object sync = new();
    private readonly Dictionary<HotkeyInfo, (byte KeyCode, ushort Modifiers)> registeredHotkeys = new();
    private IntPtr connection;
    private uint rootWindow;
    private Dictionary<uint, byte> keysymToKeycode = new();
    private Thread? eventThread;
    private volatile bool closing;
    private ushort nextId = 1;

    public event HotkeyEventHandler? HotkeyPress;
    public event EventHandler? Closed;
    public event EventHandler<NativeWindowMessageEventArgs>? NativeMessageReceived { add { } remove { } }

    public IntPtr Handle => (IntPtr)rootWindow;
    public bool IsDisposed { get; private set; }
    public int HotkeyRepeatLimit { get; set; } = 1000;

    public X11HotkeyHost()
    {
        if (!OperatingSystem.IsLinux())
            throw new PlatformNotSupportedException("X11 hotkeys require Linux.");
    }

    public void Initialize()
    {
        VerifyAccess();
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (connection != IntPtr.Zero) return;

        IntPtr newConnection = XcbNative.Connect(null, out int screenNumber);
        if (XcbNative.ConnectionHasError(newConnection) != 0)
        {
            XcbNative.Disconnect(newConnection);
            DebugHelper.WriteLine("X11 hotkeys are unavailable: unable to connect to the X server.");
            return;
        }

        XcbNative.ScreenIterator iterator = XcbNative.SetupRootsIterator(XcbNative.GetSetup(newConnection));
        for (int i = 0; i < screenNumber && iterator.Rem > 0; i++) XcbNative.ScreenNext(ref iterator);

        connection = newConnection;
        rootWindow = *(uint*)iterator.Data; // xcb_screen_t.root is the first field.
        keysymToKeycode = LoadKeyboardMapping();

        eventThread = new Thread(EventLoop) { IsBackground = true, Name = "ShareX X11 hotkeys" };
        eventThread.Start();
    }

    public void RegisterHotkey(HotkeyInfo hotkeyInfo)
    {
        VerifyAccess();
        ObjectDisposedException.ThrowIf(IsDisposed || closing, this);
        if (hotkeyInfo == null || hotkeyInfo.Status == HotkeyStatus.Registered) return;
        if (!hotkeyInfo.IsValidHotkey)
        {
            hotkeyInfo.Status = HotkeyStatus.NotConfigured;
            return;
        }

        Initialize();

        if (connection == IntPtr.Zero || !TryGetKeyCode(hotkeyInfo.KeyCode, out byte keyCode))
        {
            DebugHelper.WriteLine("Unable to register hotkey: " + hotkeyInfo);
            hotkeyInfo.Status = HotkeyStatus.Failed;
            return;
        }

        ushort modifiers = ToX11Modifiers(hotkeyInfo);
        if (!Grab(keyCode, modifiers))
        {
            DebugHelper.WriteLine("Unable to register hotkey, it is probably used by another application: " + hotkeyInfo);
            hotkeyInfo.Status = HotkeyStatus.Failed;
            return;
        }

        if (hotkeyInfo.ID == 0) hotkeyInfo.ID = nextId++;

        lock (sync) registeredHotkeys[hotkeyInfo] = (keyCode, modifiers);
        hotkeyInfo.Status = HotkeyStatus.Registered;
    }

    public bool UnregisterHotkey(HotkeyInfo hotkeyInfo)
    {
        VerifyAccess();
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (hotkeyInfo == null) return false;

        (byte KeyCode, ushort Modifiers) grab;
        lock (sync)
        {
            if (!registeredHotkeys.Remove(hotkeyInfo, out grab))
            {
                hotkeyInfo.Status = HotkeyStatus.Failed;
                return false;
            }
        }

        Ungrab(grab.KeyCode, grab.Modifiers);
        hotkeyInfo.ID = 0;
        hotkeyInfo.Status = HotkeyStatus.NotConfigured;
        return true;
    }

    private bool Grab(byte keyCode, ushort modifiers)
    {
        XcbNative.Cookie[] cookies = lockModifierCombinations
            .Select(lockModifiers => XcbNative.GrabKeyChecked(connection, 1, rootWindow, (ushort)(modifiers | lockModifiers), keyCode,
                XcbNative.GrabModeAsync, XcbNative.GrabModeAsync))
            .ToArray();

        bool success = true;
        foreach (XcbNative.Cookie cookie in cookies)
        {
            XcbNative.GenericError* error = XcbNative.RequestCheck(connection, cookie);
            if (error != null)
            {
                success = false;
                XcbNative.Free(error);
            }
        }

        if (!success) Ungrab(keyCode, modifiers);
        return success;
    }

    private void Ungrab(byte keyCode, ushort modifiers)
    {
        if (connection == IntPtr.Zero) return;

        foreach (ushort lockModifiers in lockModifierCombinations)
        {
            XcbNative.GenericError* error = XcbNative.RequestCheck(connection,
                XcbNative.UngrabKeyChecked(connection, keyCode, rootWindow, (ushort)(modifiers | lockModifiers)));
            if (error != null) XcbNative.Free(error);
        }
    }

    private void EventLoop()
    {
        XcbNative.PollFd pollFd = new() { Fd = XcbNative.GetFileDescriptor(connection), Events = XcbNative.PollIn };

        while (!closing)
        {
            byte* genericEvent = XcbNative.PollForEvent(connection);
            if (genericEvent == null)
            {
                if (XcbNative.ConnectionHasError(connection) != 0) return;
                XcbNative.Poll(&pollFd, 1, 250);
                continue;
            }

            try
            {
                int type = genericEvent[0] & 0x7f;
                if (type == XcbNative.KeyPress)
                {
                    XcbNative.KeyPressEvent* keyPress = (XcbNative.KeyPressEvent*)genericEvent;
                    OnKeyPress(keyPress->Detail, (ushort)(keyPress->State & RelevantModifiers));
                }
                else if (type == XcbNative.MappingNotify)
                {
                    Dispatcher.UIThread.Post(RefreshKeyboardMapping);
                }
            }
            finally
            {
                XcbNative.Free(genericEvent);
            }
        }
    }

    private void OnKeyPress(byte keyCode, ushort modifiers)
    {
        HotkeyInfo? hotkeyInfo;
        lock (sync)
        {
            hotkeyInfo = registeredHotkeys.FirstOrDefault(x => x.Value.KeyCode == keyCode && x.Value.Modifiers == modifiers).Key;
        }

        if (hotkeyInfo == null) return;

        Dispatcher.UIThread.Post(() =>
        {
            if (IsDisposed || closing || !CheckRepeatLimitTime()) return;
            HotkeyPress?.Invoke(hotkeyInfo.ID, hotkeyInfo.KeyCode, hotkeyInfo.ModifiersEnum);
        });
    }

    /// <summary>Keycodes move when the keyboard layout changes, so existing grabs are moved to the new keycodes.</summary>
    private void RefreshKeyboardMapping()
    {
        if (IsDisposed || closing || connection == IntPtr.Zero) return;

        keysymToKeycode = LoadKeyboardMapping();

        KeyValuePair<HotkeyInfo, (byte KeyCode, ushort Modifiers)>[] hotkeys;
        lock (sync) hotkeys = registeredHotkeys.ToArray();

        foreach (KeyValuePair<HotkeyInfo, (byte KeyCode, ushort Modifiers)> hotkey in hotkeys)
        {
            if (TryGetKeyCode(hotkey.Key.KeyCode, out byte keyCode) && keyCode != hotkey.Value.KeyCode)
            {
                Ungrab(hotkey.Value.KeyCode, hotkey.Value.Modifiers);
                bool success = Grab(keyCode, hotkey.Value.Modifiers);
                lock (sync)
                {
                    if (success) registeredHotkeys[hotkey.Key] = (keyCode, hotkey.Value.Modifiers);
                    else registeredHotkeys.Remove(hotkey.Key);
                }
                if (!success) hotkey.Key.Status = HotkeyStatus.Failed;
            }
        }
    }

    private Dictionary<uint, byte> LoadKeyboardMapping()
    {
        Dictionary<uint, byte> mapping = new();
        byte* setup = XcbNative.GetSetup(connection);
        byte minKeycode = setup[XcbNative.SetupMinKeycodeOffset];
        byte maxKeycode = setup[XcbNative.SetupMaxKeycodeOffset];
        int count = maxKeycode - minKeycode + 1;

        XcbNative.Cookie cookie = XcbNative.GetKeyboardMapping(connection, minKeycode, (byte)count);
        byte* reply = XcbNative.GetKeyboardMappingReply(connection, cookie, null);
        if (reply == null) return mapping;

        try
        {
            int keysymsPerKeycode = reply[1];
            uint* keysyms = XcbNative.GetKeyboardMappingKeysyms(reply);
            int length = XcbNative.GetKeyboardMappingKeysymsLength(reply);

            // Prefer the unshifted keysym of each key, so that for example '1' maps to the 1 key and not Shift+1.
            for (int level = 0; level < keysymsPerKeycode; level++)
            {
                for (int i = 0; i < count; i++)
                {
                    int index = i * keysymsPerKeycode + level;
                    if (index >= length) break;
                    uint keysym = keysyms[index];
                    if (keysym != 0) mapping.TryAdd(keysym, (byte)(minKeycode + i));
                }
            }
        }
        finally
        {
            XcbNative.Free(reply);
        }

        return mapping;
    }

    private bool TryGetKeyCode(InputKey key, out byte keyCode)
    {
        keyCode = 0;
        uint keysym = X11KeyMap.ToKeysym(key);
        return keysym != 0 && keysymToKeycode.TryGetValue(keysym, out keyCode);
    }

    private static ushort ToX11Modifiers(HotkeyInfo hotkeyInfo)
    {
        ushort modifiers = 0;
        if (hotkeyInfo.Shift) modifiers |= XcbNative.ShiftMask;
        if (hotkeyInfo.Control) modifiers |= XcbNative.ControlMask;
        if (hotkeyInfo.Alt) modifiers |= XcbNative.Mod1Mask;
        if (hotkeyInfo.Win) modifiers |= XcbNative.Mod4Mask;
        return modifiers;
    }

    private bool CheckRepeatLimitTime()
    {
        if (HotkeyRepeatLimit > 0)
        {
            if (repeatLimitTimer.ElapsedMilliseconds < HotkeyRepeatLimit) return false;
            repeatLimitTimer.Restart();
        }
        return true;
    }

    public void Close()
    {
        VerifyAccess();
        if (IsDisposed || closing) return;
        closing = true;
        try
        {
            Closed?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            KeyValuePair<HotkeyInfo, (byte KeyCode, ushort Modifiers)>[] hotkeys;
            lock (sync)
            {
                hotkeys = registeredHotkeys.ToArray();
                registeredHotkeys.Clear();
            }

            foreach (KeyValuePair<HotkeyInfo, (byte KeyCode, ushort Modifiers)> hotkey in hotkeys)
            {
                Ungrab(hotkey.Value.KeyCode, hotkey.Value.Modifiers);
                hotkey.Key.ID = 0;
                hotkey.Key.Status = HotkeyStatus.NotConfigured;
            }

            eventThread?.Join(1000);
            eventThread = null;

            if (connection != IntPtr.Zero)
            {
                XcbNative.Disconnect(connection);
                connection = IntPtr.Zero;
            }

            IsDisposed = true;
        }
    }

    public void Dispose() => Close();

    private void VerifyAccess()
    {
        if (Environment.CurrentManagedThreadId != threadId)
            throw new InvalidOperationException("The hotkey host must be accessed on the thread that created it.");
    }
}
