# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/), and this project adheres to [Semantic Versioning](https://semver.org/).

For the legacy changelog (v0.0.1 through v7.6.0.10, Procon v1), see the [`legacy` branch](../../tree/legacy/CHANGELOG.md).

## [9.0.0] - 2026-03-28

### Added
- Procon v2 support
- Modular partial class architecture (16 component files)
- Dapper ORM for database operations
- Flurl HTTP client library
- EZScale API integration for cheat detection
- Challenge system for round-based competitive play
- GitHub Actions CI/CD workflows
- `.editorconfig` for consistent code style
- Automated release packaging on version tags

### Changed
- Complete rewrite targeting Procon v2 (not backwards compatible with Procon v1)
- Source restructured into `src/` directory with organized subdirectories
- Database connectivity migrated from legacy MySQL connector to MySqlConnector
- Threading model redesigned with centralized ThreadManager

### Removed
- Procon v1 compatibility (see `legacy` branch)
