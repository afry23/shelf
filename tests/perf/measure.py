#!/usr/bin/env python3
"""Measures the performance targets of PRD 9.1 with the real shelf binary on a populated library.

    measure.py --shelf BIN --library DIR [--tolerance 1.0] [--skip-reindex]

Every number is the wall time of a whole process, including its start. Targets (multiplied by --tolerance):
cold start < 150 ms, scan without changes < 3 s, search p95 < 100 ms (250 ms for very common words), initial index (reindex from the sidecars) < 15 min.
Prints a table and exits 1 if a target is missed.
"""
import argparse
import os
import statistics
import subprocess
import sys
import time

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


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--shelf", required=True)
    parser.add_argument("--library", required=True)
    parser.add_argument("--tolerance", type=float, default=1.0, help="factor applied to every target (shared CI runners)")
    parser.add_argument("--skip-reindex", action="store_true")
    parser.add_argument("--search-runs", type=int, default=200)
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
