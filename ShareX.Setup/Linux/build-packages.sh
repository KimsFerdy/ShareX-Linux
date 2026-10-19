#!/usr/bin/env bash
# Builds the Linux packages of ShareX: a self-contained .deb and a portable .tar.gz.
#
# Usage: ShareX.Setup/Linux/build-packages.sh [x64|arm64]
# Output: ShareX.Setup/Output/
#
# PACKAGE_REVISION (default 1) numbers Linux releases of the same ShareX version, matching the v<version>-linux.<n>
# release tags, so that each release installs as an upgrade of the previous one.
set -euo pipefail

ARCH="${1:-x64}"
case "$ARCH" in
    x64) RID=linux-x64; DEB_ARCH=amd64; PLATFORM=x64 ;;
    arm64) RID=linux-arm64; DEB_ARCH=arm64; PLATFORM=ARM64 ;;
    *) echo "Unknown architecture: $ARCH (expected x64 or arm64)" >&2; exit 1 ;;
esac

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"
OUTPUT_DIR="$REPO_DIR/ShareX.Setup/Output"
WORK_DIR="$(mktemp -d)"
trap 'rm -rf "$WORK_DIR"' EXIT

DOTNET="${DOTNET:-$(command -v dotnet || echo "$HOME/.dotnet/dotnet")}"
if [ ! -x "$DOTNET" ]; then
    echo "The .NET SDK was not found. Install it or set DOTNET to the dotnet executable." >&2
    exit 1
fi

VERSION="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$REPO_DIR/Directory.Build.props" | head -n 1)"
REVISION="${PACKAGE_REVISION:-1}"
PACKAGE_VERSION="$VERSION-$REVISION"
echo "Building ShareX $PACKAGE_VERSION for $RID"

PUBLISH_DIR="$WORK_DIR/publish"
"$DOTNET" publish "$REPO_DIR/ShareX/ShareX.csproj" \
    --configuration Release \
    --runtime "$RID" \
    --self-contained true \
    -p:Platform="$PLATFORM" \
    -p:DebugType=none \
    --output "$PUBLISH_DIR"

mkdir -p "$OUTPUT_DIR"

# Portable archive: the published folder with a launcher.
TAR_ROOT="$WORK_DIR/ShareX-$PACKAGE_VERSION-$RID"
cp -r "$PUBLISH_DIR" "$TAR_ROOT"
install -m 644 "$REPO_DIR/ShareX.HelpersLib/Resources/ShareX_Logo.png" "$TAR_ROOT/sharex.png"
TAR_FILE="$OUTPUT_DIR/ShareX-$PACKAGE_VERSION-$RID.tar.gz"
tar -C "$WORK_DIR" -czf "$TAR_FILE" "ShareX-$PACKAGE_VERSION-$RID"
echo "Created $TAR_FILE"

# Debian package.
DEB_ROOT="$WORK_DIR/deb"
INSTALL_DIR="$DEB_ROOT/opt/sharex"
mkdir -p "$INSTALL_DIR" "$DEB_ROOT/usr/bin" "$DEB_ROOT/usr/share/applications" \
    "$DEB_ROOT/usr/share/icons/hicolor/256x256/apps" "$DEB_ROOT/DEBIAN"
cp -r "$PUBLISH_DIR"/. "$INSTALL_DIR"
chmod 755 "$INSTALL_DIR/ShareX"
ln -s /opt/sharex/ShareX "$DEB_ROOT/usr/bin/sharex"
install -m 644 "$REPO_DIR/ShareX.HelpersLib/Resources/ShareX_Logo.png" \
    "$DEB_ROOT/usr/share/icons/hicolor/256x256/apps/sharex.png"
install -m 644 "$SCRIPT_DIR/sharex.desktop" "$DEB_ROOT/usr/share/applications/sharex.desktop"

INSTALLED_SIZE="$(du -sk "$DEB_ROOT" | cut -f1)"
sed -e "s/@VERSION@/$PACKAGE_VERSION/" -e "s/@ARCH@/$DEB_ARCH/" -e "s/@INSTALLED_SIZE@/$INSTALLED_SIZE/" \
    "$SCRIPT_DIR/control" > "$DEB_ROOT/DEBIAN/control"
install -m 755 "$SCRIPT_DIR/postinst" "$DEB_ROOT/DEBIAN/postinst"
install -m 755 "$SCRIPT_DIR/postrm" "$DEB_ROOT/DEBIAN/postrm"

# Package files must not be group or world writable, whatever the build user's umask is.
chmod -R u+rwX,go+rX,go-w "$DEB_ROOT"

DEB_FILE="$OUTPUT_DIR/sharex_${PACKAGE_VERSION}_${DEB_ARCH}.deb"
dpkg-deb --build --root-owner-group "$DEB_ROOT" "$DEB_FILE"
echo "Created $DEB_FILE"
