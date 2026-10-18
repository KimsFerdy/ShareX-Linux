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

using System;
using System.Diagnostics;
using System.Drawing;

namespace ShareX.HelpersLib
{
    public class WindowInfo
    {
        public IntPtr Handle { get; }

        public bool IsHandleCreated => Handle != IntPtr.Zero;

        public string Text => OperatingSystem.IsWindows() ? NativeMethods.GetWindowText(Handle) : X11Desktop.GetWindowTitle(Handle);

        public string ClassName => OperatingSystem.IsWindows() ? NativeMethods.GetClassName(Handle) : X11Desktop.GetWindowClass(Handle);

        public Process Process => OperatingSystem.IsWindows() ? NativeMethods.GetProcessByWindowHandle(Handle) : GetX11Process();

        public string ProcessName
        {
            get
            {
                using (Process process = Process)
                {
                    return process?.ProcessName;
                }
            }
        }

        public string ProcessFilePath
        {
            get
            {
                using (Process process = Process)
                {
                    return process?.MainModule?.FileName;
                }
            }
        }

        public string ProcessFileName => FileHelpers.GetFileNameSafe(ProcessFilePath);

        public int ProcessId
        {
            get
            {
                if (!OperatingSystem.IsWindows())
                {
                    return X11Desktop.GetWindowProcessId(Handle);
                }

                using (Process process = Process)
                {
                    return process.Id;
                }
            }
        }

        public IntPtr Parent => OperatingSystem.IsWindows() ? NativeMethods.GetParent(Handle) : IntPtr.Zero;

        public Rectangle Rectangle => CaptureHelpers.GetWindowRectangle(Handle);

        public Rectangle ClientRectangle => CaptureHelpers.GetClientRectangle(Handle);

        public WindowStyles Style
        {
            get
            {
                return (WindowStyles)(ulong)NativeMethods.GetWindowLong(Handle, NativeConstants.GWL_STYLE);
            }
            set
            {
                NativeMethods.SetWindowLong(Handle, NativeConstants.GWL_STYLE, (IntPtr)value);
            }
        }

        public WindowStyles ExStyle
        {
            get
            {
                return (WindowStyles)(ulong)NativeMethods.GetWindowLong(Handle, NativeConstants.GWL_EXSTYLE);
            }
            set
            {
                NativeMethods.SetWindowLong(Handle, NativeConstants.GWL_EXSTYLE, (IntPtr)value);
            }
        }

        public bool Layered
        {
            get
            {
                return ExStyle.HasFlag(WindowStyles.WS_EX_LAYERED);
            }
            set
            {
                if (value)
                {
                    ExStyle |= WindowStyles.WS_EX_LAYERED;
                }
                else
                {
                    ExStyle &= ~WindowStyles.WS_EX_LAYERED;
                }
            }
        }

        public bool TopMost
        {
            get
            {
                if (!OperatingSystem.IsWindows())
                {
                    return X11Desktop.HasWindowState(Handle, "_NET_WM_STATE_ABOVE");
                }

                return ExStyle.HasFlag(WindowStyles.WS_EX_TOPMOST);
            }
            set
            {
                if (!OperatingSystem.IsWindows())
                {
                    X11Desktop.SetWindowState(Handle, "_NET_WM_STATE_ABOVE", value);
                    return;
                }

                SetWindowPos(value ? (IntPtr)NativeConstants.HWND_TOPMOST : (IntPtr)NativeConstants.HWND_NOTOPMOST,
                    SetWindowPosFlags.SWP_NOMOVE | SetWindowPosFlags.SWP_NOSIZE);
            }
        }

        public byte Opacity
        {
            get
            {
                if (!OperatingSystem.IsWindows())
                {
                    return X11Desktop.GetWindowOpacity(Handle);
                }

                if (Layered)
                {
                    NativeMethods.GetLayeredWindowAttributes(Handle, out _, out byte alpha, out _);
                    return alpha;
                }

                return 255;
            }
            set
            {
                if (!OperatingSystem.IsWindows())
                {
                    X11Desktop.SetWindowOpacity(Handle, value);
                    return;
                }

                if (value < 255)
                {
                    Layered = true;
                    NativeMethods.SetLayeredWindowAttributes(Handle, 0, value, NativeConstants.LWA_ALPHA);
                }
                else
                {
                    Layered = false;
                }
            }
        }

        public Icon Icon => OperatingSystem.IsWindows() ? NativeMethods.GetApplicationIcon(Handle) : null;

        public bool IsMaximized => OperatingSystem.IsWindows() ? NativeMethods.IsZoomed(Handle) : X11Desktop.IsWindowMaximized(Handle);

        public bool IsMinimized => OperatingSystem.IsWindows() ? NativeMethods.IsIconic(Handle) : X11Desktop.IsWindowMinimized(Handle);

        public bool IsVisible => OperatingSystem.IsWindows() ? NativeMethods.IsWindowVisible(Handle) : X11Desktop.IsWindowVisible(Handle);

        public bool IsCloaked => OperatingSystem.IsWindows() && NativeMethods.IsWindowCloaked(Handle);

        public bool IsActive => OperatingSystem.IsWindows() ? NativeMethods.IsActive(Handle) : X11Desktop.GetActiveWindow() == Handle;

        /// <summary>The desktop background window, which shows desktop icons.</summary>
        public bool IsDesktop
        {
            get
            {
                if (!OperatingSystem.IsWindows())
                {
                    return X11Desktop.HasWindowType(Handle, "_NET_WM_WINDOW_TYPE_DESKTOP");
                }

                string className = ClassName;
                return className.Equals("Progman", StringComparison.OrdinalIgnoreCase) || className.Equals("WorkerW", StringComparison.OrdinalIgnoreCase);
            }
        }

        public WindowInfo(IntPtr handle)
        {
            Handle = handle;
        }

        public void Activate()
        {
            if (IsHandleCreated)
            {
                if (OperatingSystem.IsWindows())
                {
                    NativeMethods.SetForegroundWindow(Handle);
                }
                else
                {
                    X11Desktop.ActivateWindow(Handle);
                }
            }
        }

        public void BringToFront()
        {
            if (IsHandleCreated)
            {
                if (OperatingSystem.IsWindows())
                {
                    SetWindowPos(SetWindowPosFlags.SWP_NOMOVE | SetWindowPosFlags.SWP_NOSIZE);
                }
                else
                {
                    X11Desktop.ActivateWindow(Handle);
                }
            }
        }

        public void Restore()
        {
            if (IsHandleCreated)
            {
                if (OperatingSystem.IsWindows())
                {
                    NativeMethods.ShowWindow(Handle, (int)WindowShowStyle.Restore);
                }
                else
                {
                    // Activating a minimized window restores it on EWMH window managers.
                    X11Desktop.ActivateWindow(Handle);
                }
            }
        }

        public void SetWindowPos(SetWindowPosFlags flags)
        {
            SetWindowPos((IntPtr)NativeConstants.HWND_TOP, 0, 0, 0, 0, flags);
        }

        public void SetWindowPos(Rectangle rect, SetWindowPosFlags flags)
        {
            SetWindowPos((IntPtr)NativeConstants.HWND_TOP, rect.X, rect.Y, rect.Width, rect.Height, flags);
        }

        public void SetWindowPos(IntPtr insertAfter, SetWindowPosFlags flags)
        {
            SetWindowPos(insertAfter, 0, 0, 0, 0, flags);
        }

        public void SetWindowPos(IntPtr insertAfter, int x, int y, int width, int height, SetWindowPosFlags flags)
        {
            NativeMethods.SetWindowPos(Handle, insertAfter, x, y, width, height, flags);
        }

        private Process GetX11Process()
        {
            int processId = X11Desktop.GetWindowProcessId(Handle);

            if (processId > 0)
            {
                try
                {
                    return Process.GetProcessById(processId);
                }
                catch (ArgumentException)
                {
                }
            }

            return null;
        }

        public override string ToString()
        {
            return Text;
        }
    }
}