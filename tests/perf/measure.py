#!/usr/bin/env python3
"""Measures the performance targets of PRD 9.1 with the real shelf binary on a populated library.

    measure.py --shelf BIN --library DIR [--tolerance 1.0] [--skip-reindex] [--semantic] [--web]

Every number is the wall time of a whole process, including its start. Targets (multiplied by --tolerance):
cold start < 150 ms, scan without changes < 3 s, search p95 < 100 ms (250 ms for very common words), initial index (reindex from the sidecars) < 15 min.
Prints a table and exits 1 if a target is missed. The M2 commands (organize as a dry run, tag list, dupes, trash empty
as a dry run) have no target in the PRD; their times are only printed, so that a regression stands out.
--semantic (the library must hold embeddings: shelf reindex --embeddings) adds the semantic search p95 (work < 300 ms,
chunk < 800 ms), once as a one-shot `shelf semantic` process (model load included; reported, no target) and once as a request to a running
`shelf mcp` server (model loaded). It runs before the reindex step, which drops the embeddings.
--web starts `shelf serve` and measures what a browser asks for when the library view opens (the page, its script and style, and
the first page of the list with its facets: < 500 ms), the other calls of the views (sorting, filters, search, details, cover,
duplicates; printed, no target) and the resident memory of the idle server (< 200 MB, PRD 9.1). It runs before the reindex step.
"""
import json
import argparse
import os
import re
import statistics
import subprocess
import sys
import time
import urllib.request

# Queries a person types: one to three words, most of them selective.
TYPICAL = [
    "event", "kubernetes", "architecture", "\"event sourcing\"", "author:smith", "tag:cloud", "lang:de",
    "protocol -sidecar", "title:handbook", "zug", "verteilte", "cluster node", "consensus", "queue", "stream*",
]
# Words that are in nearly every passage of the synthetic books (and, in real books, stop words): FTS5 has to score
# a large share of the index before it can pick the best passages.
COMMON = ["the", "data", "system*", "year:>2010 data", "format:pdf network"]


def run(shelf, args):
    start = time.perf_counter()
    result = subprocess.run([shelf, *args], capture_output=True, text=True)
    elapsed = time.perf_counter() - start
    return elapsed, result


def p95(values):
    ordered = sorted(values)
    return ordered[min(len(ordered) - 1, int(len(ordered) * 0.95 + 0.9999) - 1)]


SEMANTIC = [
    "Bücher über Messaging zwischen Microservices", "event sourcing", "verteilte Systeme Konsens",
    "kubernetes cluster operations", "Datenbank Replikation", "stream processing", "Architektur Muster",
    "how to scale a queue",
]


def mcp_latencies(shelf, library, level, runs):
    """Milliseconds per semantic_search request to one running server; the first request (model load) is left out."""
    server = subprocess.Popen([shelf, "mcp", "--library", library], stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                              stderr=subprocess.DEVNULL, text=True, bufsize=1)

    def call(number, method, params=None):
        server.stdin.write(json.dumps({"jsonrpc": "2.0", "id": number, "method": method, "params": params or {}}) + "\n")
        server.stdin.flush()
        while True:
            reply = json.loads(server.stdout.readline())
            if reply.get("id") == number:
                return reply

    try:
        call(1, "initialize", {"protocolVersion": "2025-06-18", "capabilities": {}, "clientInfo": {"name": "measure", "version": "1"}})
        server.stdin.write(json.dumps({"jsonrpc": "2.0", "method": "notifications/initialized"}) + "\n")
        values = []
        for i in range(runs + 1):
            start = time.perf_counter()
            call(10 + i, "tools/call", {"name": "semantic_search", "arguments": {"query": f"{SEMANTIC[i % len(SEMANTIC)]} {i}", "level": level}})
            values.append((time.perf_counter() - start) * 1000)
        return values[1:]
    finally:
        server.stdin.close()
        server.wait(timeout=30)


def get(url, headers=None):
    """Seconds and body of one request on a connection of its own (a browser opening the page does the same)."""
    start = time.perf_counter()
    request = urllib.request.Request(url, headers=headers or {})
    with urllib.request.urlopen(request, timeout=60) as response:
        body = response.read()
    return time.perf_counter() - start, body


def resident_mb(pid):
    out = subprocess.run(["ps", "-o", "rss=", "-p", str(pid)], capture_output=True, text=True).stdout.strip()
    return int(out) / 1024


def web_rows(args, check, rows):
    """Starts `shelf serve` on a free port and measures the web interface (PRD 9.1: library view < 500 ms, idle memory < 200 MB)."""
    started = time.perf_counter()
    server = subprocess.Popen([args.shelf, "serve", "--library", args.library, "--port", "0"], stdout=subprocess.DEVNULL,
                              stderr=subprocess.PIPE, text=True)
    try:
        line = ""
        while "Listening on" not in line:
            line = server.stderr.readline()
            if not line:
                raise RuntimeError("shelf serve did not start")
        base = re.search(r"http://[^\s]+", line).group(0).replace("0.0.0.0", "127.0.0.1")
        rows.append(("serve: start to listening", (time.perf_counter() - started) * 1000, None, "ms", True))
        time.sleep(1)
        check("serve: resident memory, idle (before any request)", resident_mb(server.pid), 200, "MB")

        def timed(path, runs):
            return [get(base + path)[0] * 1000 for _ in range(runs)]

        # What a browser needs when the library view opens: the page, its script and style, then the first page of the list.
        _, page = get(base + "/")
        text = page.decode()
        script = re.search(r'src="(/assets/[^"]+\.js)"', text).group(1)
        style = re.search(r'href="(/assets/[^"]+\.css)"', text).group(1)
        get(base + "/api/v1/works")  # the first request opens the index
        opens = []
        for _ in range(args.web_runs):
            total = sum(get(base + path)[0] for path in ("/", script, style, "/api/v1/works"))
            opens.append(total * 1000)
        check(f"library view: page + script + style + first list ({len(opens)} runs, p95)", p95(opens), 500, "ms")
        rows.append(("library view: the first list alone, median", statistics.median(timed("/api/v1/works", args.web_runs)), None, "ms", True))

        for label, path in (
            ("list sorted by author (folded keys in memory), p95", "/api/v1/works?sort=author"),
            ("list sorted by year, p95", "/api/v1/works?sort=year&order=desc"),
            ("list filtered by language, p95", "/api/v1/works?q=lang%3Ade"),
            ("list filtered by tag, p95", "/api/v1/works?q=tag%3Acloud"),
            ("list, last page of 48, p95", "/api/v1/works?offset=9950"),
            ("search (full text), p95", "/api/v1/search?q=event"),
            ("search, very common word, p95", "/api/v1/search?q=data"),
            ("tags, p95", "/api/v1/tags"),
        ):
            rows.append((label, p95(timed(path, 20)), None, "ms", True))

        listing = json.loads(get(base + "/api/v1/works?limit=5")[1])["items"]
        detail = listing[0]["id"]
        rows.append(("work details, p95", p95(timed(f"/api/v1/works/{detail}", 20)), None, "ms", True))
        covered = next((i["id"] for i in json.loads(get(base + "/api/v1/works?limit=100")[1])["items"] if i["hasCover"]), None)
        if covered:
            rows.append(("cover of a work, p95", p95(timed(f"/api/v1/works/{covered}/cover", 20)), None, "ms", True))
        rows.append(("duplicates (the whole library), once", timed("/api/v1/duplicates", 1)[0], None, "ms", True))
        time.sleep(1)
        check("serve: resident memory after these requests", resident_mb(server.pid), 200, "MB")
    finally:
        server.terminate()
        server.wait(timeout=30)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--shelf", required=True)
    parser.add_argument("--library", required=True)
    parser.add_argument("--tolerance", type=float, default=1.0, help="factor applied to every target (shared CI runners)")
    parser.add_argument("--skip-reindex", action="store_true")
    parser.add_argument("--semantic", action="store_true", help="also measure semantic search (needs embeddings)")
    parser.add_argument("--search-runs", type=int, default=200)
    parser.add_argument("--web", action="store_true", help="also measure the web interface (starts shelf serve)")
    parser.add_argument("--web-runs", type=int, default=40)
    args = parser.parse_args()
    lib = ["--library", args.library]
    rows = []

    def check(name, value, limit, unit):
        limit *= args.tolerance
        ok = value < limit
        rows.append((name, value, limit, unit, ok))

    # cold start: a fresh process each time, no library involved
    times = [run(args.shelf, ["--version"])[0] for _ in range(30)]
    check("cold start (median of 30, --version)", statistics.median(times) * 1000, 150, "ms")

    # scan without changes
    run(args.shelf, ["scan", *lib])  # settle: first scan may touch records
    scans = []
    for _ in range(3):
        elapsed, result = run(args.shelf, ["scan", *lib])
        if result.returncode != 0:
            print(result.stderr, file=sys.stderr)
            return 1
        scans.append(elapsed)
    check("scan without changes (median of 3)", statistics.median(scans), 3, "s")

    # search latency
    run(args.shelf, ["search", "data", *lib])  # warm the file cache like a user's second query

    def latencies(queries, runs):
        values = []
        for i in range(runs):
            elapsed, result = run(args.shelf, ["search", queries[i % len(queries)], *lib])
            if result.returncode not in (0, 2):
                print(result.stderr, file=sys.stderr)
                sys.exit(1)
            values.append(elapsed * 1000)
        return values

    typical = latencies(TYPICAL, args.search_runs)
    check(f"search p95 ({len(typical)} runs, {len(TYPICAL)} typical queries)", p95(typical), 100, "ms")
    rows.append(("search median, typical queries", statistics.median(typical), None, "ms", True))
    common = latencies(COMMON, max(40, args.search_runs // 5))
    check(f"search p95 ({len(common)} runs, {len(COMMON)} very common words)", p95(common), 250, "ms")

    # M2 commands: no target, the time is informative (dry runs and listings only; nothing is changed)
    for label, command in (
        ("organize, dry run (whole library)", ["organize"]),
        ("tag list", ["tag", "list"]),
        ("dupes (list)", ["dupes"]),
        ("trash empty, dry run", ["trash", "empty"]),
    ):
        elapsed, result = run(args.shelf, [*command, *lib])
        if result.returncode != 0:
            print(result.stderr, file=sys.stderr)
            return 1
        rows.append((label, elapsed, None, "s", True))

    if args.web:
        web_rows(args, check, rows)

    if args.semantic:
        for level, flags, limit in (("work", [], 300), ("chunk", ["--chunks"], 800)):
            one_shot = []
            for i in range(40):
                elapsed, result = run(args.shelf, ["semantic", SEMANTIC[i % len(SEMANTIC)], *flags, *lib])
                if result.returncode != 0:
                    print(result.stderr, file=sys.stderr)
                    return 1
                one_shot.append(elapsed * 1000)
            # The model (470 MB) is read and set up on every start: about 0.45 s, whatever the query. Only a process that
            # stays up (mcp, later serve) can meet the targets; the one-shot time is reported (ADR 0014).
            rows.append((f"semantic {level} p95, one-shot process (40 runs)", p95(one_shot), None, "ms", True))
            running = mcp_latencies(args.shelf, args.library, level, 40)
            check(f"semantic {level} p95, running mcp server (40 runs)", p95(running), limit, "ms")

    # initial index from the sidecars
    if not args.skip_reindex:
        database = os.path.join(args.library, ".shelf", "index.db")
        for suffix in ("", "-wal", "-shm"):
            if os.path.exists(database + suffix):
                os.remove(database + suffix)
        elapsed, result = run(args.shelf, ["reindex", *lib])
        if result.returncode != 0:
            print(result.stderr, file=sys.stderr)
            return 1
        check("initial index (reindex)", elapsed / 60, 15, "min")

    failed = False
    for name, value, limit, unit, ok in rows:
        target = "" if limit is None else f"  target < {limit:g} {unit}  {'ok' if ok else 'MISSED'}"
        print(f"{name:<48} {value:>9.2f} {unit}{target}")
        failed |= not ok
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
