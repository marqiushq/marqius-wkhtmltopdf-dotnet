#!/usr/bin/env python3
"""DinkToPdf as people have it (DinkToPdf 1.0.8 with the libwkhtmltox 0.12.4 its repository carries)
beside Marqius.DinkToPdf: the same documents (tests/compat/Cases.cs, every setting that changes a
PDF), made by each on this computer, then compared with marqius-wkhtmltopdf compare.

    python3 tests/compat.py [OUT]       (Linux x64 or Windows x64; default OUT: compat-out)

libwkhtmltox is fetched from DinkToPdf's repository at a fixed commit, and its SHA-256 checked. On
Linux it needs X's libraries (libx11-6 libxext6 libxrender1 libfontconfig1 libfreetype6). Both sides
use the computer's fonts. Writes OUT/original, OUT/marqius, OUT/report (marked-up PDFs and page
pictures where they differ) and OUT/summary.md; exit status 1 when a document is not made by
Marqius.DinkToPdf, or compare finds a blocking difference.
"""
import csv
import hashlib
import os
import platform
import subprocess
import sys
import urllib.request
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import customer  # noqa: E402

HERE = Path(__file__).resolve().parent.parent
COMMIT = "b0546d21641eaef71f6fe0ddfd3998e8502baab7"  # rdvojmoc/DinkToPdf master, unchanged since 2020
# documents whose blocking difference is known, with why: shown in the summary, not failed
EXPECTED = {
    "21-table-over-pages": "expected: libwkhtmltox 0.12.4 draws the table's repeated header over the first row of the next page; Marqius.DinkToPdf draws it above that row",
}
# known faults of ours, while their fix is under way (none: 10-no-images, LoadImages = false, prints
# no picture since marqius-wkhtmltopdf 0.12.15)
KNOWN = {}

# documents made again, each alone in a new process, after the run of all: libwkhtmltox keeps the
# pictures it loaded for the life of the process, so in a run with the others, a picture an earlier
# document loaded reaches a later one that blocks files. An app that always blocks them never loads it.
# (Made alone, DinkToPdf blocks the picture too, as Marqius.DinkToPdf does: run 45355255926.)
ALONE = ("20-local-picture-blocked",)

NATIVE = {
    "Linux": ("libwkhtmltox.so", "f134eb7ecd429d4c182ab41518c0d7d888cbb040ff5571750ca5657a6938975a"),
    "Windows": ("libwkhtmltox.dll", "6763fcc0707eca0a7f8d02495fb58d9c996282bd328573882d8bbbde60666f19"),
}


def project(folder, reference):
    folder.mkdir(parents=True, exist_ok=True)
    (folder / "Compat.csproj").write_text(f"""<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>disable</Nullable>
    <ImplicitUsings>disable</ImplicitUsings>
    <NoWarn>$(NoWarn);NU1701</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    {reference}
  </ItemGroup>
</Project>
""", encoding="utf-8")
    for f in ("Cases.cs", "Program.cs"):
        (folder / f).write_text((HERE / "tests" / "compat" / f).read_text(encoding="utf-8"), encoding="utf-8")


def main():
    out = Path(sys.argv[1] if len(sys.argv) > 1 else "compat-out").resolve()
    system = platform.system()
    if system not in NATIVE or platform.machine().lower() not in ("x86_64", "amd64"):
        sys.exit(f"the original DinkToPdf has a 64-bit libwkhtmltox for Linux and Windows x64 only, not {system} {platform.machine()}")
    native, digest = NATIVE[system]
    root, env = customer.workspace()

    # the original: DinkToPdf 1.0.8 from nuget.org, with its libwkhtmltox beside the app
    original = root / "original"
    project(original, '<PackageReference Include="DinkToPdf" Version="1.0.8" />')
    url = f"https://raw.githubusercontent.com/rdvojmoc/DinkToPdf/{COMMIT}/v0.12.4/64%20bit/{native}"
    data = urllib.request.urlopen(urllib.request.Request(url, headers={"User-Agent": "marqius-compat"}), timeout=300).read()
    if hashlib.sha256(data).hexdigest() != digest:
        sys.exit(f"{native} from DinkToPdf's repository is not the file this test knows (SHA-256 {hashlib.sha256(data).hexdigest()})")
    customer.run(["dotnet", "build", "-c", "Release", "-o", str(original / "bin")], original, env)
    (original / "bin" / native).write_bytes(data)

    # ours: Marqius.DinkToPdf from dist/
    ours = root / "marqius"
    project(ours, '<PackageReference Include="Marqius.DinkToPdf" Version="*" />')
    customer.run(["dotnet", "build", "-c", "Release", "-o", str(ours / "bin")], ours, env)

    lines = []
    for side, folder in (("original", original), ("marqius", ours)):
        target = out / side
        p = customer.run(["dotnet", str(folder / "bin" / "Compat.dll"), str(root / f"work-{side}"), str(target)], folder / "bin", env, check=False)
        print(f"--- {side} (exit {p.returncode})\n{p.stdout}{p.stderr[-2000:]}", flush=True)
        lines.append((side, p.stdout))
    for name in ALONE:
        for side, folder in (("original", original), ("marqius", ours)):
            p = customer.run(["dotnet", str(folder / "bin" / "Compat.dll"), str(root / f"alone-{side}-{name}"), str(out / side), name], folder / "bin", env, check=False)
            print(f"--- {side}, {name} alone in a new process (exit {p.returncode})\n{p.stdout}{p.stderr[-2000:]}", flush=True)

    program = os.environ.get("MARQIUS_WKHTMLTOPDF") or str(HERE / "builds" / "runtimes" / ("win-x64" if system == "Windows" else "linux-x64") / "native" / ("marqius-wkhtmltopdf.exe" if system == "Windows" else "marqius-wkhtmltopdf"))
    report = out / "report"
    c = subprocess.run([program, "compare", str(out / "original"), str(out / "marqius"), "-o", str(report), "--fail-on", "blocking"],
                       capture_output=True, text=True)
    print(c.stdout[-4000:], c.stderr[-1000:], flush=True)

    made = {l.split("\t")[0]: l.split("\t")[1] for _, text in lines[1:] for l in text.splitlines() if "\t" in l}
    rows = list(csv.DictReader(open(report / "summary.csv", encoding="utf-8"))) if (report / "summary.csv").exists() else []
    summary = [f"### DinkToPdf 1.0.8 with libwkhtmltox 0.12.4, beside Marqius.DinkToPdf ({system})", "",
               f"Compared by marqius-wkhtmltopdf compare: {c.stdout.strip().splitlines()[-1] if c.stdout.strip() else 'no report'}", "",
               "| Document | Verdict | Level | Pages (DinkToPdf / ours) | First difference |", "|---|---|---|---|---|"]
    for r in rows:
        first = str(r.get("first", "")).replace("|", "/")[:120]
        summary.append(f"| {r['pair']} | {r['verdict']} | {r['level']} | {r['pages_old']} / {r['pages_new']} | {first} |")
    not_made = [n for n, state in made.items() if state != "made"]
    if not_made:
        summary += ["", f"Not made by Marqius.DinkToPdf: {', '.join(not_made)}"]
    unexplained = []
    notes = []
    for r in rows:
        if r.get("level") != "blocking":
            continue
        why = EXPECTED.get(r["pair"]) or KNOWN.get(r["pair"])
        if why:
            notes.append(f"- **{r['pair']}**: {why}")
        else:
            unexplained.append(r["pair"])
    if notes:
        summary += ["", "Blocking differences already explained:", ""] + notes
    if unexplained:
        summary += ["", f"**Blocking differences not explained: {', '.join(unexplained)}**"]
    (out / "summary.md").write_text("\n".join(summary) + "\n", encoding="utf-8")
    if os.environ.get("GITHUB_STEP_SUMMARY"):
        with open(os.environ["GITHUB_STEP_SUMMARY"], "a", encoding="utf-8") as f:
            f.write("\n".join(summary) + "\n")
    print("\n".join(summary[-(len(notes) + len(unexplained) + 4):]), flush=True)
    sys.exit(1 if not_made or unexplained or not rows else 0)


if __name__ == "__main__":
    main()
