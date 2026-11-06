"""Build the release DLL and package AeronauticFlares as a KSP mod zip.

    python tools/package.py                  package the committed state
    python tools/package.py --allow-dirty    package the working tree, for a release candidate

Writes dist/AeronauticFlares_<version>.zip:

    GameData/AeronauticFlares/Plugins/AeronauticFlares.dll
    GameData/AeronauticFlares/Settings.cfg, AeronauticFlares.version
    GameData/AeronauticFlares/README.md, CHANGELOG.md, license.md, THIRD-PARTY-NOTICES.md, docs/how-it-works.md

Refuses to run when the versions in Source/FlareSettings.cs, Source/AssemblyInfo.cs, the .version file and
the top of CHANGELOG.md disagree, or (without --allow-dirty) tracked files have uncommitted changes.
"""
import json
import os
import re
import subprocess
import sys
import zipfile

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
NAME = "AeronauticFlares"
DIST = os.path.join(ROOT, "dist")
BUILD = os.path.join(DIST, "build")
GAMEDATA = os.path.join(ROOT, "GameData", NAME)
DOCS = ("README.md", "CHANGELOG.md", "license.md", "THIRD-PARTY-NOTICES.md", "docs/how-it-works.md")


def read(path):
    with open(path, encoding="utf-8-sig") as f:
        return f.read()


def run(*cmd):
    return subprocess.run(cmd, cwd=ROOT, capture_output=True, text=True)


def main():
    allow_dirty = "--allow-dirty" in sys.argv
    avc = json.loads(read(os.path.join(GAMEDATA, NAME + ".version")))["VERSION"]
    found = [
        re.search(r'Version = "([^"]+)"', read(os.path.join(ROOT, "Source", "FlareSettings.cs"))),
        re.search(r'AssemblyVersion\("(\d+\.\d+\.\d+)\.0"\)', read(os.path.join(ROOT, "Source", "AssemblyInfo.cs"))),
        re.search(r"^## (\S+)", read(os.path.join(ROOT, "CHANGELOG.md")), re.M),
    ]
    found = [m and m.group(1) for m in found] + ["%d.%d.%d" % (avc["MAJOR"], avc["MINOR"], avc["PATCH"])]
    if not (found[0] and len(set(found)) == 1):
        sys.exit("versions must agree: FlareSettings %s, AssemblyInfo %s, CHANGELOG %s, .version %s" % tuple(found))
    version = found[0]

    dirty = run("git", "status", "--porcelain", "--untracked-files=no").stdout.strip()
    if dirty and not allow_dirty:
        sys.exit("uncommitted changes to tracked files:\n" + dirty + "\ncommit them, or use --allow-dirty for a release candidate")

    print("building %s %s (Release)" % (NAME, version))
    result = run("dotnet", "build", os.path.join(ROOT, NAME + ".csproj"), "-c", "Release", "-nologo", "-v", "q",
                 "-p:OutputPath=" + BUILD + os.sep)
    if result.returncode != 0:
        sys.exit(result.stdout + result.stderr)
    dll = os.path.join(BUILD, NAME + ".dll")

    os.makedirs(DIST, exist_ok=True)
    out = os.path.join(DIST, "%s_%s.zip" % (NAME, version))
    base = "GameData/" + NAME + "/"
    with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED) as z:
        z.write(dll, base + "Plugins/" + NAME + ".dll")
        z.write(os.path.join(GAMEDATA, "Settings.cfg"), base + "Settings.cfg")
        z.write(os.path.join(GAMEDATA, NAME + ".version"), base + NAME + ".version")
        for doc in DOCS:
            z.write(os.path.join(ROOT, doc), base + doc)
    print("wrote " + out)


if __name__ == "__main__":
    main()
