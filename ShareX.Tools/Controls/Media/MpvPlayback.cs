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

using Avalonia.Threading;
using ShareX.HelpersLib;
using System.Diagnostics;
using System.Globalization;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ShareX.Tools;

/// <summary>
/// Plays media with mpv embedded in an X11 window, controlled through mpv's JSON IPC socket. Events are raised on the
/// UI thread.
/// </summary>
internal sealed class MpvPlayback : IDisposable
{
    private readonly nint _window;
    private readonly object _sync = new();
    private readonly Dictionary<int, TaskCompletionSource<JsonNode?>> _requests = [];
    private Process? _process;
    private Socket? _socket;
    private NetworkStream? _stream;
    private string? _socketPath;
    private string? _inputConfigPath;
    private TaskCompletionSource? _fileLoaded;
    private int _nextRequestId;
    private bool _paused = true;
    private bool _ended;
    private bool _disposed;

    public event Action<double>? PositionChanged;
    public event Action<bool>? IsPlayingChanged;
    public event Action<Exception>? PlaybackFailed;
    public event Action? VideoClicked;

    public double Duration { get; private set; }
    public bool IsPlaybackRequested { get; private set; }

    public MpvPlayback(nint window)
    {
        _window = window;
    }

    public static bool IsInstalled => LinuxEnvironment.FindExecutable("mpv") != null;

    public async Task<VideoPlaybackInfo> LoadAsync(string path, double volume, bool muted, CancellationToken token)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await EnsureStartedAsync(token);

        Unload();
        TaskCompletionSource fileLoaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_sync) _fileLoaded = fileLoaded;

        await CommandAsync(token, "set_property", "volume", Math.Round(Math.Clamp(volume, 0, 1) * 100));
        await CommandAsync(token, "set_property", "mute", muted);
        await CommandAsync(token, "set_property", "pause", true);
        await CommandAsync(token, "loadfile", Path.GetFullPath(path), "replace");
        await fileLoaded.Task.WaitAsync(TimeSpan.FromSeconds(30), token);

        Duration = (await GetDoubleAsync("duration", token)) ?? 0;
        int width = (int)((await GetDoubleAsync("width", token)) ?? 0);
        int height = (int)((await GetDoubleAsync("height", token)) ?? 0);
        _ended = false;
        return new VideoPlaybackInfo(Duration, width, height);
    }

    public void Unload()
    {
        IsPlaybackRequested = false;
        Duration = 0;
        if (_stream != null) Send("stop");
    }

    public void Play()
    {
        if (Duration <= 0) return;
        IsPlaybackRequested = true;
        if (_ended) Send("seek", 0, "absolute+exact");
        Send("set_property", "pause", false);
    }

    public void Pause()
    {
        IsPlaybackRequested = false;
        Send("set_property", "pause", true);
        IsPlayingChanged?.Invoke(false);
    }

    public void Seek(double seconds)
    {
        if (Duration <= 0 || !double.IsFinite(seconds)) return;
        Pause();
        // Seeking exactly to the end has no frame left to show.
        Send("seek", Math.Clamp(seconds, 0, Math.Max(0, Duration - 0.001)), "absolute+exact");
    }

    public void StepFrame(bool forward)
    {
        if (Duration <= 0) return;
        IsPlaybackRequested = false;
        Send(forward ? "frame-step" : "frame-back-step");
    }

    public void SetVolume(double volume) => Send("set_property", "volume", Math.Round(Math.Clamp(volume, 0, 1) * 100));

    public void SetMuted(bool muted) => Send("set_property", "mute", muted);

    private async Task EnsureStartedAsync(CancellationToken token)
    {
        if (_process is { HasExited: false } && _stream != null) return;

        string? mpvPath = LinuxEnvironment.FindExecutable("mpv");
        if (mpvPath == null)
        {
            throw new InvalidOperationException("Video playback on Linux requires mpv. Install it with your package manager, for example \"sudo apt install mpv\".");
        }

        string runtimeFolder = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR") is { Length: > 0 } folder && Directory.Exists(folder)
            ? folder : Path.GetTempPath();
        string id = Guid.NewGuid().ToString("N");
        _socketPath = Path.Combine(runtimeFolder, $"sharex-mpv-{id}.sock");
        _inputConfigPath = Path.Combine(runtimeFolder, $"sharex-mpv-{id}.conf");
        // Clicks on the video go back to ShareX, which owns all other input.
        File.WriteAllText(_inputConfigPath, "MBTN_LEFT script-message sharex-click\n");

        ProcessStartInfo startInfo = new(mpvPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (string argument in new[]
        {
            "--wid=" + _window.ToString(CultureInfo.InvariantCulture),
            "--input-ipc-server=" + _socketPath,
            "--input-conf=" + _inputConfigPath,
            "--input-default-bindings=no", "--input-vo-keyboard=no", "--osc=no", "--osd-level=0",
            "--idle=yes", "--keep-open=always", "--pause", "--force-window=yes", "--hwdec=auto-safe",
            // Errors go to stderr, which is logged; mpv must not read the terminal.
            "--cursor-autohide=no", "--input-terminal=no", "--msg-level=all=error,ffmpeg=no"
        })
        {
            startInfo.ArgumentList.Add(argument);
        }

        _process = Process.Start(startInfo) ?? throw new InvalidOperationException("mpv could not be started.");
        _process.OutputDataReceived += (_, _) => { };
        _process.ErrorDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) DebugHelper.WriteLine("mpv: " + e.Data); };
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();

        Stopwatch timer = Stopwatch.StartNew();
        while (true)
        {
            token.ThrowIfCancellationRequested();
            if (_process.HasExited) throw new InvalidOperationException("mpv exited while starting.");

            if (File.Exists(_socketPath))
            {
                try
                {
                    Socket socket = new(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                    await socket.ConnectAsync(new UnixDomainSocketEndPoint(_socketPath), token);
                    _socket = socket;
                    _stream = new NetworkStream(socket, ownsSocket: false);
                    break;
                }
                catch (SocketException) when (timer.ElapsedMilliseconds < 10000)
                {
                }
            }

            if (timer.ElapsedMilliseconds >= 10000) throw new TimeoutException("mpv did not open its control socket.");
            await Task.Delay(50, token);
        }

        Thread reader = new(ReadEvents) { IsBackground = true, Name = "ShareX mpv events" };
        reader.Start(_stream);

        Send("observe_property", 1, "time-pos");
        Send("observe_property", 2, "pause");
        Send("observe_property", 3, "eof-reached");
    }

    private void ReadEvents(object? state)
    {
        NetworkStream stream = (NetworkStream)state!;
        using StreamReader reader = new(stream, Encoding.UTF8);

        try
        {
            while (reader.ReadLine() is string line)
            {
                JsonNode? message;
                try
                {
                    message = JsonNode.Parse(line);
                }
                catch (JsonException)
                {
                    continue;
                }

                if (message?["request_id"]?.GetValue<int>() is int requestId && requestId != 0)
                {
                    TaskCompletionSource<JsonNode?>? request;
                    lock (_sync) _requests.Remove(requestId, out request);
                    request?.TrySetResult(message);
                }
                else if (message?["event"]?.GetValue<string>() is string name)
                {
                    Dispatcher.UIThread.Post(() => OnEvent(name, message));
                }
            }
        }
        catch (IOException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private void OnEvent(string name, JsonNode message)
    {
        if (_disposed) return;

        switch (name)
        {
            case "file-loaded":
                lock (_sync) _fileLoaded?.TrySetResult();
                break;
            case "end-file" when message["reason"]?.GetValue<string>() == "error":
                string error = message["file_error"]?.GetValue<string>() ?? "unknown error";
                Exception exception = new InvalidOperationException("mpv could not play the file: " + error);
                lock (_sync) _fileLoaded?.TrySetException(exception);
                PlaybackFailed?.Invoke(exception);
                break;
            case "client-message":
                JsonArray? args = message["args"] as JsonArray;
                if (args?.Count > 0 && args[0]?.GetValue<string>() == "sharex-click" && Duration > 0) VideoClicked?.Invoke();
                break;
            case "property-change":
                OnPropertyChanged(message["name"]?.GetValue<string>(), message["data"]);
                break;
        }
    }

    private void OnPropertyChanged(string? name, JsonNode? data)
    {
        switch (name)
        {
            case "time-pos" when data != null && Duration > 0:
                PositionChanged?.Invoke(Math.Clamp(data.GetValue<double>(), 0, Duration));
                break;
            case "pause" when data != null:
                _paused = data.GetValue<bool>();
                if (_paused) IsPlaybackRequested = false;
                IsPlayingChanged?.Invoke(!_paused);
                break;
            case "eof-reached":
                _ended = data?.GetValue<bool>() == true;
                break;
        }
    }

    private void Send(params object[] command)
    {
        NetworkStream? stream = _stream;
        if (stream == null) return;

        try
        {
            byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { command }) + "\n");
            lock (_sync) stream.Write(bytes);
        }
        catch (IOException e)
        {
            DebugHelper.WriteException(e, "mpv command failed.");
        }
    }

    private async Task<JsonNode?> CommandAsync(CancellationToken token, params object[] command)
    {
        NetworkStream stream = _stream ?? throw new InvalidOperationException("mpv is not running.");
        TaskCompletionSource<JsonNode?> response = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int requestId;
        lock (_sync)
        {
            requestId = ++_nextRequestId;
            _requests[requestId] = response;
        }

        byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["command"] = command,
            ["request_id"] = requestId
        }) + "\n");
        lock (_sync) stream.Write(bytes);

        return await response.Task.WaitAsync(TimeSpan.FromSeconds(10), token);
    }

    private async Task<double?> GetDoubleAsync(string property, CancellationToken token)
    {
        JsonNode? response = await CommandAsync(token, "get_property", property);
        return response?["error"]?.GetValue<string>() == "success" && response["data"] is JsonValue value &&
            value.TryGetValue(out double result) ? result : null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Send("quit");
        _stream?.Dispose();
        _socket?.Dispose();

        if (_process != null)
        {
            if (!_process.WaitForExit(1000))
            {
                try { _process.Kill(); } catch (InvalidOperationException) { }
            }
            _process.Dispose();
        }

        foreach (string? path in new[] { _socketPath, _inputConfigPath })
        {
            try { if (path != null) File.Delete(path); } catch (IOException) { }
        }

        lock (_sync)
        {
            foreach (TaskCompletionSource<JsonNode?> request in _requests.Values) request.TrySetCanceled();
            _requests.Clear();
            _fileLoaded?.TrySetCanceled();
        }
    }
}
