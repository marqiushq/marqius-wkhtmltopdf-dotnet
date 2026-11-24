#!/usr/bin/env python3
"""Marqius.DinkToPdf has DinkToPdf 1.0.8's API, so code written for DinkToPdf compiles, and code built
against it runs, unchanged.

    python3 tests/api.py              (after python3 build.py VERSION: reads dist/Marqius.DinkToPdf.*.nupkg)

DinkToPdf 1.0.8 comes from nuget.org, checked against its SHA-256. tests/api (ApiList) prints each
assembly's public and protected API, a line per type and member. The check fails when:
  - anything DinkToPdf 1.0.8 has is missing or different (a type, member, signature, default value,
    modifier, [WkHtml] setting name, enum value), for each framework the package carries;
  - anything new appears in DinkToPdf's own namespaces, where it could clash with an app's code
    (the one exception: PdfTools.Timeout, a static property of a sealed class);
  - the assembly is not "DinkToPdf", unsigned, at version 1.0.8.0 or above, which is what lets code
    built against DinkToPdf 1.0.8 load it.
Anything Marqius.DinkToPdf adds lives in the namespace Marqius.DinkToPdf, which no existing code imports.
"""
import hashlib
import os
import subprocess
import sys
import tempfile
import urllib.request
import zipfile
from pathlib import Path

HERE = Path(__file__).resolve().parent.parent
ORIGINAL = ("https://api.nuget.org/v3-flatcontainer/dinktopdf/1.0.8/dinktopdf.1.0.8.nupkg",
            "6b20bb84e587ca250906c7eae5e681f158060dba4ec3a505f5593125d4ace969")
ALLOWED = {
    "  DinkToPdf.PdfTools: static System.Nullable<System.TimeSpan> Timeout { get; set; }",
}
OURS = "Marqius.DinkToPdf"


def api(lister, dll):
    out = subprocess.run(["dotnet", str(lister), str(dll)], capture_output=True, text=True, check=True).stdout.splitlines()
    return out[0], out[1:]


def main():
    work = Path(tempfile.mkdtemp(prefix="marqius-api-"))
    url, digest = ORIGINAL
    data = urllib.request.urlopen(urllib.request.Request(url, headers={"User-Agent": "marqius-api-check"}), timeout=120).read()
    if hashlib.sha256(data).hexdigest() != digest:
        sys.exit(f"{url} is not the DinkToPdf 1.0.8 this check knows")
    (work / "original.nupkg").write_bytes(data)
    with zipfile.ZipFile(work / "original.nupkg") as z:
        (work / "original").mkdir()
        (work / "original" / "DinkToPdf.dll").write_bytes(z.read("lib/netstandard1.6/DinkToPdf.dll"))
    ours = sorted((HERE / "dist").glob("Marqius.DinkToPdf.*.nupkg"))
    ours = [p for p in ours if not p.name.endswith(".snupkg")]
    if not ours:
        sys.exit("no dist/Marqius.DinkToPdf.*.nupkg: run python3 build.py VERSION first")
    subprocess.run(["dotnet", "build", str(HERE / "tests" / "api"), "-c", "Release", "-o", str(work / "lister"), "-nologo", "-v", "q"],
                   check=True, capture_output=True, text=True)
    lister = work / "lister" / "ApiList.dll"

    identity, original = api(lister, work / "original" / "DinkToPdf.dll")
    original = set(original)
    lines, failures = [], 0
    with zipfile.ZipFile(ours[-1]) as z:
        frameworks = sorted({n.split("/")[1] for n in z.namelist() if n.startswith("lib/") and n.endswith("/DinkToPdf.dll")})
        for fw in frameworks:
            (work / fw).mkdir()
            (work / fw / "DinkToPdf.dll").write_bytes(z.read(f"lib/{fw}/DinkToPdf.dll"))
    for fw in frameworks:
        ident, have = api(lister, work / fw / "DinkToPdf.dll")
        have = set(have)
        _, name, version, _, key = ident.split()
        major = tuple(int(x) for x in version.split("."))
        problems = []
        if name != "DinkToPdf" or key != "none" or major < (1, 0, 8, 0):
            problems.append(f"the assembly is {name} {version}, key {key}: code built against DinkToPdf 1.0.8 needs DinkToPdf, unsigned, 1.0.8.0 or above")
        missing = sorted(original - have)
        added = sorted(l for l in have - original
                       if l not in ALLOWED and not l.startswith(f"  {OURS}.") and f" {OURS}." not in l.split(":")[0])
        problems += [f"missing or different: {l.strip()}" for l in missing]
        problems += [f"new in DinkToPdf's namespaces: {l.strip()}" for l in added]
        extras = sorted(l for l in have - original if l not in added)
        failures += len(problems)
        lines.append(f"| {fw} | {name} {version} | {len(original) - len(missing)} of {len(original)} lines of DinkToPdf 1.0.8's API | "
                     + ("<br>".join(e.strip() for e in extras) or "none") + " | " + ("**FAIL**: " + "<br>".join(problems) if problems else "pass") + " |")
        for p in problems:
            print(f"FAIL\t{fw}\t{p}")
    table = ["### Marqius.DinkToPdf beside DinkToPdf 1.0.8's API", "",
             f"DinkToPdf 1.0.8 from nuget.org (SHA-256 {digest[:16]}…), {identity.split()[1]} {identity.split()[2]}.", "",
             "| Framework | Assembly | DinkToPdf's API | Added (outside DinkToPdf's namespaces, or allowed) | Verdict |", "|---|---|---|---|---|"] + lines
    print("\n".join(table))
    if os.environ.get("GITHUB_STEP_SUMMARY"):
        with open(os.environ["GITHUB_STEP_SUMMARY"], "a", encoding="utf-8") as f:
            f.write("\n".join(table) + "\n")
    sys.exit(1 if failures else 0)


if __name__ == "__main__":
    main()
