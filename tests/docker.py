#!/usr/bin/env python3
"""The README's Dockerfile with an ASP.NET Core app, on Debian and on Alpine (Linux with Docker).

    python3 tests/docker.py

The app is the README's ASP.NET Core lines (tests/customer.py makes it the same way), published as
dotnet publish makes it. The Dockerfile is the README's, as printed, and its Alpine form with the line
the README gives for Alpine. Each image runs as the .NET images run apps (the user "app", not root),
and /invoice must answer with a PDF. A third image leaves the fonts line out, and must say how to
install them instead of answering with a PDF. Prints pass or FAIL lines as customer.py does.
"""
import subprocess
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import customer  # noqa: E402  (the same workspace, examples and reports)


def docker(*args, check=True, cwd=None):
    p = subprocess.run(["docker", *args], capture_output=True, text=True, cwd=cwd, timeout=900)
    if check and p.returncode != 0:
        raise RuntimeError(f"docker {' '.join(args)} -> {p.returncode}: {(p.stdout + p.stderr)[-1500:]}")
    return p


def ask(port, seconds=90):
    """(status, bytes) of GET /invoice, once the app answers."""
    end = time.time() + seconds
    while time.time() < end:
        try:
            with urllib.request.urlopen(f"http://127.0.0.1:{port}/invoice", timeout=60) as r:
                return r.status, r.read()
        except urllib.error.HTTPError as e:
            return e.code, e.read()
        except OSError:
            time.sleep(1)
    return 0, b""


def main():
    root, env = customer.workspace()
    customer.run(["dotnet", "new", "web", "-f", "net8.0", "-o", "MyApp"], root, env)
    app = root / "MyApp"
    customer.run(["dotnet", "add", "package", "Marqius.DinkToPdf"], app, env)
    register = customer.example("README.md", "aspnet register")
    usings = "".join(l + "\n" for l in register.splitlines() if l.startswith("using "))
    register = "".join(l + "\n" for l in register.splitlines() if not l.startswith("using ") and l.strip())
    (app / "Program.cs").write_text(usings + "var builder = WebApplication.CreateBuilder(args);\n" + register
                                    + "var app = builder.Build();\n" + customer.example("README.md", "aspnet endpoint")
                                    + "app.Run();\n", encoding="utf-8")
    customer.run(["dotnet", "publish", "-c", "Release", "-o", str(app / "publish")], app, env)

    printed = customer.example("README.md", "docker Dockerfile")
    alpine = (printed.replace("FROM mcr.microsoft.com/dotnet/aspnet:8.0", "FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine")
              .replace("RUN apt-get update && apt-get install -y --no-install-recommends fonts-liberation && rm -rf /var/lib/apt/lists/*",
                       "RUN apk add --no-cache font-liberation"))
    bare = "".join(l + "\n" for l in printed.splitlines() if not l.startswith("RUN "))
    assert alpine != printed and "apk add" in alpine

    for tag, dockerfile, wants_pdf in (("debian", printed, True), ("alpine", alpine, True), ("no-fonts", bare, False)):
        name = (f"the README's Dockerfile ({tag})" if wants_pdf else "the Dockerfile without its fonts line") + ": GET /invoice"
        image = f"marqius-dinktopdf-test-{tag}"
        try:
            (app / "Dockerfile").write_text(dockerfile, encoding="utf-8")
            docker("build", "-q", "-t", image, ".", cwd=app)
            port = customer.free_port()
            container = docker("run", "-d", "-p", f"127.0.0.1:{port}:8080", image).stdout.strip()
            try:
                status, body = ask(port)
                logs = docker("logs", container, check=False)
                said = (logs.stdout + logs.stderr)[-600:]
                if wants_pdf:
                    ok = status == 200 and body[:5] == b"%PDF-"
                    customer.report(ok, name, f"{len(body)} bytes of PDF, as the user app" if ok else f"status {status}; {said}")
                else:
                    ok = body[:5] != b"%PDF-" and "fonts" in said
                    customer.report(ok, name, "no PDF, and the log says how to install fonts" if ok else f"status {status}; {said}")
            finally:
                docker("rm", "-f", container, check=False)
        except Exception as e:  # noqa: BLE001
            customer.report(False, name, e)
    sys.exit(1 if customer.failed else 0)


if __name__ == "__main__":
    main()
