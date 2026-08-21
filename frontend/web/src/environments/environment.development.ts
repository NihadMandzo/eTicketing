// `ng serve` (dev server on :4200) talks to the Gateway on its real public port, 5000 (see
// .claude/rules/01-domain.md) — the same port docker-compose exposes, not the Gateway project's
// own plain `dotnet run` port (5262). Run the Gateway with `dotnet run` directly instead of via
// Docker and you'd need to point this at :5262 instead.
export const environment = {
  production: false,
  apiBaseUrl: 'http://localhost:5000/api',
};
