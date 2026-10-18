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
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ShareX.HelpersLib;

/// <summary>Reads and writes freedesktop.org desktop entry files, which Linux uses for launchers and autostart.</summary>
public static class LinuxDesktopEntry
{
    public static string AutostartFolder => Path.Combine(LinuxEnvironment.ConfigHome, "autostart");

    public static string ApplicationsFolder => Path.Combine(LinuxEnvironment.DataHome, "applications");

    public static void Write(string path, string name, string targetPath, string arguments = "", string? comment = null, string? iconPath = null,
        IReadOnlyDictionary<string, string>? environment = null, bool autostart = false)
    {
        StringBuilder exec = new();
        if (environment is { Count: > 0 })
        {
            exec.Append("env");
            foreach (KeyValuePair<string, string> variable in environment)
            {
                exec.Append(' ').Append(variable.Key).Append('=').Append(QuoteExecArgument(variable.Value));
            }
            exec.Append(' ');
        }
        exec.Append(QuoteExecArgument(targetPath));
        if (!string.IsNullOrEmpty(arguments)) exec.Append(' ').Append(arguments);

        StringBuilder sb = new();
        sb.AppendLine("[Desktop Entry]");
        sb.AppendLine("Type=Application");
        sb.AppendLine("Name=" + name);
        if (!string.IsNullOrEmpty(comment)) sb.AppendLine("Comment=" + comment);
        sb.AppendLine("Exec=" + exec);
        sb.AppendLine("Path=" + Path.GetDirectoryName(targetPath));
        if (!string.IsNullOrEmpty(iconPath)) sb.AppendLine("Icon=" + iconPath);
        sb.AppendLine("Terminal=false");
        sb.AppendLine("Categories=Graphics;Utility;");
        if (autostart) sb.AppendLine("X-GNOME-Autostart-enabled=true");

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, sb.ToString());
    }

    /// <summary>Returns the program path from the Exec key, without an env prefix or the program's arguments.</summary>
    public static string? GetTargetPath(string path)
    {
        string? exec = GetValue(path, "Exec");
        if (string.IsNullOrEmpty(exec)) return null;

        List<string> words = SplitExec(exec);
        int index = 0;
        if (words.Count > 0 && words[0] == "env")
        {
            index = 1;
            while (index < words.Count && words[index].Contains('=')) index++;
        }

        return index < words.Count ? words[index] : null;
    }

    /// <summary>Splits an Exec value into words, following the desktop entry quoting rules.</summary>
    private static List<string> SplitExec(string exec)
    {
        List<string> words = [];
        StringBuilder word = new();
        bool quoted = false, inWord = false;

        for (int i = 0; i < exec.Length; i++)
        {
            char c = exec[i];
            if (quoted)
            {
                if (c == '\\' && i + 1 < exec.Length) word.Append(exec[++i]);
                else if (c == '"') quoted = false;
                else word.Append(c);
            }
            else if (c == '"')
            {
                quoted = inWord = true;
            }
            else if (c == ' ')
            {
                if (inWord) words.Add(word.ToString());
                word.Clear();
                inWord = false;
            }
            else
            {
                word.Append(c);
                inWord = true;
            }
        }

        if (inWord) words.Add(word.ToString());
        return words;
    }

    /// <summary>True when the desktop environment's startup settings have turned the entry off.</summary>
    public static bool IsDisabled(string path) =>
        string.Equals(GetValue(path, "Hidden"), "true", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(GetValue(path, "X-GNOME-Autostart-enabled"), "false", StringComparison.OrdinalIgnoreCase);

    public static string? GetValue(string path, string key)
    {
        if (!File.Exists(path)) return null;

        bool inMainGroup = false;
        foreach (string rawLine in File.ReadLines(path))
        {
            string line = rawLine.Trim();
            if (line.StartsWith('['))
            {
                inMainGroup = line == "[Desktop Entry]";
            }
            else if (inMainGroup)
            {
                int separator = line.IndexOf('=');
                if (separator > 0 && line[..separator].TrimEnd() == key)
                {
                    return line[(separator + 1)..].TrimStart();
                }
            }
        }

        return null;
    }

    private static string QuoteExecArgument(string value)
    {
        StringBuilder sb = new("\"");
        foreach (char c in value)
        {
            if (c is '"' or '`' or '$' or '\\') sb.Append('\\');
            sb.Append(c);
        }
        return sb.Append('"').ToString();
    }
}
