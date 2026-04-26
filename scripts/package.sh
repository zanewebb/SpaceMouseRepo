#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."

VERSION=$(grep -m1 'version_number' thunderstore/manifest.json | sed -E 's/.*"version_number": *"([^"]+)".*/\1/')
OUT="dist/SpaceMouseRepo-${VERSION}.zip"

dotnet build src/SpaceMouseRepo.Plugin -c Release

STAGE="$(mktemp -d)"
trap "rm -rf '$STAGE'" EXIT

mkdir -p "$STAGE/BepInEx/plugins/SpaceMouseRepo"
cp src/SpaceMouseRepo.Plugin/bin/Release/net472/SpaceMouseRepo.dll       "$STAGE/BepInEx/plugins/SpaceMouseRepo/"
cp src/SpaceMouseRepo.Plugin/bin/Release/net472/SpaceMouseRepo.Core.dll  "$STAGE/BepInEx/plugins/SpaceMouseRepo/"
cp src/SpaceMouseRepo.Plugin/bin/Release/net472/HidLibrary.dll           "$STAGE/BepInEx/plugins/SpaceMouseRepo/"

cp thunderstore/manifest.json "$STAGE/"
cp thunderstore/icon.png      "$STAGE/"
cp thunderstore/README.md     "$STAGE/"

mkdir -p dist
(cd "$STAGE" && zip -qr "$OLDPWD/$OUT" .)
echo "Wrote $OUT"
