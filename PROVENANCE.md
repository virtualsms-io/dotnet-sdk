# Source provenance note

The published NuGet package (`VirtualSMS` 1.0.0) ships a compiled `VirtualSMS.dll` with no
embedded source and no prior GitHub repository — the `nuspec`'s `repository` field pointed at
this URL before this repo existed (404 until this commit).

Unlike the Ruby/Node.js/Python SDK repos in this backlink-metadata pass (which are byte-exact
recoveries of already-published source), this .NET source was **re-created from the NuGet
package's own shipped `README.md`**, which documents the exact public API surface
(`VirtualSMSClient`, `GetBalanceAsync`, `GetNumberAsync`, `GetStatusAsync`, `DoneAsync`,
`CancelAsync`, `WaitForCodeAsync`) and mirrors the identical method contract already
implemented and shipped in the PHP/Ruby/Node.js/Python SDKs against the same
`https://virtualsms.io/stubs/handler_api.php` endpoint.

This has not been built or tested against the live API from this session. Before the next
NuGet publish, verify:
- `dotnet build` succeeds
- A live smoke test against `https://virtualsms.io/stubs/handler_api.php` matches the
  compiled `1.0.0` DLL's actual behavior (decompile-diff recommended if in doubt)

## Validation pass (2026-07-16)

**Build/test: NOT RUN — no .NET SDK on this machine.** `C:\Program Files\dotnet` has only
the runtime (`Microsoft.NETCore.App`, `Microsoft.WindowsDesktop.App`); no `sdk\` folder,
so `dotnet build`/`restore`/`test` fail with "No .NET SDKs were found." This is a tooling
gap, not a code finding — `dotnet build` and `dotnet test` (including writing a smoke-test
project) still need to run on a machine with an SDK installed before this is build-verified.
No test project exists yet in this repo.

**Manual code review (in lieu of a compiler) — `src/VirtualSMS/VirtualSMSClient.cs`:**
No syntax errors spotted. `ImplicitUsings=enable` in the csproj covers `System`,
`System.Collections.Generic`, `System.Threading(.Tasks)`, `System.Net.Http` — the types used
without explicit `using`s (`Dictionary`, `List`, `DateTime`, `TimeSpan`, `Uri`) resolve under
that. Target-typed `new() { ["k"] = v }` against a `Dictionary<string,string>?` parameter is
valid C# 12 (net8.0). `KeyValuePair` tuple deconstruction in the `foreach` is supported since
.NET Core 2.0. Nothing here should block a build in principle, but this has NOT been
compiler-verified — treat as "no defects found by inspection," not "builds clean."

**Divergence check vs sibling SDKs** (node-sdk and python-sdk are the authoritative
references — both carry a `fix: recover published SDK source into version control` commit,
i.e. real recovered source, not reconstructions; php-sdk is also real/authoritative):
- Base URL (`https://virtualsms.io/stubs/handler_api.php`), action names (`getBalance`,
  `getNumber`, `getStatus`, `setStatus`), param names (`service`, `country`, `id`, `status`),
  response-prefix parsing (`ACCESS_BALANCE:`, `ACCESS_NUMBER:`, `NO_NUMBERS`,
  `STATUS_WAIT_CODE`, `STATUS_OK:`, `STATUS_CANCEL`), default country `187` (US), and the
  done/cancel status codes (`6`/`8`) all match exactly across .NET, Node
  (`node-sdk/index.js`), Python (`python-sdk/virtualsms/client.py`), and PHP
  (`virtualsms-php-sdk/src/VirtualSMS.php`). Auth is the same `api_key` query param scheme
  everywhere (no header-based auth on this legacy endpoint in any SDK).
- **Minor gap (not a bug per README):** Node and Python also implement `getPrices` (and Node
  additionally implements REST-API `rentNumber`/`getRentalStatus` against
  `https://virtualsms.io/api/v1`). The .NET client implements neither — but `README.md`
  explicitly scopes this SDK to balance/number/status/done/cancel/wait and says proxies/
  rentals are "on the roadmap, not shipped." So this is a documented smaller surface, not a
  divergence bug.
- **Minor robustness note (not fixed — see below):** `RequestAsync` in the .NET client
  URL-encodes parameter *values* via `Uri.EscapeDataString` but not the `action`/`api_key`
  literals in the query string (`VirtualSMSClient.cs:95-103`). Node (`URLSearchParams.set`),
  Python (`requests` `params=`), and PHP (`http_build_query`) all auto-encode every part of
  the query string. Harmless in practice (action names are fixed literals, API keys are
  `vsms_`-prefixed alphanumeric), but it's the one place the .NET client is less defensive
  than its siblings. Left as-is (no compile-blocking reason to touch it, and no compiler
  available this session to verify a change doesn't regress anything) — worth a one-line fix
  in a future session that has `dotnet build` available.

**Verdict: sound by inspection, NOT build-verified.** No structural/API-contract defects
found; the public surface matches the documented contract and the sibling SDKs exactly for
everything it claims to implement. Still needs an actual `dotnet build`/`dotnet test` pass
(SDK install required) before this can be called production-verified — don't downgrade this
line item to "done" until that happens.
