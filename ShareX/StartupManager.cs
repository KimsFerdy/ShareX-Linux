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

using Microsoft.Win32;
using ShareX.HelpersLib;
using ShareX.Localization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

#if MicrosoftStore
using Windows.ApplicationModel;
#endif

namespace ShareX
{
    public static class StartupManager
    {
#if MicrosoftStore
        private const int StartupTargetIndex = 0;
        private static readonly StartupTask packageTask = StartupTask.GetForCurrentPackageAsync().GetAwaiter().GetResult()[StartupTargetIndex];
#endif

        public static string StartupTargetPath
        {
            get
            {
#if STEAM
                return FileHelpers.GetAbsolutePath("../ShareX_Launcher.exe");
#else
                return Environment.ProcessPath;
#endif
            }
        }

        public static string StartupText => OperatingSystem.IsWindows() ?
            Strings.ApplicationSettingsForm_cbStartWithWindows_Text : Strings.ApplicationSettingsForm_cbStartWithLinux_Text;

        public static string DisabledByUserText => OperatingSystem.IsWindows() ?
            Strings.ApplicationSettingsForm_cbStartWithWindows_DisabledByUser_Text : Strings.ApplicationSettingsForm_cbStartWithLinux_DisabledByUser_Text;

        public static StartupState State
        {
            get
            {
#if MicrosoftStore
                return (StartupState)packageTask.State;
#else
                if (!OperatingSystem.IsWindows())
                {
                    return GetLinuxAutostartState();
                }

                if (ShortcutHelpers.CheckShortcut(Environment.SpecialFolder.Startup, "ShareX", StartupTargetPath))
                {
                    if (Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder",
                        "ShareX.lnk", null) is byte[] status && status.Length > 0 && status[0] == 3)
                    {
                        return StartupState.DisabledByUser;
                    }
                    else
                    {
                        return StartupState.Enabled;
                    }
                }
                else
                {
                    return StartupState.Disabled;
                }
#endif
            }
            set
            {
#if MicrosoftStore
                if (value == StartupState.Enabled)
                {
                    packageTask.RequestEnableAsync().GetAwaiter().GetResult();
                }
                else if (value == StartupState.Disabled)
                {
                    packageTask.Disable();
                }
                else
                {
                    throw new NotSupportedException();
                }
#else
                if (!OperatingSystem.IsWindows())
                {
                    SetLinuxAutostartState(value);
                }
                else if (value == StartupState.Enabled || value == StartupState.Disabled)
                {
                    ShortcutHelpers.SetShortcut(value == StartupState.Enabled, Environment.SpecialFolder.Startup, "ShareX", StartupTargetPath, "-silent");
                }
                else
                {
                    throw new NotSupportedException();
                }
#endif
            }
        }

        private static string LinuxAutostartPath => Path.Combine(LinuxDesktopEntry.AutostartFolder, "sharex.desktop");

        private static StartupState GetLinuxAutostartState()
        {
            string targetPath = LinuxDesktopEntry.GetTargetPath(LinuxAutostartPath);

            if (string.IsNullOrEmpty(targetPath) || !targetPath.Equals(StartupTargetPath, StringComparison.Ordinal))
            {
                return StartupState.Disabled;
            }

            return LinuxDesktopEntry.IsDisabled(LinuxAutostartPath) ? StartupState.DisabledByUser : StartupState.Enabled;
        }

        /// <summary>
        /// A build that uses a shared .NET runtime, such as a development build, finds the runtime through DOTNET_ROOT,
        /// which the login session does not set. Self-contained builds, such as the .deb package, need nothing.
        /// </summary>
        private static Dictionary<string, string> GetLinuxAutostartEnvironment()
        {
            Dictionary<string, string> environment = new();
            string runtimeDirectory = Path.GetFullPath(RuntimeEnvironment.GetRuntimeDirectory());
            string appDirectory = Path.GetFullPath(AppContext.BaseDirectory);

            if (!runtimeDirectory.StartsWith(appDirectory, StringComparison.Ordinal))
            {
                // The runtime is in <dotnet root>/shared/Microsoft.NETCore.App/<version>/.
                DirectoryInfo dotnetRoot = new DirectoryInfo(runtimeDirectory.TrimEnd(Path.DirectorySeparatorChar)).Parent?.Parent?.Parent;
                if (dotnetRoot != null)
                {
                    environment["DOTNET_ROOT"] = dotnetRoot.FullName;
                }
            }

            return environment;
        }

        private static void SetLinuxAutostartState(StartupState value)
        {
            try
            {
                if (value == StartupState.Enabled)
                {
                    LinuxDesktopEntry.Write(LinuxAutostartPath, "ShareX", StartupTargetPath, "-silent",
                        "Screen capture, file sharing and productivity tool", "sharex", GetLinuxAutostartEnvironment(), autostart: true);
                }
                else if (value == StartupState.Disabled)
                {
                    if (File.Exists(LinuxAutostartPath))
                    {
                        File.Delete(LinuxAutostartPath);
                    }
                }
                else
                {
                    throw new NotSupportedException();
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                DebugHelper.WriteException(e);
                e.ShowError();
            }
        }
    }
}