[//]: # "<latest_stable_release>9.0.0.0</latest_stable_release>"

<p align="center">
    <img src="images/AdKats.png" alt="AdKats Advanced In-Game Admin Tools">
</p>

<p align="center">
    <strong>Advanced In-Game Admin and Ban Enforcer for Procon v2</strong>
</p>

<p align="center">
    <a href="../../wiki">Full Documentation (Wiki)</a> &bull;
    <a href="../../tree/legacy">Procon v1 (legacy branch)</a>
</p>

---

## Overview

AdKats is a comprehensive admin toolset with over 100 in-game commands and extensive customization options. It focuses on making in-game admins more efficient and accurate, with flexibility for almost any server setup.

Designed for groups with high-traffic servers and many admins, but works just as well for small servers.

**Supported Games:** BF3, BF4, BF Hardline, BFBC2

## Features

- **100+ In-Game Commands** &mdash; Kill, kick, ban, punish, move, whitelist, message, and more. Accessible from in-game chat, Procon's chat window, the database, or other plugins.
- **Role-Based Access Control** &mdash; Custom user roles with granular command permissions. Roles sync automatically across servers.
- **Cross-Server Ban Enforcer** &mdash; Multi-metric enforcement by GUID, IP, and name. Bans are checked on player join across all connected servers.
- **Infraction Tracking** &mdash; Escalating punishments based on player history. All players treated equally regardless of who issues the punishment.
- **Player Reputation System** &mdash; Numeric reputation scoring based on commands issued from and against players.
- **AntiCheat** &mdash; Stats-based cheat detection using Battlelog and EZScale data. Automatic detection and banning for damage mods, aimbots, and magic bullet.
- **Setting Sync** &mdash; Database-driven settings replicated across all Procon layers. Change once, apply everywhere.
- **Report & Admin Call System** &mdash; With email and PushBullet notifications.
- **VPN/Proxy Detection** &mdash; External API checks with schedule-based enforcement.
- **Ping Enforcer** &mdash; Automated kick for high-latency players.
- **AFK Manager** &mdash; Automatic kick for idle players.
- **SpamBot** &mdash; Customizable timed message broadcasting.
- **Surrender Vote System** &mdash; Player-initiated round surrender voting.
- **Cross-Server Messaging** &mdash; Player-to-player communication across servers.
- **TeamSpeak & Discord Integration** &mdash; Perks and monitoring via VOIP platforms.
- **Challenge System** &mdash; Round-based competitive challenges.
- **On-Spawn Loadout Enforcer** &mdash; BF4 infantry and vehicle loadout enforcement via [AdKatsLRT](https://github.com/AdKats/AdKats-LRT).

## Documentation

Full documentation is available on the [**AdKats Wiki**](../../wiki), including:

- [Commands Reference](../../wiki/Commands) — all 100+ in-game commands
- [Settings Guide](../../wiki/Settings) — complete configuration reference
- [AntiCheat System](../../wiki/AntiCheat) — cheat detection setup
- [Ban Enforcer](../../wiki/Ban-Enforcer) — cross-server ban management
- [Infraction System](../../wiki/Infraction-System) — punishment tracking
- [External Commands API](../../wiki/External-Commands-API) — integrating with AdKats from other plugins

## Dependencies

- **Procon v2**
- **MySQL Database** &mdash; Used for cross-server settings, ban enforcement, player tracking, and all persistent data.
- **CChatGUIDStatsLogger** (XpKiller's Stats Logger) &mdash; Required for player stats tracking and database logging. Not yet refactored for v9.

## Installation

1. Download the latest release from the [Releases](../../releases) page.
2. Place the `.cs` plugin files in your Procon v2 plugins directory.
3. Set up a MySQL database and import `src/adkats.sql` for the initial schema.
4. Enable the plugin in Procon and configure your database connection in the plugin settings.
5. Follow the in-game setup prompts to complete initial configuration.

## Project Structure

```
src/
  AdKats.cs              # Main plugin entry point
  AdKats/                # Modular components (partial classes)
    Actions.cs           # Kill processing and action execution
    AntiCheat.cs         # Stats-based cheat detection
    BanEnforcer.cs       # Cross-server ban enforcement
    Challenges.cs        # Round-based challenge system
    Commands.cs          # Command parsing and execution
    Database.cs          # Database communication (Dapper ORM)
    Events.cs            # Procon event handlers
    External.cs          # VPN/proxy detection
    EZScale.cs           # EZScale API integration
    Integrations.cs      # Email, PushBullet, Discord, TeamSpeak
    Messaging.cs         # Chat message parsing and player messaging
    Models.cs            # Data model classes
    Players.cs           # Player state tracking
    Settings.cs          # Plugin settings and configuration
    Threading.cs         # Thread management and synchronization
    Utilities.cs         # Helper functions
  lib/                   # Game data files (loadouts, weapon codes)
  adkats.sql             # Database schema
  *.json                 # Weapon stats, reputation data, update manifests
```

## Development

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download) (for formatting and style checks)
- Procon v2 (for runtime testing)

### Style Checks

The project uses `dotnet format` with an `.editorconfig` for style enforcement. CI runs these checks automatically on pushes to `master` and on pull requests.

```bash
# Check for style violations
dotnet format --verify-no-changes

# Auto-fix style violations
dotnet format
```

### Releases

Releases are automated via GitHub Actions. To create a release:

1. Tag the commit with a version tag: `git tag v9.0.0`
2. Push the tag: `git push origin v9.0.0`
3. GitHub Actions will package the source files and create a release automatically.

## Community

- [AdKats Forum Thread](https://myrcon.net/topic/459-advanced-in-game-admin-and-ban-enforcer-adkats/) &mdash; Discussion and support on myrcon.net
- [AdKatsLRT Extension](https://github.com/AdKats/AdKats-LRT) &mdash; On-spawn loadout enforcer for BF4

## History

AdKats was originally created by Daniel J. Gradinjan (ColColonCleaner) for A Different Kind (ADK) on April 20, 2013. Version 8.0+ was maintained by Hedius. Version 9.0 is a rewrite for Procon v2.

For the full legacy changelog (v0.0.1 through v7.6.0.10), see the [`legacy` branch](../../tree/legacy/CHANGELOG.md).

## License

[GNU General Public License v3](LICENSE)
