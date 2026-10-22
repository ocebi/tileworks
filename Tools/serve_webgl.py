#!/usr/bin/env python3
"""Serve the WebGL build on this Mac and the local network, for testing in desktop and phone browsers.

    python3 Tools/serve_webgl.py [--port 8765] [--dir Builds/WebGL/Tileworlds]

The default folder holds the release build; the showcase build (with dev tools) is in Builds/WebGL/Tileworlds-dev.

Open the printed address on a phone on the same Wi-Fi. Nothing is cached, so a new build shows up on reload.
"""
import argparse
import functools
import http.server
import os
import socket
import socketserver

TYPES = {".wasm": "application/wasm", ".js": "application/javascript", ".data": "application/octet-stream", ".svg": "image/svg+xml"}


class Handler(http.server.SimpleHTTPRequestHandler):
    def guess_type(self, path):
        # Gzipped build files are named *.wasm.unityweb and so on; the type comes from the inner extension.
        base, ext = os.path.splitext(path)
        if ext == ".unityweb":
            ext = os.path.splitext(base)[1]
        return TYPES.get(ext) or super().guess_type(path)

    def end_headers(self):
        # The browser unpacks gzip itself, faster than Unity's fallback does.
        path = self.translate_path(self.path.split("?", 1)[0])
        if path.endswith(".unityweb") and is_gzip(path):
            self.send_header("Content-Encoding", "gzip")
        self.send_header("Cache-Control", "no-store")
        super().end_headers()

    def log_message(self, format, *args):
        if "404" in (format % args):
            super().log_message(format, *args)


def is_gzip(path):
    try:
        with open(path, "rb") as file:
            return file.read(2) == b"\x1f\x8b"
    except OSError:
        return False


def lan_address():
    with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as probe:
        try:
            probe.connect(("10.255.255.255", 1))
            return probe.getsockname()[0]
        except OSError:
            return None


def main():
    root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--port", type=int, default=8765)
    parser.add_argument("--dir", default=os.path.join(root, "Builds", "WebGL", "Tileworlds"))
    args = parser.parse_args()
    if not os.path.isfile(os.path.join(args.dir, "index.html")):
        raise SystemExit(f"No build in {args.dir}. Run Tileworks > Build > All Games > WebGL (release) first.")

    socketserver.ThreadingTCPServer.allow_reuse_address = True
    try:
        server = socketserver.ThreadingTCPServer(("0.0.0.0", args.port), functools.partial(Handler, directory=args.dir))
    except OSError as error:
        raise SystemExit(f"Can't use port {args.port} ({error.strerror}). Pick another with --port.")
    with server:
        print(f"Serving {args.dir}\n  This Mac: http://localhost:{args.port}/")
        lan = lan_address()
        if lan:
            print(f"  Phone:    http://{lan}:{args.port}/   (same Wi-Fi)")
        print("Ctrl+C to stop.")
        try:
            server.serve_forever()
        except KeyboardInterrupt:
            pass


if __name__ == "__main__":
    main()
