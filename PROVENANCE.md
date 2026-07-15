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
