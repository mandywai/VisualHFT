# .NET Version Upgrade

## Preferences
- **Flow Mode**: Automatic
- **Target Framework**: net10.0 (LTS)
- **Migration Approach**: Minimal — do not change code unless it fails to compile

## Source Control
- **Source Branch**: master
- **Working Branch**: master (user confirmed staying on current branches)
- **Commit Strategy**: Manual (user will commit everything at the end — do NOT auto-commit)

## Strategy
**Selected**: Bottom-Up (Dependency-First)
**Rationale**: Solution contains 1 .NET Framework project (demoTradingCore, net472). Bottom-Up is non-negotiable for Framework→modern migrations with 2+ projects.

### Execution Constraints
- Strict tier ordering: complete and validate each tier before starting the next
- Between-tier validation: confirm higher tiers still build after each tier completes
- All projects build warning-free before marking a task complete
- No code changes unless required to compile
- Never auto-commit — user commits manually at the end

## Upgrade Options
**Source**: .github/upgrades/scenarios/dotnet-version-upgrade/upgrade-options.md

### Strategy
- Upgrade Strategy: Bottom-Up (fixed)

### Project Structure
- Project Approach: In-place
- Package Management: Per-Project (defer CPM to post-migration)

### Compatibility
- Unsupported Packages: Resolve Inline
- Unsupported API Handling: Fix Inline

### Modernization
- Nullable Reference Types: Leave Disabled
