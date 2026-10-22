#!/usr/bin/env python3
"""Publish the WebGL release build (Builds/WebGL/Tileworlds) to ocebi.github.io/tileworlds on GitHub Pages.

    python3 Tools/deploy_pages.py [--repo ~/Projects/ocebi.github.io] [--no-push]

The page stays on GitHub Pages but loads the game files through jsDelivr, pinned to the commit that holds them, because
Pages is slow on some networks. That takes two commits in the Pages repo: the game files, then the page pointing at that
commit. jsDelivr serves files of up to 20 MB from GitHub, so a bigger file stops the deploy. A file jsDelivr doesn't
send loads from the page's own copy on Pages (mirrorUrl in the template). The old /tessera/ page becomes a redirect to
/tileworlds/; /tessera/levels stays where installed games read it. See docs/WEB-BUILD.md.
"""
import argparse
import os
import re
import subprocess
import sys
import urllib.request

SITE_FOLDER = "tileworlds"
OLD_SITE_FOLDER = "tessera"
REDIRECT_PAGE = """<!doctype html>
<meta charset="utf-8">
<title>Tileworlds</title>
<meta http-equiv="refresh" content="0; url=/tileworlds/">
<link rel="canonical" href="/tileworlds/">
<script>location.replace("/tileworlds/" + location.search + location.hash);</script>
<a href="/tileworlds/">Tileworlds has moved here.</a>
"""
MAX_FILE_BYTES = 20 * 1024 * 1024
BUILD_URL_LINE = 'const buildUrl = "Build";'
MIRROR_URL_LINE = 'const mirrorUrl = "Build";'
CONTENT_URL_LINE = 'const contentUrl = "Build/content";'
CATALOG = "StreamingAssets/aa/catalog.bin"
BUNDLE_FOLDERS = ["Build/content/WebGL", "StreamingAssets/aa/WebGL"]
BUNDLE_NAME = re.compile(rb"[A-Za-z0-9_.]+_[0-9a-f]{32}\.bundle")


def git(repo, *args):
    return subprocess.run(["git", "-C", repo, *args], check=True, capture_output=True, text=True).stdout.strip()


def commit_if_changed(repo, paths, message):
    git(repo, "add", "--", *paths)
    if subprocess.run(["git", "-C", repo, "diff", "--cached", "--quiet"]).returncode != 0:
        git(repo, "commit", "-q", "-m", message)
        print(f"Committed: {message}")


def build_paths(folder):
    """Every file under Build/, content bundles included, as paths."""
    return sorted(os.path.join(root, name) for root, _, names in os.walk(folder) for name in names if name != ".DS_Store")


def previous_bundles(site, build):
    """Bundles the deployed build's catalog names that the new build doesn't have, as rsync paths under the site.

    The catalog is served from Pages, unpinned and cached for 10 minutes, so for a while after a deploy a browser can pair
    the new build with the old catalog. Keeping the bundles it names (their names carry content hashes, so old and new sit
    side by side) lets that browser load instead of hanging on a 404. Bundles older than that are pruned.
    """
    catalog = os.path.join(site, CATALOG)
    if not os.path.isfile(catalog):
        return []
    names = set(m.decode() for m in BUNDLE_NAME.findall(open(catalog, "rb").read()))
    kept = []
    for folder in BUNDLE_FOLDERS:
        deployed = os.path.join(site, folder)
        if not os.path.isdir(deployed):
            continue
        for name in sorted(os.listdir(deployed)):
            if name in names and not os.path.exists(os.path.join(build, folder, name)):
                kept.append(f"/{folder}/{name}")
    return kept


def github_slug(repo):
    url = git(repo, "remote", "get-url", "origin")
    match = re.search(r"github\.com[:/](.+?)(?:\.git)?$", url)
    if not match:
        sys.exit(f"Origin {url} isn't on GitHub.")
    return match.group(1)


def main():
    root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--repo", default=os.path.expanduser("~/Projects/ocebi.github.io"))
    parser.add_argument("--build", default=os.path.join(root, "Builds", "WebGL", "Tileworlds"))
    parser.add_argument("--no-push", action="store_true", help="commit, but don't push or warm the CDN")
    args = parser.parse_args()

    build_files = os.path.join(args.build, "Build")
    if not os.path.isfile(os.path.join(args.build, "index.html")) or not os.path.isdir(build_files):
        sys.exit(f"No build in {args.build}. Run Tileworks > Build > All Games > WebGL (release) first.")
    too_big = [os.path.relpath(path, build_files) for path in build_paths(build_files) if os.path.getsize(path) > MAX_FILE_BYTES]
    if too_big:
        sys.exit(f"Over jsDelivr's 20 MB limit: {', '.join(too_big)}")
    if not os.path.isdir(os.path.join(build_files, "content")):
        sys.exit("The build has no Build/content folder: the content build didn't run, so the games would have nothing to load.")
    page = open(os.path.join(args.build, "index.html"), encoding="utf-8").read()
    if BUILD_URL_LINE not in page or MIRROR_URL_LINE not in page or CONTENT_URL_LINE not in page:
        sys.exit("index.html has no buildUrl, mirrorUrl or contentUrl line, so it can't load from the CDN with a fallback.")
    if git(args.repo, "diff", "--cached", "--name-only"):
        sys.exit(f"{args.repo} has staged changes; commit or unstage them first.")

    site = os.path.join(args.repo, SITE_FOLDER)
    kept = previous_bundles(site, args.build)
    if kept:
        print(f"Keeping {len(kept)} bundle(s) of the previous build for one deploy.")
    # levels/ is published separately by publish_levels.py.
    excludes = [arg for path in ["/levels/", *kept] for arg in ("--exclude", path)]
    subprocess.run(["rsync", "-a", "--delete", "--exclude", ".DS_Store", *excludes, args.build + "/", site + "/"], check=True)

    # 1. The game files. The page is committed separately, after it points at this commit.
    commit_if_changed(args.repo, [f"{SITE_FOLDER}/Build", f"{SITE_FOLDER}/TemplateData"], "Update Tileworlds game files")
    files_commit = git(args.repo, "log", "-1", "--format=%H", "--", f"{SITE_FOLDER}/Build")

    # 2. The page, loading those files from jsDelivr.
    cdn = f"https://cdn.jsdelivr.net/gh/{github_slug(args.repo)}@{files_commit}/{SITE_FOLDER}/Build"
    with open(os.path.join(site, "index.html"), "w", encoding="utf-8") as file:
        file.write(page.replace(BUILD_URL_LINE, f'const buildUrl = "{cdn}";').replace(CONTENT_URL_LINE, f'const contentUrl = "{cdn}/content";'))
    # The old link keeps working; its levels/ folder is left alone.
    os.makedirs(os.path.join(args.repo, OLD_SITE_FOLDER), exist_ok=True)
    with open(os.path.join(args.repo, OLD_SITE_FOLDER, "index.html"), "w", encoding="utf-8") as file:
        file.write(REDIRECT_PAGE)
    commit_if_changed(args.repo, [SITE_FOLDER, f":(exclude){SITE_FOLDER}/levels", f"{OLD_SITE_FOLDER}/index.html"], "Update Tileworlds page")

    if args.no_push:
        print("Not pushed (--no-push).")
        return
    subprocess.run(["git", "-C", args.repo, "-c", "http.postBuffer=524288000", "push", "-q", "origin", "HEAD"], check=True)
    print("Pushed.")

    # The first request for each file makes jsDelivr fetch it from GitHub; do that now rather than for the first player.
    # It can fail for a big file, which isn't fatal since the page falls back to GitHub Pages.
    for path in build_paths(build_files):
        name = os.path.relpath(path, build_files)
        for attempt in range(3):
            try:
                with urllib.request.urlopen(f"{cdn}/{name}", timeout=300) as response:
                    size = len(response.read())
                print(f"Cached on jsDelivr: {name} ({size / 1048576:.1f} MB)")
                break
            except OSError as error:
                print(f"jsDelivr didn't send {name} ({error}){', trying again' if attempt < 2 else ''}")
        else:
            print(f"  Players load {name} from GitHub Pages until jsDelivr has it.")


if __name__ == "__main__":
    main()
