#!/bin/bash
# Compila a suíte contra as DLLs do próprio jogo e da BepInEx 5 (sem NuGet).
set -e
cd "$(dirname "$0")"
mkdir -p bin
REFS=$(for f in refs/*.dll; do printf -- '-r:%s ' "$f"; done)
dotnet /usr/lib/dotnet/sdk/*/Roslyn/bincore/csc.dll -nologo -noconfig -nostdlib+ -target:library \
  -langversion:9.0 -optimize+ -deterministic -debug:portable -warn:4 -nowarn:1701,1702 \
  -out:bin/WardensLogistics.dll $REFS $(find src -name '*.cs')
echo "OK -> bin/WardensLogistics.dll"
