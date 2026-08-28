## graphify

This project has a knowledge graph at graphify-out/ with god nodes, community structure, and cross-file relationships.

Rules:
- For codebase questions, first run `graphify query "<question>"` when graphify-out/graph.json exists. Use `graphify path "<A>" "<B>"` for relationships and `graphify explain "<concept>"` for focused concepts. These return a scoped subgraph, usually much smaller than GRAPH_REPORT.md or raw grep output.
- **Before implementing any feature/change, query the graph first.** Run `graphify query "<what you're about to build/touch>"` (and `graphify explain "<concept>"` / `graphify path "<A>" "<B>"` as needed) to find the relevant existing code, related nodes, and cross-file relationships before writing anything. Only fall back to normal exploration (Grep/Glob/Read/Explore agent) when the graph query comes back thin or empty for that topic — don't skip the graph step because the task "seems obvious."
- If graphify-out/wiki/index.md exists, use it for broad navigation instead of raw source browsing.
- Read graphify-out/GRAPH_REPORT.md only for broad architecture review or when query/path/explain do not surface enough context.
- After modifying code, run `graphify update .` to keep the graph current (AST-only, no API cost).
