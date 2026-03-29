# AdKats v9 — Procon v2 Plugin

## Project Overview

AdKats is a C# admin toolset plugin for Procon v2 (Battlefield game server administration). Version 9 is a complete rewrite for Procon v2 — the legacy Procon v1 version lives on the `legacy` branch.

- **Language:** C#
- **License:** GPLv3
- **Supported games:** BF3, BF4, BF Hardline, BFBC2
- **Dependencies:** MySqlConnector, Dapper, Flurl, Flurl.Http, Newtonsoft.Json, Procon v2 (runtime only)
- **External plugin dependency:** CChatGUIDStatsLogger (XpKiller's Stats Logger) — not yet refactored for v9

## Architecture

The plugin uses a **partial class** pattern — `AdKats` is split across 16+ files in `src/AdKats/`:

| File | Responsibility |
|------|---------------|
| `src/AdKats.cs` | Main entry point, enums, plugin metadata |
| `src/AdKats/Models.cs` | Data model classes (APlayer, ARecord, ABan, ACommand, etc.) |
| `src/AdKats/Database.cs` | MySQL via Dapper ORM — all queries, player fetch, settings sync |
| `src/AdKats/Commands.cs` | In-game command parsing and execution |
| `src/AdKats/Actions.cs` | Kill processing, action queue, HasAccess() RBAC check |
| `src/AdKats/BanEnforcer.cs` | Cross-server ban enforcement |
| `src/AdKats/AntiCheat.cs` | Stats-based cheat detection (Battlelog + EZScale) |
| `src/AdKats/Events.cs` | Procon event handlers |
| `src/AdKats/External.cs` | VPN/proxy detection via Procon's IP check service |
| `src/AdKats/EZScale.cs` | EZScale ML API integration |
| `src/AdKats/Integrations.cs` | Email, PushBullet, Discord, TeamSpeak |
| `src/AdKats/Messaging.cs` | Chat message parsing, mute system, instance checks |
| `src/AdKats/Players.cs` | Player listing thread, state tracking |
| `src/AdKats/Settings.cs` | Plugin settings UI and persistence |
| `src/AdKats/Threading.cs` | ThreadManager, watchdog threads |
| `src/AdKats/Utilities.cs` | Helpers: HTTP, logging, string ops, parameter parsing |

## Code Style

Style is enforced by `.editorconfig` and checked via `dotnet format` in CI.

**Critical conventions:**
- **Use `String`, `Int32`, `Boolean`, `Double`** — NOT `string`, `int`, `bool`, `double`. The codebase uses explicit System type names everywhere.
- **Allman brace style** — opening brace on its own line
- **4 spaces** for indentation, LF line endings
- **Block-scoped namespaces** (not file-scoped)
- **`using` directives outside namespace**, System usings first

## Build & CI

- `AdKats.csproj` at root is a **CI-only artifact** for `dotnet format`. It is NOT a real build file — Procon v2 assemblies are unavailable for compilation.
- **CI workflow** (`.github/workflows/ci.yml`): runs on push to `master` and PRs. Checks `dotnet format whitespace` and `dotnet format style --exclude-diagnostics IDE1007`.
- **Release workflow** (`.github/workflows/release.yml`): triggered by `v*` tags. Packages `.cs` files from `src/` into a zip and creates a GitHub Release.

### Running style checks locally

```bash
dotnet restore
dotnet format whitespace --verify-no-changes
dotnet format style --verify-no-changes --severity warn --exclude-diagnostics IDE1007
```

## Threading Model

The plugin runs multiple dedicated consumer threads, each with a queue + `EventWaitHandle` pattern:
- Messaging, Commands, Actions, BanEnforcer, Database, Players, AntiCheat

**Queue pattern:** Lock the queue, check count inside the lock, drain and clear. Never check `.Count` outside a lock (TOCTOU).

## Database

- All queries use **Dapper parameterized queries** (`@paramName` with `DynamicParameters` or anonymous objects). Never concatenate user input into SQL strings.
- Schema name (`_mySqlSchemaName`) is validated to `^[a-zA-Z0-9_]+$` on assignment.
- `IsSoldierNameValid()` enforces `^[a-zA-Z0-9_\-]+$` and max 16 chars.

## Security Notes

- **No hardcoded credentials.** SMTP, database, and API credentials must be configured at runtime via plugin settings.
- **No `Environment.Exit` in chat handlers.** Legitimate exit calls exist only for memory management, server reboot, and level-load shutdown.
- **Instance check messages** (`AdKatsInstanceCheck`) are only accepted from `Speaker == "Server"`.
- **External plugin commands** (`ParseExternalCommand`) are subject to `HasAccess()` RBAC checks.
- **Passwords are masked** in the Procon settings UI (shown as `********`).
- **Release workflow** pins third-party GitHub Actions to commit SHAs.

## Branch Structure

- `master` — current development, Procon v2 only
- `legacy` — archived Procon v1 version, no longer maintained
