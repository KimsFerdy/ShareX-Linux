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

namespace ShareX.ScreenRecordingLib;

/// <summary>A Video4Linux camera. Id is its device node, such as /dev/video0.</summary>
public sealed record CameraCaptureDevice(string Id, string Name);

public static class CameraCaptureDevices
{
    private const string SysfsPath = "/sys/class/video4linux";

    public static IReadOnlyList<CameraCaptureDevice> GetCameras()
    {
        if (!Directory.Exists(SysfsPath)) return [];

        List<CameraCaptureDevice> cameras = new();
        foreach (string device in Directory.GetDirectories(SysfsPath, "video*").OrderBy(GetDeviceNumber))
        {
            // UVC cameras expose a metadata node next to each capture node; only index 0 captures frames.
            if (ReadAttribute(device, "index") is string index && index != "0") continue;

            string node = "/dev/" + Path.GetFileName(device);
            cameras.Add(new(node, ReadAttribute(device, "name") ?? node));
        }
        return cameras;
    }

    private static int GetDeviceNumber(string path) =>
        int.TryParse(Path.GetFileName(path).AsSpan("video".Length), out int number) ? number : int.MaxValue;

    private static string? ReadAttribute(string device, string name)
    {
        try
        {
            return File.ReadAllText(Path.Combine(device, name)).Trim();
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}
