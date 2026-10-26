#!/usr/bin/env bash
# Prints the Homebrew cask of a release, for n2jsoft-public-org/homebrew-tap: run by .github/workflows/release.yml.
# Usage: scripts/homebrew-cask.sh <version> <checksums.txt>
set -euo pipefail

version="$1"
checksums="$2"

sha() {
  local sum
  sum="$(awk -v f="modularguard-$1.tar.gz" '$2 == f { print $1 }' "$checksums")"
  [ -n "$sum" ] || { echo "No checksum for modularguard-$1.tar.gz in $checksums" >&2; exit 1; }
  echo "$sum"
}

# Assignments, not substitutions inside the heredoc: set -e stops on a missing checksum.
osx_arm64="$(sha osx-arm64)"
osx_x64="$(sha osx-x64)"
linux_arm64="$(sha linux-arm64)"
linux_x64="$(sha linux-x64)"

url='https://github.com/n2jsoft-public-org/ModularGuard/releases/download/v#{version}/modularguard'

cat <<EOF
cask "modularguard" do
  version "$version"

  on_macos do
    on_arm do
      sha256 "$osx_arm64"
      url "$url-osx-arm64.tar.gz"
    end
    on_intel do
      sha256 "$osx_x64"
      url "$url-osx-x64.tar.gz"
    end
  end
  on_linux do
    on_arm do
      sha256 "$linux_arm64"
      url "$url-linux-arm64.tar.gz"
    end
    on_intel do
      sha256 "$linux_x64"
      url "$url-linux-x64.tar.gz"
    end
  end

  name "modularguard"
  desc "Validate project references in a modular monolith .NET solution"
  homepage "https://github.com/n2jsoft-public-org/ModularGuard"

  binary "modularguard"

  livecheck do
    skip "Auto-generated on release."
  end

  # The binaries are not notarized: without this, macOS refuses to run them.
  postflight do
    system_command "/usr/bin/xattr", args: ["-dr", "com.apple.quarantine", "#{staged_path}/modularguard"] if OS.mac?
  end
end
EOF
