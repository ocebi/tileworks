#!/usr/bin/env python3
"""Publish exported levels (Builds/Levels) to ocebi.github.io/tessera/levels, where installed games pick them up.

    python3 Tools/publish_levels.py [--game tilematch] [--repo ~/Projects/ocebi.github.io] [--no-push]

Export first with Tileworks > Levels > Export for Publishing; it checks every level. Like deploy_pages.py this takes two commits
in the Pages repo: the level files, then each game's manifest, which points at that commit on jsDelivr and raises the revision.
Clients take a manifest only when its revision is higher than theirs. A game whose levels haven't changed is left alone.
After a push, each game's Content/published.json records the revision, so the next build knows which one its levels match.
The Levels window's Publish button runs this script; it reads the RESULT lines. See docs/REMOTE-LEVELS.md.
"""
import argparse
import concurrent.futures
import json
import os
import re
import subprocess
import sys
import urllib.request

LEVELS_FOLDER = "tessera/levels"
SAFE_ID = re.compile(r"^[A-Za-z0-9_\-]{1,64}$")


def git(repo, *args):
    return subprocess.run(["git", "-C", repo, *args], check=True, capture_output=True, text=True).stdout.strip()


def commit_if_changed(repo, paths, message):
    git(repo, "add", "-A", "--", *paths)
    if subprocess.run(["git", "-C", repo, "diff", "--cached", "--quiet"]).returncode == 0:
        return False
    git(repo, "commit", "-q", "-m", message)
    print(f"Committed: {message}")
    return True


def github_slug(repo):
    url = git(repo, "remote", "get-url", "origin")
    match = re.search(r"github\.com[:/](.+?)(?:\.git)?$", url)
    if not match:
        sys.exit(f"Origin {url} isn't on GitHub.")
    return match.group(1)


def read_json(path):
    with open(path, encoding="utf-8") as file:
        return json.load(file)


def write_json(path, data):
    with open(path, "w", encoding="utf-8") as file:
        json.dump(data, file, indent=2)
        file.write("\n")


def playlist(manifest):
    return [{k: level.get(k) for k in ("id", "hash", "payloadVersion", "reward", "label")} for level in manifest["levels"]]


def stamp(root, folder, game, revision, manifest):
    source = os.path.join(folder, "source.json")
    if not os.path.isfile(source):
        print(f"{game}: no source.json in the export, so the next build won't know it matches revision {revision}.")
        return
    content = os.path.join(root, read_json(source)["content"])
    path = os.path.join(content, "published.json")
    record = {"game": game, "revision": revision, "levels": playlist(manifest)}
    if not os.path.isfile(path) or read_json(path) != record:
        write_json(path, record)


def check_export(folder, game):
    manifest = read_json(os.path.join(folder, "manifest.json"))
    if manifest.get("game") != game:
        sys.exit(f"{folder}/manifest.json is for {manifest.get('game')}, not {game}.")
    if not manifest.get("levels"):
        sys.exit(f"{game}: the manifest has no levels.")
    for level in manifest["levels"]:
        if not SAFE_ID.match(level.get("id", "")):
            sys.exit(f"{game}: bad level id {level.get('id')!r}.")
        if not os.path.isfile(os.path.join(folder, level["id"] + ".json")):
            sys.exit(f"{game}: {level['id']} is in the manifest but wasn't exported.")
    return manifest


def main():
    root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--game", action="append", help="publish only this game (repeatable); default all exported games")
    parser.add_argument("--repo", default=os.path.expanduser("~/Projects/ocebi.github.io"))
    parser.add_argument("--export", default=os.path.join(root, "Builds", "Levels"))
    parser.add_argument("--no-push", action="store_true", help="commit, but don't push or warm the CDN")
    parser.add_argument("--no-warm", action="store_true", help="don't request the new files from jsDelivr after pushing")
    args = parser.parse_args()

    if not os.path.isdir(args.export):
        sys.exit(f"No export in {args.export}. Run Tileworks > Levels > Export for Publishing first.")
    games = args.game or sorted(g for g in os.listdir(args.export) if os.path.isfile(os.path.join(args.export, g, "manifest.json")))
    if not games:
        sys.exit(f"No game folders with a manifest.json in {args.export}.")
    if git(args.repo, "diff", "--cached", "--name-only"):
        sys.exit(f"{args.repo} has staged changes; commit or unstage them first.")

    # 1. The level files of every game that changed.
    changed = {}
    unchanged = {}
    for game in games:
        source = os.path.join(args.export, game)
        manifest = check_export(source, game)
        site = os.path.join(args.repo, LEVELS_FOLDER, game)
        published = os.path.join(site, "manifest.json")
        previous = read_json(published) if os.path.isfile(published) else None
        if previous and playlist(previous) == playlist(manifest):
            print(f"{game}: unchanged since revision {previous['revision']}")
            unchanged[game] = previous
            continue
        os.makedirs(site, exist_ok=True)
        subprocess.run(["rsync", "-a", "--delete", "--exclude", "manifest.json", "--exclude", "source.json", "--exclude", ".DS_Store",
                        source + "/", site + "/"], check=True)
        manifest["revision"] = (previous["revision"] if previous else 0) + 1
        changed[game] = manifest
    if not changed:
        if unpushed(args.repo):
            # An earlier publish committed but its push failed: push those commits now.
            if args.no_push:
                print("Not pushed (--no-push); earlier publish commits are still waiting to be pushed.")
                for game, previous in unchanged.items():
                    print(f"RESULT {game} committed {previous['revision']}")
                return
            push(args.repo)
            for game, previous in unchanged.items():
                stamp(root, os.path.join(args.export, game), game, previous["revision"], previous)
                print(f"RESULT {game} published {previous['revision']}")
            for game, previous in ({} if args.no_warm else unchanged).items():
                warm(previous["files"], game, previous)
            return
        print("Nothing to publish.")
        for game, previous in unchanged.items():
            stamp(root, os.path.join(args.export, game), game, previous["revision"], previous)
            print(f"RESULT {game} unchanged {previous['revision']}")
        return
    names = ", ".join(f"{game} r{manifest['revision']}" for game, manifest in changed.items())
    commit_if_changed(args.repo, [f"{LEVELS_FOLDER}/{game}" for game in changed], f"Update Tileworlds levels ({names})")
    files_commit = git(args.repo, "rev-parse", "HEAD")

    # 2. The manifests, pointing at those files on jsDelivr. Clients fall back to the copy next to the manifest.
    cdn = f"https://cdn.jsdelivr.net/gh/{github_slug(args.repo)}@{files_commit}/{LEVELS_FOLDER}"
    for game, manifest in changed.items():
        manifest["files"] = f"{cdn}/{game}"
        write_json(os.path.join(args.repo, LEVELS_FOLDER, game, "manifest.json"), manifest)
    commit_if_changed(args.repo, [f"{LEVELS_FOLDER}/{game}/manifest.json" for game in changed], f"Publish Tileworlds levels ({names})")

    if args.no_push:
        print("Not pushed (--no-push), so published.json is left as it was.")
        for game, manifest in changed.items():
            print(f"RESULT {game} committed {manifest['revision']}")
        return
    push(args.repo)
    print(f"Pushed. Players get {names} on their next start or return to Home (GitHub Pages caches the manifest for up to 10 minutes).")
    for game, manifest in changed.items():
        stamp(root, os.path.join(args.export, game), game, manifest["revision"], manifest)
        print(f"RESULT {game} published {manifest['revision']}")
    for game, previous in unchanged.items():
        stamp(root, os.path.join(args.export, game), game, previous["revision"], previous)
        print(f"RESULT {game} unchanged {previous['revision']}")

    for game, manifest in ({} if args.no_warm else changed).items():
        warm(manifest["files"], game, manifest)


def push(repo):
    subprocess.run(["git", "-C", repo, "push", "-q", "origin", "HEAD"], check=True)


def unpushed(repo):
    """Whether HEAD has commits its upstream doesn't, as of the last fetch."""
    result = subprocess.run(["git", "-C", repo, "rev-list", "--count", "@{u}..HEAD"], capture_output=True, text=True)
    return result.returncode == 0 and int(result.stdout.strip() or 0) > 0


def warm(files, game, manifest):
    """Each file's first jsDelivr request fetches it from GitHub; make that now rather than for the first player."""
    def fetch(level):
        try:
            urllib.request.urlopen(f"{files}/{level['id']}.json", timeout=20).read()
        except OSError as error:
            print(f"jsDelivr didn't send {game}/{level['id']}.json ({error}); clients use the Pages copy.")

    with concurrent.futures.ThreadPoolExecutor(max_workers=8) as pool:
        list(pool.map(fetch, manifest["levels"]))
    print(f"{game}: CDN warmed")


if __name__ == "__main__":
    main()
