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
using System.Diagnostics;

namespace ShareX.HelpersLib;

public static class LinuxFileManager
{
    /// <summary>
    /// Opens the file's folder with the file selected, through the freedesktop.org FileManager1 D-Bus interface that
    /// Nemo, Nautilus, Dolphin, Thunar and others implement.
    /// </summary>
    public static bool ShowItem(string filePath)
    {
        try
        {
            ProcessStartInfo startInfo = new("dbus-send")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (string argument in new[] { "--session", "--print-reply", "--dest=org.freedesktop.FileManager1", "--type=method_call",
                "/org/freedesktop/FileManager1", "org.freedesktop.FileManager1.ShowItems",
                "array:string:" + new Uri(filePath).AbsoluteUri, "string:" })
            {
                startInfo.ArgumentList.Add(argument);
            }

            using Process? process = Process.Start(startInfo);
            if (process == null || !process.WaitForExit(5000)) return false;
            return process.ExitCode == 0;
        }
        catch (Exception e)
        {
            DebugHelper.WriteException(e);
            return false;
        }
    }
}
