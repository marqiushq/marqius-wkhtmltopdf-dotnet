#!/usr/bin/env python3
"""What a developer does with the packages, run as the READMEs print it, in empty folders.

    python3 tests/customer.py [quickstart|dinktopdf-example|aspnet|framework]...   (default: the first two)

The packages come from dist/ (python3 build.py VERSION makes them), through a nuget.config that
puts that folder first, as nuget.org will serve them; NuGet's cache is a folder of its own, so a
package rebuilt with the same version is never served stale. Each check prints "pass" or "FAIL",
a tab, its name, a tab, the detail; the exit status is 1 when one fails.

  quickstart         both READMEs' "Make your first PDF": dotnet new console, dotnet add package,
                     the README's Program.cs, dotnet run; the PDF must be there
  dinktopdf-example  DinkToPdf's own example app (its Program.cs from DinkToPdf's repository, which
                     the folder tests/dinktopdf-example keeps), unchanged but for its last line,
                     Console.ReadKey(), which cannot run without a console
  aspnet             the README's ASP.NET Core lines in a minimal API, published, run, asked for
                     /invoice
  framework          (Windows) a .NET Framework 4.8 console app with the package, built and run
"""
import os
import re
import shutil
import socket
import subprocess
import sys
import tempfile
import time
import urllib.request
from pathlib import Path

HERE = Path(__file__).resolve().parent.parent
DIST = HERE / "dist"
failed = 0


def report(ok, name, detail=""):
    global failed
    failed += 0 if ok else 1
    print(f"{'pass' if ok else 'FAIL'}\t{name}\t{str(detail).strip()[:400]}", flush=True)


def example(readme, label):
    """The code block a README marks <!-- example: LABEL -->."""
    text = (HERE / readme).read_text(encoding="utf-8")
    m = re.search(r"<!-- example: " + re.escape(label) + r" -->\n```[a-z]*\n(.*?)\n```", text, re.S)
    if not m:
        raise SystemExit(f"{readme} has no example {label!r}")
    return m.group(1) + "\n"


def run(cmd, cwd, env, timeout=900, check=True):
    p = subprocess.run(cmd, cwd=cwd, env=env, capture_output=True, text=True, timeout=timeout,
                       shell=isinstance(cmd, str))
    if check and p.returncode != 0:
        raise RuntimeError(f"{cmd if isinstance(cmd, str) else ' '.join(cmd)} -> {p.returncode}: {(p.stdout + p.stderr)[-1500:]}")
    return p


def is_pdf(path):
    data = Path(path).read_bytes() if Path(path).exists() else b""
    return data[:5] == b"%PDF-" and b"%%EOF" in data[-64:]


def workspace():
    root = Path(tempfile.mkdtemp(prefix="marqius-customer-"))
    env = dict(os.environ, DOTNET_CLI_TELEMETRY_OPTOUT="1", DOTNET_NOLOGO="1", NUGET_PACKAGES=str(root / "nuget-cache"))
    (root / "nuget.config").write_text(f"""<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="these packages" value="{DIST}" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
""", encoding="utf-8")
    return root, env


def quickstart():
    for readme, package in (("README.md", "Marqius.DinkToPdf"), ("src/Marqius.Wkhtmltopdf/README.md", "Marqius.Wkhtmltopdf")):
        name = f"{package}: Make your first PDF, as its README prints it"
        try:
            root, env = workspace()
            # the README's three lines: dotnet new console -o Invoice; cd Invoice; dotnet add package
            run(["dotnet", "new", "console", "-o", "Invoice"], root, env)
            app = root / "Invoice"
            run(["dotnet", "add", "package", package], app, env)
            (app / "Program.cs").write_text(example(readme, "quickstart Program.cs"), encoding="utf-8")
            started = time.time()
            out = run(["dotnet", "run"], app, env)
            ok = is_pdf(app / "invoice.pdf")
            report(ok, name, f"invoice.pdf made in {time.time() - started:.1f} s (with the build)" if ok else out.stdout + out.stderr)
        except Exception as e:  # noqa: BLE001 (each check reports, the next still runs)
            report(False, name, e)


def dinktopdf_example():
    name = "DinkToPdf's own example app, from DinkToPdf's repository"
    try:
        root, env = workspace()
        run(["dotnet", "new", "console", "-o", "Example"], root, env)
        app = root / "Example"
        run(["dotnet", "add", "package", "Marqius.DinkToPdf"], app, env)
        source = (HERE / "tests" / "dinktopdf-example" / "Program.cs").read_text(encoding="utf-8-sig")
        assert source.count("Console.ReadKey();") == 1
        (app / "Program.cs").write_text(source.replace("Console.ReadKey();", "// Console.ReadKey(); needs a console"), encoding="utf-8")
        out = run(["dotnet", "run"], app, env)
        pdfs = [p for p in app.iterdir() if p.name.endswith(".pdf")] + list((app / "Files").glob("*.pdf"))
        events = [l for l in out.stdout.splitlines() if l.startswith(("Phase changed", "Progress changed", "Conversion"))]
        ok = len(pdfs) == 1 and is_pdf(pdfs[0]) and "Conversion successful " in out.stdout
        report(ok, name, f"{pdfs[0].name if pdfs else 'no PDF'}; events: {'; '.join(events)}" if ok else out.stdout + out.stderr)
    except Exception as e:  # noqa: BLE001
        report(False, name, e)


def free_port():
    with socket.socket() as s:
        s.bind(("127.0.0.1", 0))
        return s.getsockname()[1]


def aspnet():
    name = "ASP.NET Core: the README's register and endpoint lines, published and asked for /invoice"
    try:
        root, env = workspace()
        run(["dotnet", "new", "web", "-o", "MyApp"], root, env)
        app = root / "MyApp"
        run(["dotnet", "add", "package", "Marqius.DinkToPdf"], app, env)
        register = example("README.md", "aspnet register")
        usings = "".join(l + "\n" for l in register.splitlines() if l.startswith("using "))
        register = "".join(l + "\n" for l in register.splitlines() if not l.startswith("using ") and l.strip())
        program = (usings + "var builder = WebApplication.CreateBuilder(args);\n" + register
                   + "var app = builder.Build();\n" + example("README.md", "aspnet endpoint") + "app.Run();\n")
        (app / "Program.cs").write_text(program, encoding="utf-8")
        run(["dotnet", "publish", "-c", "Release", "-o", str(app / "publish")], app, env)
        port = free_port()
        server = subprocess.Popen(["dotnet", "MyApp.dll", "--urls", f"http://127.0.0.1:{port}"], cwd=app / "publish", env=env,
                                  stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
        try:
            body = b""
            for _ in range(60):
                try:
                    with urllib.request.urlopen(f"http://127.0.0.1:{port}/invoice", timeout=60) as r:
                        body = r.read()
                        kind = r.headers.get("Content-Type")
                    break
                except OSError:
                    time.sleep(1)
            ok = body[:5] == b"%PDF-"
            report(ok, name, f"{len(body)} bytes, {kind}" if ok else "no PDF")
        finally:
            server.kill()
    except Exception as e:  # noqa: BLE001
        report(False, name, e)


def framework():
    name = ".NET Framework 4.8: a console app with Marqius.DinkToPdf"
    if os.name != "nt":
        report(True, name, "skipped: Windows only")
        return
    try:
        root, env = workspace()
        app = root / "Net48"
        app.mkdir()
        (app / "Net48.csproj").write_text("""<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net48</TargetFramework>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Marqius.DinkToPdf" Version="*" />
  </ItemGroup>
</Project>
""", encoding="utf-8")
        (app / "Program.cs").write_text("""using System.IO;
using DinkToPdf;

class Program
{
    static void Main()
    {
        var converter = new SynchronizedConverter(new PdfTools());
        var pdf = converter.Convert(new HtmlToPdfDocument
        {
            Objects = { new ObjectSettings { HtmlContent = "<h1>Invoice 42</h1><p>.NET Framework 4.8</p>" } },
        });
        File.WriteAllBytes("invoice.pdf", pdf);
    }
}
""", encoding="utf-8")
        run(["dotnet", "build", "-c", "Release"], app, env)
        exe = app / "bin" / "Release" / "net48" / "Net48.exe"
        run([str(exe)], exe.parent, env)
        ok = is_pdf(exe.parent / "invoice.pdf")
        report(ok, name, "invoice.pdf made by the app's own marqius-wkhtmltopdf.exe" if ok else "no PDF")
    except Exception as e:  # noqa: BLE001
        report(False, name, e)


CHECKS = {"quickstart": quickstart, "dinktopdf-example": dinktopdf_example, "aspnet": aspnet, "framework": framework}

if __name__ == "__main__":
    if not any(DIST.glob("Marqius.DinkToPdf.*.nupkg")):
        sys.exit("no packages in dist/: run python3 build.py VERSION first")
    for check in sys.argv[1:] or ["quickstart", "dinktopdf-example"]:
        if check not in CHECKS:
            sys.exit(__doc__)
        CHECKS[check]()
    sys.exit(1 if failed else 0)
