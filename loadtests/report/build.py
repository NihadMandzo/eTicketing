"""Builds the report page: template.html + results/report-data.json -> results/load-test-report.html.

    python loadtests/report/build.py
"""
import json
from datetime import date
from pathlib import Path

HERE = Path(__file__).resolve().parent
RESULTS = HERE.parent / "results"

data = json.loads((RESULTS / "report-data.json").read_text(encoding="utf-8"))
data["generated"] = date.today().isoformat()
payload = json.dumps(data, separators=(",", ":")).replace("</", "<\\/")
page = (HERE / "template.html").read_text(encoding="utf-8").replace("/*DATA*/", payload)
out = RESULTS / "load-test-report.html"
out.write_text(page, encoding="utf-8")
print(f"wrote {out} ({out.stat().st_size // 1024} KB)")
