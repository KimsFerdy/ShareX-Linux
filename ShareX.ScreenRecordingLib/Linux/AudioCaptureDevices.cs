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

using System.Diagnostics;
using System.Text.Json;

namespace ShareX.ScreenRecordingLib;

/// <summary>A PulseAudio or PipeWire source. Id is the source name that FFmpeg's pulse input accepts.</summary>
public sealed record AudioCaptureDevice(string Id, string Name);

public static class AudioCaptureDevices
{
    private const string MonitorSuffix = ".monitor";

    /// <summary>Lists input sources, excluding the monitors of output devices.</summary>
    public static IReadOnlyList<AudioCaptureDevice> GetMicrophones() =>
        GetSources().Where(source => !source.Id.EndsWith(MonitorSuffix, StringComparison.Ordinal)).ToArray();

    /// <summary>Lists monitor sources, which record what an output device plays.</summary>
    public static IReadOnlyList<AudioCaptureDevice> GetSystemAudioDevices() =>
        GetSources().Where(source => source.Id.EndsWith(MonitorSuffix, StringComparison.Ordinal)).ToArray();

    public static AudioCaptureDevice? GetDefaultSystemAudioDevice()
    {
        string? sink = RunPactl("get-default-sink")?.Trim();
        if (string.IsNullOrEmpty(sink)) return null;
        string id = sink + MonitorSuffix;
        return GetSystemAudioDevices().FirstOrDefault(device => device.Id == id) ?? new(id, id);
    }

    public static AudioCaptureDevice? GetDefaultMicrophone()
    {
        string? source = RunPactl("get-default-source")?.Trim();
        if (string.IsNullOrEmpty(source)) return null;
        return GetMicrophones().FirstOrDefault(device => device.Id == source) ?? new(source, source);
    }

    private static IReadOnlyList<AudioCaptureDevice> GetSources()
    {
        string? json = RunPactl("-f", "json", "list", "sources");
        if (string.IsNullOrEmpty(json)) return [];

        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            List<AudioCaptureDevice> devices = new();
            foreach (JsonElement source in document.RootElement.EnumerateArray())
            {
                string? name = source.TryGetProperty("name", out JsonElement nameElement) ? nameElement.GetString() : null;
                if (string.IsNullOrEmpty(name)) continue;
                string? description = source.TryGetProperty("description", out JsonElement descriptionElement) ? descriptionElement.GetString() : null;
                devices.Add(new(name, string.IsNullOrEmpty(description) ? name : description));
            }
            return devices;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string? RunPactl(params string[] arguments)
    {
        try
        {
            ProcessStartInfo startInfo = new("pactl")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (string argument in arguments) startInfo.ArgumentList.Add(argument);

            using Process process = Process.Start(startInfo)!;
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);
            return process.ExitCode == 0 ? output : null;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            // pactl is not installed.
            return null;
        }
    }
}
