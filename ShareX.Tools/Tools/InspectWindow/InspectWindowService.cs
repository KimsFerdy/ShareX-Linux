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

using ShareX.HelpersLib;
using SkiaSharp;
using System.Runtime.InteropServices;
using AvaloniaBitmap = Avalonia.Media.Imaging.Bitmap;
using ImageFormat = SkiaSharp.SKEncodedImageFormat;

namespace ShareX.Tools;

public sealed class InspectWindowListItem : IDisposable
{
    public IntPtr Handle { get; }
    public string Title { get; }
    public string ProcessName { get; }
    public AvaloniaBitmap? Icon { get; }
    public bool HasIcon => Icon != null;
    public string DisplayName => string.IsNullOrWhiteSpace(ProcessName) ? Title : $"{Title}  —  {ProcessName}";

    public InspectWindowListItem(IntPtr handle, string title, string processName, AvaloniaBitmap? icon)
    {
        Handle = handle;
        Title = title;
        ProcessName = processName;
        Icon = icon;
    }

    public void Dispose()
    {
        Icon?.Dispose();
    }

    public override string ToString() => Title;
}

public static class InspectWindowService
{
    private const uint GA_ROOT = 2;
    private static readonly string[] IgnoredClasses = ["Progman", "Button"];

    public static IntPtr GetWindowAtPoint(int x, int y, bool topLevel)
    {
        // X11 toolkits draw their controls inside the top-level window, so there is only the window to pick.
        if (!OperatingSystem.IsWindows())
        {
            return X11Desktop.GetWindowAtPoint(new System.Drawing.Point(x, y));
        }

        IntPtr handle = WindowFromPoint(new POINT(x, y));
        return topLevel && handle != IntPtr.Zero ? GetAncestor(handle, GA_ROOT) : handle;
    }

    public static IReadOnlyList<InspectWindowListItem> GetVisibleWindows(IntPtr ignoredHandle)
    {
        List<InspectWindowListItem> windows = [];

        bool AddWindow(IntPtr handle, IntPtr _)
        {
            if (handle == ignoredHandle)
            {
                return true;
            }

            try
            {
                WindowInfo window = new(handle);
                string title = window.Text;
                string className = window.ClassName;
                System.Drawing.Rectangle rectangle = window.Rectangle;

                if (window.IsVisible && !window.IsCloaked && !string.IsNullOrWhiteSpace(title) &&
                    rectangle.Width > 0 && rectangle.Height > 0 &&
                    !IgnoredClasses.Contains(className, StringComparer.OrdinalIgnoreCase))
                {
                    windows.Add(new InspectWindowListItem(
                        handle,
                        title,
                        TryGet(() => window.ProcessName),
                        GetWindowIcon(window)));
                }
            }
            catch (Exception ex)
            {
                ToolsDiagnostics.ReportWarning(nameof(InspectWindowService), "Failed to inspect a window while building the window list.", ex);
            }

            return true;
        }

        if (OperatingSystem.IsWindows())
        {
            NativeMethods.EnumWindows(AddWindow, IntPtr.Zero);
        }
        else
        {
            foreach (IntPtr handle in X11Desktop.GetWindowsTopmostFirst())
            {
                AddWindow(handle, IntPtr.Zero);
            }
        }

        return windows.OrderBy(x => x.Title, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }

    private static string TryGet(Func<string> valueFactory)
    {
        try
        {
            return valueFactory() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    public static AvaloniaBitmap? GetWindowIcon(IntPtr handle)
    {
        return handle == IntPtr.Zero ? null : GetWindowIcon(new WindowInfo(handle));
    }

    private static AvaloniaBitmap? GetWindowIcon(WindowInfo window)
    {
        try
        {
            if (!OperatingSystem.IsWindows())
            {
                using SKBitmap? x11Icon = X11Desktop.GetWindowIcon(window.Handle);
                return x11Icon == null ? null : ToAvaloniaBitmap(x11Icon);
            }

            using System.Drawing.Icon? icon = window.Icon;
            if (icon == null || icon.Width <= 0 || icon.Height <= 0)
            {
                return null;
            }

            using SKBitmap bitmap = WindowsImageInterop.FromIcon(icon.Handle);
            return ToAvaloniaBitmap(bitmap);
        }
        catch
        {
            return null;
        }
    }

    private static AvaloniaBitmap ToAvaloniaBitmap(SKBitmap bitmap)
    {
        using MemoryStream stream = new();
        bitmap.Save(stream, ImageFormat.Png);
        stream.Position = 0;
        return new AvaloniaBitmap(stream);
    }

    [DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(POINT point);

    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr handle, uint flags);
}
