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
  publish            dotnet publish -r <this system>, self-contained and single-file, run from elsewhere
  rotativa           Rotativa.AspNetCore 1.5.0 with the program README's two lines; ViewAsPdf
  cache              a cache folder that cannot be written (IIS): the index goes to the temporary folder
  manual             every example of docs/manual.md, as printed: the first PDF, a template with its
                     style sheet and logo (in a folder whose name has a space), settings, a web page,
                     three parts in one PDF, an MVC controller, languages, errors, the program itself,
                     the licence line; the website and Docker chapters must match the README's tested
                     ones
  old-build          code built against DinkToPdf 1.0.8 (tests/old-build/Reports.cs, in a library that
                     references DinkToPdf 1.0.8) runs in an app with Marqius.DinkToPdf: the build picks
                     Marqius.DinkToPdf's DLL, and the library's code works on it with no libwkhtmltox;
                     then the same code compiles against Marqius.DinkToPdf with warnings as errors
"""
import atexit
import os
import re
import shutil
import socket
import subprocess
import sys
import tempfile
import time
import urllib.error
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


WORKSPACES = []


@atexit.register
def _clean():
    # each workspace has a NuGet cache of its own (hundreds of MB): gone when the checks end, unless
    # CUSTOMER_KEEP is set to look at them
    if not os.environ.get("CUSTOMER_KEEP"):
        for root in WORKSPACES:
            shutil.rmtree(root, ignore_errors=True)


def workspace():
    root = Path(tempfile.mkdtemp(prefix="marqius-customer-"))
    WORKSPACES.append(root)
    env = dict(os.environ, DOTNET_CLI_TELEMETRY_OPTOUT="1", DOTNET_NOLOGO="1", NUGET_PACKAGES=str(root / "nuget-cache"))
    # CUSTOMER_FEED=nuget.org: the packages as nuget.org serves them, after they are published
    local = "" if os.environ.get("CUSTOMER_FEED") == "nuget.org" else f'    <add key="these packages" value="{DIST}" />\n'
    (root / "nuget.config").write_text(f"""<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
{local}    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
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


def website(root, env):
    """The README's website: dotnet new web -o InvoiceSite, the package, its Program.cs as printed."""
    run(["dotnet", "new", "web", "-f", "net8.0", "-o", "InvoiceSite"], root, env)
    app = root / "InvoiceSite"
    run(["dotnet", "add", "package", "Marqius.DinkToPdf"], app, env)
    (app / "Program.cs").write_text(example("README.md", "aspnet Program.cs"), encoding="utf-8")
    return app


def settings():
    """The README's "Common settings" Program.cs: landscape A4 with a header, footer and title."""
    name = "the README's common settings, as printed: A4 turned, header, footer, title"
    try:
        root, env = workspace()
        run(["dotnet", "new", "console", "-o", "Settings"], root, env)
        app = root / "Settings"
        run(["dotnet", "add", "package", "Marqius.DinkToPdf"], app, env)
        (app / "Program.cs").write_text(example("README.md", "settings Program.cs"), encoding="utf-8")
        out = run(["dotnet", "run"], app, env)
        pdf = app / "invoice.pdf"
        w, h = page_size(pdf) if is_pdf(pdf) else (0, 0)
        text = pdf.read_bytes().decode("latin-1") if pdf.exists() else ""
        ok = abs(w - 841.9) < 1 and abs(h - 595.3) < 1 and "invoice.pdf is ready" in out.stdout
        report(ok, name, f"{w:.1f} x {h:.1f} pt" if ok else f"page {w:.1f} x {h:.1f}; {out.stdout[-300:]}")
    except Exception as e:  # noqa: BLE001
        report(False, name, e)


def aspnet():
    name = "ASP.NET Core: the README's website, as printed, asked for /invoice"
    try:
        root, env = workspace()
        app = website(root, env)
        run(["dotnet", "publish", "-c", "Release", "-o", str(app / "publish")], app, env)
        port = free_port()
        server = subprocess.Popen(["dotnet", "InvoiceSite.dll", "--urls", f"http://127.0.0.1:{port}"], cwd=app / "publish", env=env,
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


def this_rid():
    import platform
    machine = platform.machine().lower()
    arm = machine in ("arm64", "aarch64")
    if os.name == "nt":
        return "win-x64"
    if sys.platform == "darwin":
        return "osx-arm64"
    return "linux-arm64" if arm else "linux-x64"


def publish():
    """dotnet publish for this system: self-contained, and single-file; the app runs from its folder."""
    rid = this_rid()
    root, env = workspace()
    run(["dotnet", "new", "console", "-f", "net8.0", "-o", "Invoice"], root, env)
    app = root / "Invoice"
    run(["dotnet", "add", "package", "Marqius.DinkToPdf"], app, env)
    (app / "Program.cs").write_text(example("README.md", "quickstart Program.cs"), encoding="utf-8")
    for mode, extra in (("self-contained", ["--self-contained"]), ("single-file", ["--self-contained", "-p:PublishSingleFile=true"])):
        name = f"dotnet publish -r {rid} ({mode}): the published app makes a PDF"
        try:
            out = app / f"publish-{mode}"
            run(["dotnet", "publish", "-c", "Release", "-r", rid, *extra, "-o", str(out)], app, env)
            others = [d.name for d in (out / "runtimes").iterdir()] if (out / "runtimes").exists() else []
            elsewhere = Path(tempfile.mkdtemp(prefix="marqius-run-"))
            exe = out / ("Invoice.exe" if os.name == "nt" else "Invoice")
            run([str(exe)], elsewhere, env)
            ok = is_pdf(elsewhere / "invoice.pdf") and not others
            report(ok, name, f"invoice.pdf; the program beside the app, no other system's ({sum(1 for _ in out.iterdir())} files)" if ok
                   else f"pdf={is_pdf(elsewhere / 'invoice.pdf')}, other systems' folders: {others}")
        except Exception as e:  # noqa: BLE001
            report(False, name, e)


def rotativa():
    """Rotativa.AspNetCore 1.5.0 with the README's two lines: ViewAsPdf makes the PDF."""
    name = "Rotativa.AspNetCore: the README's Program.cs and HomeController.cs, then /Home/Invoice"
    try:
        root, env = workspace()
        run(["dotnet", "new", "mvc", "-f", "net8.0", "-o", "Shop"], root, env)
        app = root / "Shop"
        run(["dotnet", "add", "package", "Rotativa.AspNetCore", "--version", "1.5.0"], app, env)
        run(["dotnet", "add", "package", "Marqius.Wkhtmltopdf"], app, env)
        # the README's two files, as printed
        (app / "Program.cs").write_text(example("src/Marqius.Wkhtmltopdf/README.md", "rotativa Program.cs"), encoding="utf-8")
        (app / "Controllers" / "HomeController.cs").write_text(example("src/Marqius.Wkhtmltopdf/README.md", "rotativa HomeController.cs"), encoding="utf-8")
        run(["dotnet", "publish", "-c", "Release", "-o", str(app / "publish")], app, env)
        port = free_port()
        server = subprocess.Popen(["dotnet", "Shop.dll", "--urls", f"http://127.0.0.1:{port}"], cwd=app / "publish", env=env,
                                  stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
        try:
            body, kind = b"", ""
            for _ in range(60):
                try:
                    with urllib.request.urlopen(f"http://127.0.0.1:{port}/Home/Invoice", timeout=60) as r:
                        body, kind = r.read(), r.headers.get("Content-Type")
                    break
                except urllib.error.HTTPError as e:
                    body, kind = e.read(), f"HTTP {e.code}"
                    break
                except OSError:
                    time.sleep(1)
            ok = body[:5] == b"%PDF-"
            names = sorted(p.name for p in (app / "publish" / "wwwroot" / "Rotativa").iterdir()) if (app / "publish" / "wwwroot" / "Rotativa").exists() else []
            report(ok, name, f"{len(body)} bytes, {kind}; wwwroot/Rotativa holds {', '.join(names)}" if ok else f"{kind}: {body[:300]!r}")
        finally:
            server.kill()
    except Exception as e:  # noqa: BLE001
        report(False, name, e)


def cache():
    """A server whose usual cache folder cannot be written (an IIS pool's identity, a read-only home):
    the package gives the program a cache in the temporary folder, so the second PDF does not read
    every font again."""
    name = "a cache folder that cannot be written: the font index goes to the temporary folder"
    try:
        root, env = workspace()
        run(["dotnet", "new", "console", "-f", "net8.0", "-o", "Cache"], root, env)
        app = root / "Cache"
        run(["dotnet", "add", "package", "Marqius.DinkToPdf"], app, env)
        (app / "Program.cs").write_text("""using System.Diagnostics;
using DinkToPdf;

Console.WriteLine("cache: " + (Marqius.Wkhtmltopdf.Wkhtmltopdf.CacheFolder ?? "the program's own"));
var converter = new SynchronizedConverter(new PdfTools());
for (int i = 1; i <= 2; i++)
{
    var clock = Stopwatch.StartNew();
    var pdf = converter.Convert(new HtmlToPdfDocument { Objects = { new ObjectSettings { HtmlContent = "<p>Cache " + i + "</p>" } } });
    Console.WriteLine($"pdf {i}: {pdf.Length} bytes, {clock.ElapsedMilliseconds} ms");
}
""", encoding="utf-8")
        run(["dotnet", "build", "-c", "Release"], app, env)
        locked = root / "locked"
        locked.mkdir()
        env2 = dict(env, MARQIUS_CACHE="")
        env2.pop("MARQIUS_CACHE")
        if os.name == "nt":
            # a LOCALAPPDATA that names a file, not a folder: nothing can be made under it
            (locked / "not-a-folder").write_text("")
            env2["LOCALAPPDATA"] = str(locked / "not-a-folder")
        else:
            os.chmod(locked, 0o500)
            env2["HOME"] = str(locked)
            env2.pop("XDG_CACHE_HOME", None)
        env2["DOTNET_CLI_HOME"] = str(root / "dotnet-home")  # the dotnet command's own folder, apart from HOME
        out = run(["dotnet", str(app / "bin" / "Release" / "net8.0" / "Cache.dll")], app, env2)
        lines = out.stdout.strip().splitlines()
        temp = tempfile.gettempdir()
        ok = (len(lines) == 3 and "marqius-cache-" in lines[0] and all(" 0 bytes" not in l for l in lines[1:]))
        report(ok, name, "; ".join(lines) if ok else out.stdout + out.stderr)
        if os.name != "nt":
            os.chmod(locked, 0o700)
    except Exception as e:  # noqa: BLE001
        report(False, name, e)


def page_size(path):
    """The first page's MediaBox width and height, in points."""
    data = Path(path).read_bytes().decode("latin-1")
    m = re.search(r"/MediaBox\s*\[\s*([-\d.]+)\s+([-\d.]+)\s+([-\d.]+)\s+([-\d.]+)\s*\]", data)
    return (float(m.group(3)) - float(m.group(1)), float(m.group(4)) - float(m.group(2))) if m else (0, 0)


def aot():
    """A trimmed app, and a Native AOT app: the settings DinkToPdf reads by reflection (paper size,
    orientation, margins) still reach the PDF."""
    rid = this_rid()
    root, env = workspace()
    run(["dotnet", "new", "console", "-f", "net8.0", "-o", "Small"], root, env)
    app = root / "Small"
    run(["dotnet", "add", "package", "Marqius.DinkToPdf"], app, env)
    (app / "Program.cs").write_text("""using DinkToPdf;

var converter = new SynchronizedConverter(new PdfTools());
var pdf = converter.Convert(new HtmlToPdfDocument
{
    GlobalSettings = { PaperSize = PaperKind.A5, Orientation = Orientation.Landscape, Margins = new MarginSettings(10, 10, 10, 10) },
    Objects = { new ObjectSettings { HtmlContent = "<h1>A5, turned</h1>" } },
});
File.WriteAllBytes("invoice.pdf", pdf);
""", encoding="utf-8")
    for mode, extra in (("trimmed", ["--self-contained", "-p:PublishTrimmed=true"]), ("Native AOT", ["-p:PublishAot=true"])):
        name = f"dotnet publish -r {rid} ({mode}): paper size, orientation and margins still reach the PDF"
        try:
            out = app / ("publish-" + mode.replace(" ", "-").lower())
            p = run(["dotnet", "publish", "-c", "Release", "-r", rid, *extra, "-o", str(out)], app, env)
            warnings = sorted(set(re.findall(r"warning (IL\d+)", p.stdout + p.stderr)))
            elsewhere = Path(tempfile.mkdtemp(prefix="marqius-run-"))
            exe = out / ("Small.exe" if os.name == "nt" else "Small")
            run([str(exe)], elsewhere, env)
            w, h = page_size(elsewhere / "invoice.pdf") if is_pdf(elsewhere / "invoice.pdf") else (0, 0)
            ok = abs(w - 595.3) < 1 and abs(h - 419.5) < 1 and not warnings
            report(ok, name, f"A5 landscape, {w:.1f} x {h:.1f} pt, no trimming warnings" if ok else f"page {w:.1f} x {h:.1f} pt; warnings {warnings}")
        except Exception as e:  # noqa: BLE001
            report(False, name, e)


def old_build():
    """A library built against DinkToPdf 1.0.8, in an app that adds Marqius.DinkToPdf (also what happens
    when another package brings DinkToPdf in): the app runs the library's compiled code on ours."""
    name = "code built against DinkToPdf 1.0.8 runs unchanged on Marqius.DinkToPdf"
    try:
        root, env = workspace()
        old = HERE / "tests" / "old-build"
        run(["dotnet", "new", "classlib", "-f", "netstandard2.0", "-o", "Reports"], root, env)
        lib = root / "Reports"
        (lib / "Class1.cs").unlink()
        shutil.copy(old / "Reports.cs", lib / "Reports.cs")
        project = (lib / "Reports.csproj").read_text(encoding="utf-8")
        (lib / "Reports.csproj").write_text(project.replace("</PropertyGroup>", "  <AllowUnsafeBlocks>true</AllowUnsafeBlocks>\n  </PropertyGroup>", 1), encoding="utf-8")
        run(["dotnet", "add", "package", "DinkToPdf", "--version", "1.0.8"], lib, env)
        run(["dotnet", "new", "console", "-f", "net8.0", "-o", "App"], root, env)
        app = root / "App"
        shutil.copy(old / "Program.cs", app / "Program.cs")
        run(["dotnet", "add", "reference", str(lib / "Reports.csproj")], app, env)
        run(["dotnet", "add", "package", "Marqius.DinkToPdf"], app, env)
        out = run(["dotnet", "run"], app, env)
        text = out.stdout
        natives = [p.name for p in app.rglob("*wkhtmltox*")]
        ok = (is_pdf(app / "invoice.pdf") and "assembly: DinkToPdf 1.1.0.0" in text and "raw: 0.12.6, A5, 48" in text
              and "thread: True" in text and "finished: True" in text and "phase 6 of 6: Printing pages" in text
              and "processing: True" in text and "library: 0.12.6, extended Qt: True" in text and not natives)
        detail = "; ".join(l for l in text.splitlines() if l.startswith(("assembly", "raw", "finished", "library")))
        report(ok, name, detail if ok else f"{text}{out.stderr}; libwkhtmltox files: {natives}")
    except Exception as e:  # noqa: BLE001
        report(False, name, e)

    name = "the same code compiles against Marqius.DinkToPdf, every warning an error"
    try:
        root, env = workspace()
        run(["dotnet", "new", "classlib", "-f", "net8.0", "-o", "Reports"], root, env)
        lib = root / "Reports"
        (lib / "Class1.cs").unlink()
        shutil.copy(HERE / "tests" / "old-build" / "Reports.cs", lib / "Reports.cs")
        project = (lib / "Reports.csproj").read_text(encoding="utf-8")
        (lib / "Reports.csproj").write_text(project.replace(
            "</PropertyGroup>", "  <AllowUnsafeBlocks>true</AllowUnsafeBlocks>\n    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>\n    <Nullable>disable</Nullable>\n  </PropertyGroup>", 1), encoding="utf-8")
        run(["dotnet", "add", "package", "Marqius.DinkToPdf"], lib, env)
        out = run(["dotnet", "build", "-c", "Release"], lib, env)
        report(True, name, "built, no warnings")
    except Exception as e:  # noqa: BLE001
        report(False, name, e)


MANUAL = "docs/manual.md"


def console_app(root, env, name, package="Marqius.DinkToPdf"):
    run(["dotnet", "new", "console", "-f", "net8.0", "-o", name], root, env)
    app = root / name
    run(["dotnet", "add", "package", package], app, env)
    return app


def pdf_pages(path):
    data = Path(path).read_bytes() if Path(path).exists() else b""
    return len(re.findall(rb"/Type\s*/Page(?![a-zA-Z])", data))


def pdf_text(path):
    """The PDF's text, when poppler's pdftotext is on this computer (else None)."""
    tool = shutil.which("pdftotext")
    if not tool:
        return None
    return subprocess.run([tool, "-layout", str(path), "-"], capture_output=True, text=True).stdout


def serve(app, dll, env, path):
    """dotnet publish, run the site on a free port, GET path: (body, content type)."""
    run(["dotnet", "publish", "-c", "Release", "-o", str(app / "publish")], app, env)
    port = free_port()
    server = subprocess.Popen(["dotnet", dll, "--urls", f"http://127.0.0.1:{port}"], cwd=app / "publish", env=env,
                              stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
    try:
        for _ in range(60):
            try:
                with urllib.request.urlopen(f"http://127.0.0.1:{port}{path}", timeout=60) as r:
                    return r.read(), r.headers.get("Content-Type")
            except urllib.error.HTTPError as e:
                return e.read(), f"HTTP {e.code}"
            except OSError:
                time.sleep(1)
        return b"", "no answer"
    finally:
        server.kill()


def manual():
    """Every example of the manual, as printed."""
    def check(name, body):
        try:
            ok, detail = body()
            report(ok, f"manual: {name}", detail)
        except Exception as e:  # noqa: BLE001
            report(False, f"manual: {name}", e)

    def first():
        root, env = workspace()
        app = console_app(root, env, "Invoice")
        (app / "Program.cs").write_text(example(MANUAL, "manual first Program.cs"), encoding="utf-8")
        out = run(["dotnet", "run"], app, env)
        return is_pdf(app / "invoice.pdf") and "invoice.pdf is ready" in out.stdout, out.stdout.strip()

    def template():
        root, env = workspace()
        spaced = root / "My Apps"  # a folder name with a space, as many people have
        spaced.mkdir()
        app = console_app(spaced, env, "InvoiceApp")
        (app / "templates").mkdir()
        for f in ("invoice.html", "style.css", "logo.svg"):
            (app / "templates" / f).write_text(example(MANUAL, f"manual template {f}"), encoding="utf-8")
        project = app / "InvoiceApp.csproj"
        text = project.read_text(encoding="utf-8")
        assert text.rstrip().endswith("</Project>")
        cut = text.rstrip().rfind("</Project>")
        project.write_text(text[:cut] + example(MANUAL, "manual template csproj") + "\n" + text[cut:], encoding="utf-8")
        code = example(MANUAL, "manual template Program.cs")
        line = "var converter = new SynchronizedConverter(new PdfTools());"
        assert code.count(line) == 1
        # the test listens for warnings too: a style sheet or picture not found would be one
        code = code.replace(line, line + '\nconverter.Warning += (s, e) => Console.WriteLine("WARNING " + e.Message);')
        (app / "Program.cs").write_text(code, encoding="utf-8")
        out = run(["dotnet", "run"], app, env)
        text = pdf_text(app / "invoice.pdf")
        # a style sheet or picture that did not load is a fault; a font the machine lacks (Arial on
        # Linux, say) is only worth knowing, and the PDF is set in another
        missing = [l for l in out.stdout.splitlines() if l.startswith("WARNING") and "could not be" in l]
        ok = is_pdf(app / "invoice.pdf") and not missing and (text is None or ("Northwind" in text and "Ink cartridge" in text and "24.49" in text.replace(",", ".")))
        return ok, "logo, style sheet and data in the PDF; nothing failed to load" if ok else out.stdout + (text or "")

    def settings_():
        root, env = workspace()
        app = console_app(root, env, "Settings")
        (app / "Program.cs").write_text(example(MANUAL, "manual settings Program.cs"), encoding="utf-8")
        run(["dotnet", "run"], app, env)
        w, h = page_size(app / "invoice.pdf") if is_pdf(app / "invoice.pdf") else (0, 0)
        text = pdf_text(app / "invoice.pdf")
        ok = abs(w - 841.9) < 1 and abs(h - 595.3) < 1 and (text is None or "Page 1 of 1" in text)
        return ok, f"A4 landscape {w:.1f} x {h:.1f} pt, \"Page 1 of 1\" in the header"

    def web():
        root, env = workspace()
        app = console_app(root, env, "Web")
        (app / "Program.cs").write_text(example(MANUAL, "manual web Program.cs"), encoding="utf-8")
        out = run(["dotnet", "run"], app, env)
        return is_pdf(app / "page.pdf"), out.stdout.strip()

    def parts():
        root, env = workspace()
        app = console_app(root, env, "Report")
        (app / "Program.cs").write_text(example(MANUAL, "manual parts Program.cs"), encoding="utf-8")
        run(["dotnet", "run"], app, env)
        pages = pdf_pages(app / "report.pdf")
        text = pdf_text(app / "report.pdf")
        ok = pages == 3 and (text is None or ("Page 2 of 3" in text and "Page 3 of 3" in text))
        return ok, f"{pages} pages, numbered 2 of 3 and 3 of 3"

    def same_as_readme(label, readme_label, readme="README.md"):
        same = example(MANUAL, label) == example(readme, readme_label)
        return same, "the same as the README's, which the aspnet and docker checks run" if same else "differs from the README's tested example"

    def mvc():
        root, env = workspace()
        run(["dotnet", "new", "mvc", "-f", "net8.0", "-o", "Shop"], root, env)
        app = root / "Shop"
        run(["dotnet", "add", "package", "Marqius.DinkToPdf"], app, env)
        program = (app / "Program.cs").read_text(encoding="utf-8")
        anchor = "builder.Services.AddControllersWithViews();\n"
        assert program.count(anchor) == 1
        (app / "Program.cs").write_text(program.replace(anchor, anchor + example(MANUAL, "manual mvc Program.cs")), encoding="utf-8")
        (app / "Controllers" / "InvoiceController.cs").write_text(example(MANUAL, "manual mvc InvoiceController.cs"), encoding="utf-8")
        body, kind = serve(app, "Shop.dll", env, "/Invoice/Download/42")
        return body[:5] == b"%PDF-", f"/Invoice/Download/42: {len(body)} bytes, {kind}"

    def languages():
        root, env = workspace()
        app = console_app(root, env, "Languages")
        (app / "Program.cs").write_text(example(MANUAL, "manual languages Program.cs"), encoding="utf-8")
        out = run(["dotnet", "run"], app, env)
        warned = [l for l in out.stdout.splitlines() if l.startswith("Warning:")]
        return is_pdf(app / "thanks.pdf"), f"thanks.pdf made; {len(warned)} font warnings on this machine"

    def errors():
        root, env = workspace()
        app = console_app(root, env, "Errors")
        (app / "Program.cs").write_text(example(MANUAL, "manual errors Program.cs"), encoding="utf-8")
        out = run(["dotnet", "run"], app, env)
        ok = is_pdf(app / "report.pdf") and "report.pdf is ready" in out.stdout and any(l.startswith("Warning:") and "chart.png" in l for l in out.stdout.splitlines())
        return ok, "a warning names chart.png, and the PDF is made" if ok else out.stdout

    def program():
        root, env = workspace()
        app = console_app(root, env, "Statement", "Marqius.Wkhtmltopdf")
        (app / "Program.cs").write_text(example(MANUAL, "manual program Program.cs"), encoding="utf-8")
        out = run(["dotnet", "run"], app, env)
        return is_pdf(app / "statement.pdf") and "statement.pdf is ready" in out.stdout, out.stdout.strip()

    def licence():
        # the line goes into the website's Program.cs, before the first PDF: it must build
        root, env = workspace()
        app = website(root, env)
        program = (app / "Program.cs").read_text(encoding="utf-8")
        anchor = "var builder = WebApplication.CreateBuilder(args);\n"
        assert program.count(anchor) == 1
        (app / "Program.cs").write_text(program.replace(anchor, anchor + example(MANUAL, "manual licence Program.cs")), encoding="utf-8")
        run(["dotnet", "build", "-c", "Release"], app, env)
        return True, "builds in the website's Program.cs"

    check("1-2 the first PDF", first)
    check("3 a template, its style sheet and logo, filled with data", template)
    check("4 paper, margins, headers and footers", settings_)
    check("5 a web page", web)
    check("6 three parts in one PDF", parts)
    check("7 the minimal API", lambda: same_as_readme("manual aspnet Program.cs", "aspnet Program.cs"))
    check("7 an MVC controller", mvc)
    check("8 other languages", languages)
    check("9 the Dockerfile", lambda: same_as_readme("manual docker Dockerfile", "docker Dockerfile"))
    check("10 errors and warnings", errors)
    check("12 the program itself", program)
    check("13 the licence line", licence)


CHECKS = {"settings": settings, "quickstart": quickstart, "dinktopdf-example": dinktopdf_example, "aspnet": aspnet, "framework": framework,
          "publish": publish, "rotativa": rotativa, "cache": cache, "aot": aot, "old-build": old_build, "manual": manual}

if __name__ == "__main__":
    if os.environ.get("CUSTOMER_FEED") != "nuget.org" and not any(DIST.glob("Marqius.DinkToPdf.*.nupkg")):
        sys.exit("no packages in dist/: run python3 build.py VERSION first")
    for check in sys.argv[1:] or ["quickstart", "dinktopdf-example"]:
        if check not in CHECKS:
            sys.exit(__doc__)
        CHECKS[check]()
    sys.exit(1 if failed else 0)
