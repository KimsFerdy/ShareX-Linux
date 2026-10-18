# Linux packages

`build-packages.sh` publishes ShareX as a self-contained Linux build, so the target system does not need .NET, and
packages it as:

- `sharex_<version>_<arch>.deb` for Debian, Ubuntu, Linux Mint and other Debian-based distributions. It installs to
  `/opt/sharex`, adds the `sharex` command, an application menu entry and an icon.
- `ShareX-<version>-<rid>.tar.gz`, a portable folder for other distributions. Run `ShareX` inside it.

## Building

Requires the .NET 10 SDK and `dpkg-deb`.

```sh
ShareX.Setup/Linux/build-packages.sh        # x64
ShareX.Setup/Linux/build-packages.sh arm64  # ARM64
```

The packages are written to `ShareX.Setup/Output/`. Set `DOTNET` to use a specific `dotnet` executable.

## Installing

```sh
sudo apt install ./ShareX.Setup/Output/sharex_<version>_amd64.deb
```

`apt` also installs the recommended packages: FFmpeg for screen recording, mpv for the video player and trimmer, and
`pactl` and `pw-play` for audio devices
and sounds. Install `tesseract-ocr` and its language data for OCR.

## Desktop support

ShareX uses X11 for capture, hotkeys and window detection. Wayland sessions are not supported yet.
