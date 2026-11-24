#!/usr/bin/env python3
"""The nuget.org listing, checked before it is published, and shown on one page for one approval.

    python3 tests/listing.py                 the newest packages in dist/ (python3 build.py makes them)
    python3 tests/listing.py --guard REF     also refuse when code changed since REF (a listing-only release)

For each package it reads what nuget.org will show: the title, description, tags, README, badges,
pictures and licence section, and checks:
  - a title; a description of 200 to 400 characters; at least 15 tags;
  - every badge has its colour set (shields.io colours a 0.x version orange and a new package's
    downloads red);
  - every picture and link is a full https address that answers (nuget.org cannot follow relative ones);
  - no trial wording and no prices (prices live only on the plans page);
  - the licence section is the approved one, word for word.
It writes listing-preview.md: everything nuget.org will show, on one page, to approve at once.
Exit status 1 when a check fails.

With --guard REF, it also fails when anything other than the listing changed since REF: the READMEs,
docs/, samples/, and the title, description, tags and release notes in the project files. That is
what makes a listing-only release safe to publish after one Linux check.
"""
import re
import subprocess
import sys
import urllib.request
import zipfile
from pathlib import Path

HERE = Path(__file__).resolve().parent.parent
LICENCE = """## Licence

marqius-wkhtmltopdf has two licences.
- **Personal, teaching and open-source use** is free with a Community licence.
- **Anything done by or for a business** needs a commercial licence.
  [See the plans](https://marqius.com/wkhtmltopdf-buy/).
- **To use a licence,** set the environment variable `MARQIUS_LICENCE` to the licence, or to the path of a file that holds it.
"""
# what never changes the code in the packages: the listing itself, and the tests and workflows around it
LISTING_FILES = re.compile(r"^(README\.md|src/[^/]+/README\.md|docs/.*|samples/.*|tests/.*|\.github/.*|icon\.png|LICENSE\.md|\.gitignore)$")
LISTING_PROPS = re.compile(r"<(Title|Description|PackageTags|PackageReleaseNotes|Copyright)>")
failures = []


def fail(package, what):
    failures.append(f"{package}: {what}")
    print(f"FAIL\t{package}\t{what}")


def answers(url):
    try:
        req = urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0 (listing check)"}, method="GET")
        with urllib.request.urlopen(req, timeout=30) as r:
            return r.status < 400
    except Exception:
        return False


def guard(ref):
    """Only the listing changed since ref: a listing-only release may skip the code tests."""
    changed = subprocess.run(["git", "diff", "--name-only", ref, "HEAD"], cwd=HERE, capture_output=True, text=True, check=True).stdout.split()
    for path in changed:
        if LISTING_FILES.match(path):
            continue
        if path.endswith(".csproj") or path == "Directory.Build.props":
            diff = subprocess.run(["git", "diff", "-U0", ref, "HEAD", "--", path], cwd=HERE, capture_output=True, text=True, check=True).stdout
            lines = [l for l in diff.splitlines() if l[:1] in "+-" and not l.startswith(("+++", "---"))]
            if all(LISTING_PROPS.search(l) for l in lines):
                continue
        fail("release", f"{path} changed since {ref}: that is code, not the listing; run the full release")


def main():
    if "--guard" in sys.argv:
        guard(sys.argv[sys.argv.index("--guard") + 1])
    preview = ["# What nuget.org will show", ""]
    packages = sorted(p for p in (HERE / "dist").glob("Marqius.*.nupkg") if not p.name.endswith(".snupkg"))
    newest = {}
    for p in packages:
        pid = p.name.split(".0.")[0]
        newest[pid] = p  # sorted: the last one is the newest version
    if len(newest) != 2:
        sys.exit("dist/ needs both packages: python3 build.py VERSION [PACKAGE_VERSION]")
    for pid, nupkg in sorted(newest.items()):
        with zipfile.ZipFile(nupkg) as z:
            nuspec = z.read(f"{pid}.nuspec").decode("utf-8")
            readme = z.read("README.md").decode("utf-8")
        meta = {t: (re.search(rf"<{t}[^>]*>(.*?)</{t}>", nuspec, re.S) or [None, ""])[1].strip() for t in ("version", "title", "description", "tags", "releaseNotes")}
        tags = meta["tags"].split()
        if not meta["title"]:
            fail(pid, "no title")
        if not 200 <= len(meta["description"]) <= 400:
            fail(pid, f"the description is {len(meta['description'])} characters, not 200 to 400")
        if len(tags) < 15:
            fail(pid, f"{len(tags)} tags, fewer than 15")
        for url in re.findall(r"https://img\.shields\.io/[^\s)\"]+", readme):
            if "color=" not in url and "/badge/" not in url:
                fail(pid, f"badge without its colour set (shields.io picks orange or red): {url}")
        images = re.findall(r"!\[[^\]]*\]\(([^)\s]+)\)", readme)
        links = re.findall(r"(?<!!)\[[^\]]*\]\(([^)\s]+)\)", readme)
        for url in images + links:
            if not url.startswith("https://"):
                if not url.startswith("#"):
                    fail(pid, f"not a full https address (nuget.org cannot follow it): {url}")
                continue
            if not answers(url):
                fail(pid, f"does not answer: {url}")
        text = re.sub(r"```.*?```", "", readme, flags=re.S)
        if re.search(r"\btrial\b", text, re.I):
            fail(pid, "trial wording (the package pages have none)")
        if re.search(r"(\$|USD|€|£)\s?\d", text):
            fail(pid, "a price (prices are only on the plans page)")
        if LICENCE not in readme:
            fail(pid, "the licence section is not the approved one, word for word")
        preview += [f"## {pid} {meta['version']}", "",
                    f"**Title:** {meta['title']}", "",
                    f"**Description ({len(meta['description'])} characters):** {meta['description']}", "",
                    f"**Tags ({len(tags)}):** {', '.join(tags)}", "",
                    f"**Release notes:** {meta['releaseNotes']}", "",
                    f"**Badges:** {len(re.findall(r'img.shields.io', readme))}, **pictures:** {sum('raw.githubusercontent' in u for u in images)}, **links:** {len(links)}, all answering", "",
                    "**The README nuget.org shows:**", "", "---", "", readme.strip(), "", "---", ""]
    (HERE / "listing-preview.md").write_text("\n".join(preview) + "\n", encoding="utf-8")
    print(f"{'FAIL' if failures else 'pass'}\tlisting\t{len(failures)} problems; the preview is in listing-preview.md")
    sys.exit(1 if failures else 0)


if __name__ == "__main__":
    main()
