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

/// <summary>
/// The native recorder is built on Direct3D 11 and Media Foundation. On other platforms screen recording uses the
/// FFmpeg recorder, so this type only reports that the native engine is unavailable.
/// </summary>
public sealed class ScreenRecorder : IDisposable, IAsyncDisposable
{
    public Task<RecordingResult> Completion => Task.FromException<RecordingResult>(CreateException());
    public VideoEncoderInfo? Encoder => null;
    public CameraCaptureInfo? Camera => null;

    public event Action<string>? Diagnostic { add { } remove { } }

    public bool IsRecording => false;
    public bool IsPaused => false;
    public TimeSpan Elapsed => TimeSpan.Zero;

    public ScreenRecorder(RecordingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
    }

    public Task PrepareAsync(CancellationToken cancellationToken = default) => throw CreateException();
    public Task StartAsync(CancellationToken cancellationToken = default) => throw CreateException();
    public void Pause() { }
    public void Resume() { }
    public void Resume(System.Drawing.Rectangle region) { }
    public void RequestStop() { }
    public Task<RecordingResult> StopAsync() => Completion;
    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static PlatformNotSupportedException CreateException() =>
        new("Native screen recording requires Windows. Use the FFmpeg recorder instead.");
}
