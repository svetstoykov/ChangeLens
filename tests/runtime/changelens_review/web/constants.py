"""Constants shared by the local review web server: its address, guard header, routes, and files."""

from pathlib import Path

API_PREFIX = "/api"
REVIEW_HEADER = "X-ChangeLens-Review"
REVIEW_HEADER_VALUE = "1"
DEFAULT_PORT = 8765
BIND_HOST = "127.0.0.1"
PORT_PROBE_SECONDS = 0.5
RAW_FILES = ("engine.log", "protocol.ndjson")
STATIC_ROOT = Path(__file__).resolve().parent / "static"
STATIC_FILES = {
    "/": ("index.html", "text/html; charset=utf-8"),
    "/index.html": ("index.html", "text/html; charset=utf-8"),
    "/app.css": ("app.css", "text/css; charset=utf-8"),
    "/app.js": ("app.js", "text/javascript; charset=utf-8"),
}
