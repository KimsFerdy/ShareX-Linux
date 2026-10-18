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
using System.IO;

namespace ShareX.HelpersLib;

/// <summary>Plays WAV audio through the first available PipeWire, PulseAudio or ALSA command line player.</summary>
public static class LinuxSoundPlayer
{
    private static readonly string[] players = ["pw-play", "paplay", "aplay"];
    private static readonly Lazy<string?> playerPath = new(FindPlayer);

    public static void PlaySync(Stream stream)
    {
        string tempPath = Path.Combine(Path.GetTempPath(), $"sharex-sound-{Guid.NewGuid():N}.wav");

        try
        {
            using (FileStream fileStream = File.Create(tempPath))
            {
                stream.CopyTo(fileStream);
            }

            PlaySync(tempPath);
        }
        finally
        {
            try { File.Delete(tempPath); } catch { }
        }
    }

    public static void PlaySync(string filePath)
    {
        string? player = playerPath.Value;
        if (player == null) return;

        try
        {
            ProcessStartInfo startInfo = new(player)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            if (Path.GetFileName(player) == "aplay") startInfo.ArgumentList.Add("-q");
            startInfo.ArgumentList.Add(filePath);

            using Process? process = Process.Start(startInfo);
            process?.WaitForExit(10000);
        }
        catch (Exception e)
        {
            DebugHelper.WriteException(e);
        }
    }

    private static string? FindPlayer()
    {
        foreach (string player in players)
        {
            string? path = LinuxEnvironment.FindExecutable(player);
            if (path != null) return path;
        }

        return null;
    }
}
