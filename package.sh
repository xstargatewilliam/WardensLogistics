#!/bin/bash
# Gera o zip para o Nexus/Vortex: BepInEx/plugins/WardensLogistics/... (DLL, ícones, licença).
set -e
cd "$(dirname "$0")"
VERSION=$(grep -oP 'Version = "\K[^"]+' src/Plugin.cs)
STAGE=$(mktemp -d)
DEST="$STAGE/BepInEx/plugins/WardensLogistics"
mkdir -p "$DEST/icons" dist
cp bin/WardensLogistics.dll LICENSE "$DEST/"
cp icons/* "$DEST/icons/"
(cd "$STAGE" && python3 -c "
import zipfile,os,sys
z=zipfile.ZipFile(sys.argv[1],'w',zipfile.ZIP_DEFLATED)
for r,d,f in os.walk('BepInEx'):
    for x in f: z.write(os.path.join(r,x))
" "$OLDPWD/dist/WardensLogistics-$VERSION.zip")
echo "OK -> dist/WardensLogistics-$VERSION.zip"
