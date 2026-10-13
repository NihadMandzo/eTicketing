"""Turns the raw output of loadtests/run-all.sh into one JSON file the report page charts.

    python loadtests/report/analyze.py            # reads loadtests/results/, writes report-data.json

Per run it reads <label>.csv.gz (every k6 sample), <label>-summary.json, <label>-stats.txt
(docker stats every 10 s) and <label>-outbox.csv (outbox backlog every 10 s). Percentiles are
computed exactly from the raw samples, not from k6's own summary, so per-bucket and per-endpoint
numbers use the same method.
"""
import csv
import gzip
import json
import math
import re
import sys
from collections import defaultdict
from pathlib import Path

RESULTS = Path(__file__).resolve().parent.parent / "results"
BUCKET = 5  # seconds per time-series point
RUNS = ["smoke", "tier-1k", "tier-5k", "breakpoint", "flash-burst", "flash-queue"]
COUNTERS = ["purchases_completed", "holds_granted", "sold_out_409", "shed_503", "gave_up_after_503", "server_errors"]


def pct(values, p):
    if not values:
        return None
    values = sorted(values)
    k = (len(values) - 1) * p / 100
    lo, hi = math.floor(k), math.ceil(k)
    return values[lo] + (values[hi] - values[lo]) * (k - lo)


def status_class(status):
    if status in ("", "0"):
        return "network"
    s = int(status)
    if 200 <= s < 300:
        return "ok"
    if s == 409:
        return "soldout"
    if s == 503:
        return "shed"
    if 500 <= s:
        return "error5xx"
    return "other4xx"


def read_samples(label):
    path = RESULTS / f"{label}.csv.gz"
    if not path.exists():
        return None
    durations = defaultdict(list)       # bucket -> [ms]
    endpoint_durations = defaultdict(list)
    statuses = defaultdict(lambda: defaultdict(int))
    vus = defaultdict(int)
    counters = defaultdict(lambda: defaultdict(float))
    endpoint_status = defaultdict(lambda: defaultdict(int))
    start = None
    def rows():
        try:
            with gzip.open(path, "rt", newline="") as f:
                yield from csv.DictReader(f)
        except EOFError:
            print(f"{label}: file still being written, using what was read so far", file=sys.stderr)

    for row in rows():
            if row["group"] == "::setup" or row["group"] == "::teardown":
                continue
            t = int(row["timestamp"])
            if start is None:
                start = t
            b = (t - start) // BUCKET
            m = row["metric_name"]
            if m == "http_req_duration":
                v = float(row["metric_value"])
                durations[b].append(v)
                endpoint_durations[row["name"]].append(v)
            elif m == "http_reqs":
                cls = status_class(row["status"])
                statuses[b][cls] += 1
                endpoint_status[row["name"]][cls] += 1
            elif m == "vus":
                vus[b] = max(vus[b], int(float(row["metric_value"])))
            elif m in COUNTERS:
                counters[m][b] += float(row["metric_value"])
    if start is None:
        return None
    last = max(list(durations) + list(statuses) + list(vus) + [0])
    series = []
    for b in range(last + 1):
        st = statuses.get(b, {})
        total = sum(st.values())
        d = durations.get(b, [])
        series.append({
            "t": b * BUCKET,
            "vus": vus.get(b),
            "rps": round(total / BUCKET, 1),
            "p50": round(pct(d, 50), 1) if d else None,
            "p95": round(pct(d, 95), 1) if d else None,
            "ok": st.get("ok", 0), "soldout": st.get("soldout", 0), "shed": st.get("shed", 0),
            "error5xx": st.get("error5xx", 0), "other4xx": st.get("other4xx", 0), "network": st.get("network", 0),
            **{c: round(counters[c].get(b, 0)) for c in COUNTERS if c in counters},
        })
    endpoints = []
    for name, values in endpoint_durations.items():
        sc = endpoint_status[name]
        endpoints.append({
            "name": name, "count": len(values),
            "p50": round(pct(values, 50), 1), "p95": round(pct(values, 95), 1), "p99": round(pct(values, 99), 1),
            "ok": sc.get("ok", 0), "shed": sc.get("shed", 0), "soldout": sc.get("soldout", 0),
            "failed": sc.get("error5xx", 0) + sc.get("network", 0) + sc.get("other4xx", 0),
        })
    endpoints.sort(key=lambda e: -e["count"])
    return start, series, endpoints


def read_summary(label):
    path = RESULTS / f"{label}-summary.json"
    if not path.exists():
        return None
    m = json.loads(path.read_text(encoding="utf-8"))["metrics"]
    get = lambda k, f: (m.get(k) or {}).get(f)
    dur = m.get("http_req_duration", {})
    out = {
        "requests": get("http_reqs", "count"),
        "rps": round(get("http_reqs", "rate") or 0, 1),
        "errorRate": round((get("http_req_failed", "value") or 0) * 100, 2),
        "p50": round(dur.get("med", 0), 1), "p90": round(dur.get("p(90)", 0), 1),
        "p95": round(dur.get("p(95)", 0), 1), "p99": round(dur.get("p(99)", 0), 1),
        "max": round(dur.get("max", 0), 1),
        "vusMax": get("vus_max", "max") or get("vus", "max"),
        "checks": round((get("checks", "value") or 0) * 100, 2),
    }
    for c in COUNTERS:
        if c in m:
            out[c] = m[c].get("count")
    if "time_to_ticket" in m:
        out["time_to_ticket_p95"] = round(m["time_to_ticket"].get("p(95)", 0), 1)
    thresholds = []
    for name, metric in m.items():
        for expr, ok in (metric.get("thresholds") or {}).items():
            thresholds.append({"metric": name, "expr": expr, "ok": not ok if isinstance(ok, bool) else ok})
    out["thresholds"] = thresholds
    return out


def to_seconds(hms):
    h, mi, s = (int(x) for x in hms.split(":"))
    return h * 3600 + mi * 60 + s


def read_stats(label, start):
    path = RESULTS / f"{label}-stats.txt"
    if not path.exists() or start is None:
        return []
    day_start = start % 86400
    points, current = [], None
    for line in path.read_text(encoding="utf-8").splitlines():
        if line.startswith("=== "):
            current = {"t": to_seconds(line[4:].strip()) - day_start, "cpu": {}}
            points.append(current)
            continue
        parts = line.split("\t")
        if current is None or len(parts) < 2:
            continue
        name = re.sub(r"^eticketing-(lt-|loadtest-)", "", parts[0])
        name = "k6" if name.startswith("k6") else name
        try:
            current["cpu"][name] = round(float(parts[1].rstrip("%")) / 100, 2)  # cores
        except ValueError:
            pass
    return [p for p in points if p["cpu"] and p["t"] >= -10]


def read_outbox(label, start):
    path = RESULTS / f"{label}-outbox.csv"
    if not path.exists() or start is None:
        return []
    day_start = start % 86400
    rows = []
    with open(path, encoding="utf-8") as f:
        for row in csv.DictReader(f):
            try:
                rows.append({"t": to_seconds(row["time"]) - day_start,
                             **{k: int(v) for k, v in row.items() if k != "time" and v not in (None, "")}})
            except ValueError:
                pass
    return rows


def main():
    report = {"bucketSeconds": BUCKET, "runs": {}}
    for label in RUNS:
        samples = read_samples(label)
        if samples is None:
            print(f"skip {label}: no samples", file=sys.stderr)
            continue
        start, series, endpoints = samples
        report["runs"][label] = {
            "summary": read_summary(label),
            "series": series,
            "endpoints": endpoints,
            "cpu": read_stats(label, start),
            "outbox": read_outbox(label, start),
        }
        print(f"{label}: {len(series)} buckets, {len(endpoints)} endpoints", file=sys.stderr)
    for name in ("verify-redis-lock", "verify-recovery"):
        p = RESULTS / f"{name}.txt"
        if p.exists():
            report[name] = p.read_text(encoding="utf-8")
    out = RESULTS / "report-data.json"
    out.write_text(json.dumps(report, separators=(",", ":")), encoding="utf-8")
    print(f"wrote {out} ({out.stat().st_size // 1024} KB)", file=sys.stderr)


if __name__ == "__main__":
    main()
