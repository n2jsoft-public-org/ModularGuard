#!/usr/bin/env bash

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Configuration
REPO="n2jsoft-public-org/ModularGuard"
INSTALL_DIR="$HOME/.local/bin"
BINARY_NAME="modularguard"

echo "Installing ModularGuard..."

# Detect OS
OS="$(uname -s)"
case "$OS" in
    Linux*)     OS_TYPE="linux";;
    Darwin*)    OS_TYPE="osx";;
    *)
        echo -e "${RED}Error: Unsupported operating system: $OS${NC}"
        exit 1
        ;;
esac

# Detect architecture
ARCH="$(uname -m)"
case "$ARCH" in
    x86_64)     ARCH_TYPE="x64";;
    amd64)      ARCH_TYPE="x64";;
    arm64)      ARCH_TYPE="arm64";;
    aarch64)    ARCH_TYPE="arm64";;
    *)
        echo -e "${RED}Error: Unsupported architecture: $ARCH${NC}"
        exit 1
        ;;
esac

# Construct download URL
ARTIFACT_NAME="modularguard-${OS_TYPE}-${ARCH_TYPE}"
DOWNLOAD_URL="https://github.com/${REPO}/releases/latest/download/${ARTIFACT_NAME}.tar.gz"

echo "Detected platform: ${OS_TYPE}-${ARCH_TYPE}"
echo "Download URL: ${DOWNLOAD_URL}"

# Create installation directory
mkdir -p "$INSTALL_DIR"

# Create temporary directory for download
TMP_DIR=$(mktemp -d)
trap "rm -rf $TMP_DIR" EXIT

echo "Downloading ModularGuard..."
if command -v curl >/dev/null 2>&1; then
    curl -fsSL "$DOWNLOAD_URL" -o "$TMP_DIR/${ARTIFACT_NAME}.tar.gz"
elif command -v wget >/dev/null 2>&1; then
    wget -q "$DOWNLOAD_URL" -O "$TMP_DIR/${ARTIFACT_NAME}.tar.gz"
else
    echo -e "${RED}Error: Neither curl nor wget found. Please install one of them.${NC}"
    exit 1
fi

echo "Extracting archive..."
tar -xzf "$TMP_DIR/${ARTIFACT_NAME}.tar.gz" -C "$TMP_DIR"

# Find the binary (it might be named modularguard or CLI depending on the build)
BINARY_PATH=""
if [ -f "$TMP_DIR/$BINARY_NAME" ]; then
    BINARY_PATH="$TMP_DIR/$BINARY_NAME"
elif [ -f "$TMP_DIR/CLI" ]; then
    BINARY_PATH="$TMP_DIR/CLI"
else
    echo -e "${RED}Error: Binary not found in archive${NC}"
    exit 1
fi

echo "Installing to $INSTALL_DIR/$BINARY_NAME..."
cp "$BINARY_PATH" "$INSTALL_DIR/$BINARY_NAME"
chmod +x "$INSTALL_DIR/$BINARY_NAME"

echo -e "${GREEN}✓ ModularGuard installed successfully!${NC}"

# Check if directory is in PATH
if [[ ":$PATH:" != *":$INSTALL_DIR:"* ]]; then
    echo ""
    echo -e "${YELLOW}Warning: $INSTALL_DIR is not in your PATH${NC}"
    echo ""
    echo "To add it to your PATH, add this line to your shell configuration file:"
    echo ""

    # Detect shell and provide appropriate instructions
    SHELL_NAME=$(basename "$SHELL")
    case "$SHELL_NAME" in
        bash)
            echo "  echo 'export PATH=\"\$HOME/.local/bin:\$PATH\"' >> ~/.bashrc"
            echo "  source ~/.bashrc"
            ;;
        zsh)
            echo "  echo 'export PATH=\"\$HOME/.local/bin:\$PATH\"' >> ~/.zshrc"
            echo "  source ~/.zshrc"
            ;;
        fish)
            echo "  fish_add_path ~/.local/bin"
            ;;
        *)
            echo "  export PATH=\"\$HOME/.local/bin:\$PATH\""
            ;;
    esac
    echo ""
    echo "Or run ModularGuard using the full path: $INSTALL_DIR/$BINARY_NAME"
else
    echo ""
    echo "Run 'modularguard --help' to get started!"
fi
