#!/usr/bin/env python3
"""Makes the NuGet packages Marqius.Wkhtmltopdf and Marqius.DinkToPdf for one release of marqius-wkhtmltopdf.

    python3 build.py VERSION [PACKAGE_VERSION]

1. `.github/release.py fetch` downloads each program of release vVERSION into builds/raw/, checked
   against the release's SHA256SUMS, its .sha256 file and its GitHub build provenance (skipped when
   builds/raw/ already holds that release's programs with a matching SHA256SUMS).
2. Each program goes into builds/runtimes/<rid>/native/ under the name the packages give it, and
   builds/Builds.cs records the version and each program's SHA-256, which the package checks before
   its first run.
3. `dotnet pack` makes both packages in dist/, as PACKAGE_VERSION (default: VERSION; a fix to the
   packages alone adds a fourth number, 0.12.14.1).
"""
import hashlib
import os
import re
import shutil
import subprocess
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
BUILDS = HERE / "builds"
RAW = BUILDS / "raw"
# the release's file names, and where each goes in the packages
LAYOUT = {
    "marqius-wkhtmltopdf-windows-x86_64.exe": ("win-x64", "marqius-wkhtmltopdf.exe"),
    "marqius-wkhtmltopdf-linux-x86_64": ("linux-x64", "marqius-wkhtmltopdf"),
    "marqius-wkhtmltopdf-linux-arm64": ("linux-arm64", "marqius-wkhtmltopdf"),
    "marqius-wkhtmltopdf-macos-arm64": ("osx-arm64", "marqius-wkhtmltopdf"),
}


def sha256(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for block in iter(lambda: f.read(1 << 20), b""):
            h.update(block)
    return h.hexdigest()


def have_release(version):
    """builds/raw holds this release's programs already (SHA256SUMS written by a fetch of it)."""
    sums = RAW / "SHA256SUMS"
    stamp = RAW / "VERSION"
    if not (sums.exists() and stamp.exists() and stamp.read_text().strip() == version):
        return False
    for line in sums.read_text().splitlines():
        digest, name = line.split()
        if not (RAW / name).exists() or sha256(RAW / name) != digest:
            return False
    return True


def main():
    if len(sys.argv) not in (2, 3) or not re.fullmatch(r"\d+\.\d+\.\d+", sys.argv[1]):
        sys.exit(__doc__)
    version = sys.argv[1]
    package_version = sys.argv[2] if len(sys.argv) == 3 else version
    if not re.fullmatch(re.escape(version) + r"(\.\d+)?", package_version):
        sys.exit(f"the package version {package_version} must be {version} or {version}.N")

    if not have_release(version):
        if RAW.exists():
            shutil.rmtree(RAW)
        subprocess.run([sys.executable, "-I", str(HERE / ".github" / "release.py"), "fetch", version, str(RAW)], check=True)
        (RAW / "VERSION").write_text(version + "\n")

    runtimes = BUILDS / "runtimes"
    if runtimes.exists():
        shutil.rmtree(runtimes)
    sums = {}
    for release_name, (rid, name) in LAYOUT.items():
        folder = runtimes / rid / "native"
        folder.mkdir(parents=True)
        shutil.copyfile(RAW / release_name, folder / name)
        os.chmod(folder / name, 0o755)
        sums[rid] = sha256(folder / name)
        print(f"{rid:<12} {name:<26} {sums[rid]}")

    cases = "\n".join(f'                case "{rid}": return "{digest}";' for rid, digest in sums.items())
    (BUILDS / "Builds.cs").write_text(f"""// Written by build.py: the release these packages carry, and each program's SHA-256.
namespace Marqius.Wkhtmltopdf
{{
    internal static class Builds
    {{
        internal const string Version = "{version}";

        internal static string Sha256(string runtimeId)
        {{
            switch (runtimeId)
            {{
{cases}
                default: return null;
            }}
        }}
    }}
}}
""", encoding="utf-8")

    dist = HERE / "dist"
    if dist.exists():
        shutil.rmtree(dist)
    for project in ("src/Marqius.Wkhtmltopdf/Marqius.Wkhtmltopdf.csproj", "src/Marqius.DinkToPdf/Marqius.DinkToPdf.csproj"):
        subprocess.run(["dotnet", "pack", str(HERE / project), "-c", "Release", "-o", str(dist),
                        f"-p:Version={package_version}", "-p:MarqiusVersion=" + version, "--nologo"], check=True)
    for p in sorted(dist.iterdir()):
        print(f"{p.name}  {p.stat().st_size / 1e6:.1f} MB")


if __name__ == "__main__":
    main()
