#!/usr/bin/env python3
"""Marqius.DinkToPdf, DinkToPdf with libwkhtmltox, and PuppeteerSharp with headless Chromium, timed the
same way on this computer with the same invoice (tests/bench/invoice.html).

    python3 tests/bench.py [OUT]          (Linux x64; default OUT: bench-out)

For each: an app is published for this system (tests/bench/Program.cs, built for that converter), and
the space it needs on the server is measured (the app's folder, plus libwkhtmltox for DinkToPdf, plus
the Chromium PuppeteerSharp downloads). Then, each in turn:
  cold       the first PDF of a new process, from the start of Main, the converter started too (median of 5)
  warm       the median and slowest of 30 PDFs one after another
  parallel   100 PDFs, 8 at a time: PDFs a second
Writes OUT/results.json and OUT/summary.md (and the run's summary on GitHub).
"""
import hashlib
import json
import os
import platform
import shutil
import statistics
import subprocess
import sys
import urllib.request
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import compat  # noqa: E402  (the original DinkToPdf's libwkhtmltox, fetched and checked)
import customer  # noqa: E402

HERE = Path(__file__).resolve().parent.parent
PUPPETEER = "25.18.1"
ENGINES = {
    "marqius": ("ENGINE_MARQIUS", '<PackageReference Include="Marqius.DinkToPdf" Version="*" />'),
    "original": ("ENGINE_ORIGINAL", '<PackageReference Include="DinkToPdf" Version="1.0.8" />'),
    "puppeteer": ("ENGINE_PUPPETEER", f'<PackageReference Include="PuppeteerSharp" Version="{PUPPETEER}" />'),
}


def size(path):
    path = Path(path)
    if path.is_file():
        return path.stat().st_size
    return sum(p.stat().st_size for p in path.rglob("*") if p.is_file() and not p.is_symlink())


def run_json(cmd, cwd, env):
    p = customer.run(cmd, cwd, env, timeout=1800)
    line = [l for l in p.stdout.splitlines() if l.startswith("{")][-1]
    return json.loads(line)


def main():
    out = Path(sys.argv[1] if len(sys.argv) > 1 else "bench-out").resolve()
    out.mkdir(parents=True, exist_ok=True)
    if platform.system() != "Linux" or platform.machine().lower() not in ("x86_64", "amd64"):
        sys.exit("the benchmark runs on Linux x64, where all three converters run")
    root, env = customer.workspace()
    results = {}
    for name, (define, reference) in ENGINES.items():
        app = root / name
        app.mkdir()
        (app / "Bench.csproj").write_text(f"""<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>disable</Nullable>
    <DefineConstants>$(DefineConstants);{define}</DefineConstants>
    <!-- DinkToPdf 1.0.8's 2017 dependencies stop a publish for one system with NU1605 unless it is let through -->
    <NoWarn>$(NoWarn);NU1701;NU1605</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    {reference}
    <None Include="invoice.html" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>
</Project>
""", encoding="utf-8")
        shutil.copy(HERE / "tests" / "bench" / "Program.cs", app / "Program.cs")
        shutil.copy(HERE / "tests" / "bench" / "invoice.html", app / "invoice.html")
        publish = app / "publish"
        customer.run(["dotnet", "publish", "-c", "Release", "-r", "linux-x64", "--self-contained", "false", "-o", str(publish)], app, env)
        extra = []
        footprint = size(publish)
        if name == "original":
            native, digest = compat.NATIVE["Linux"]
            data = urllib.request.urlopen(urllib.request.Request(
                f"https://raw.githubusercontent.com/rdvojmoc/DinkToPdf/{compat.COMMIT}/v0.12.4/64%20bit/{native}",
                headers={"User-Agent": "marqius-bench"}), timeout=300).read()
            if hashlib.sha256(data).hexdigest() != digest:
                sys.exit(f"{native} is not the file this benchmark knows")
            (publish / native).write_bytes(data)
            footprint = size(publish)
        if name == "puppeteer":
            chromium = root / "chromium"
            extra = [str(chromium)]
            # the download happens once, before any timing: the first run fetches Chromium
            run_json(["dotnet", str(publish / "Bench.dll"), "cold", "1", "1", str(chromium)], publish, env)
            footprint = size(publish) + size(chromium)
        cold = [run_json(["dotnet", str(publish / "Bench.dll"), "cold", "1", "1", *extra], publish, env)["ms"] for _ in range(5)]
        warm = run_json(["dotnet", str(publish / "Bench.dll"), "warm", "30", "1", *extra], publish, env)
        par = run_json(["dotnet", str(publish / "Bench.dll"), "parallel", "100", "8", *extra], publish, env)
        results[name] = {
            "engine": warm["engine"], "version": warm["version"], "footprint_mb": round(footprint / 1e6, 1),
            "cold_ms": round(statistics.median(cold)), "warm_median_ms": round(warm["median_ms"], 1), "warm_slowest_ms": round(warm["slowest_ms"], 1),
            "pdfs_per_second": round(par["pdfs_per_second"], 1),
        }
        print(json.dumps(results[name]), flush=True)

    cpu = ""
    try:
        cpu = next(l.split(":", 1)[1].strip() for l in open("/proc/cpuinfo") if l.startswith("model name"))
    except (OSError, StopIteration):
        pass
    meta = {"machine": f"{platform.platform()}, {os.cpu_count()} cores, {cpu}", "run": os.environ.get("GITHUB_RUN_ID", ""), "results": results}
    (out / "results.json").write_text(json.dumps(meta, indent=2), encoding="utf-8")
    lines = [f"### The same invoice, three converters, on {meta['machine']}", "",
             "| Converter | First PDF of a new process | Each PDF after that (median / slowest of 30) | 100 PDFs, 8 at a time | Space on the server |",
             "|---|---|---|---|---|"]
    for r in results.values():
        lines.append(f"| {r['engine']} ({r['version']}) | {r['cold_ms']} ms | {r['warm_median_ms']} / {r['warm_slowest_ms']} ms | "
                     f"{r['pdfs_per_second']} a second | {r['footprint_mb']} MB |")
    (out / "summary.md").write_text("\n".join(lines) + "\n", encoding="utf-8")
    if os.environ.get("GITHUB_STEP_SUMMARY"):
        with open(os.environ["GITHUB_STEP_SUMMARY"], "a", encoding="utf-8") as f:
            f.write("\n".join(lines) + "\n")
    print("\n".join(lines))


if __name__ == "__main__":
    main()
