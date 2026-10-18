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

using SkiaSharp;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;

namespace ShareX.HelpersLib;

public static class FileThumbnailHelper
{
    /// <summary>
    /// A thumbnail for a file the image decoder cannot read, such as a video. Windows asks the shell; Linux takes the
    /// first frame of a video with FFmpeg.
    /// </summary>
    public static SKBitmap? GetFileThumbnail(string filePath, Size thumbnailSize)
    {
        if (OperatingSystem.IsWindows())
        {
            return NativeMethods.GetFileThumbnail(filePath, thumbnailSize);
        }

        return FileHelpers.IsVideoFile(filePath) ? GetVideoThumbnail(filePath, thumbnailSize) : null;
    }

    private static SKBitmap? GetVideoThumbnail(string filePath, Size thumbnailSize)
    {
        string? ffmpegPath = LinuxEnvironment.FindExecutable("ffmpeg");
        if (ffmpegPath == null) return null;

        ProcessStartInfo startInfo = new(ffmpegPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        string scale = string.Format(CultureInfo.InvariantCulture,
            "scale={0}:{1}:force_original_aspect_ratio=decrease", Math.Max(thumbnailSize.Width, 1), Math.Max(thumbnailSize.Height, 1));
        foreach (string argument in new[] { "-v", "error", "-i", filePath, "-frames:v", "1", "-vf", scale, "-f", "image2pipe", "-c:v", "png", "-" })
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process? process = Process.Start(startInfo);
        if (process == null) return null;

        process.ErrorDataReceived += (_, _) => { };
        process.BeginErrorReadLine();

        using MemoryStream png = new();
        process.StandardOutput.BaseStream.CopyTo(png);

        if (!process.WaitForExit(10000))
        {
            process.Kill();
            return null;
        }

        if (process.ExitCode != 0 || png.Length == 0) return null;

        png.Position = 0;
        return SKBitmap.Decode(png);
    }
}
