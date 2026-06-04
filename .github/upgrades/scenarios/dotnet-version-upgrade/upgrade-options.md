# Upgrade Options — VisualHFT

Assessment: 25 projects; 24 on net8.0/net8.0-windows*, 1 on net472 (demoTradingCore); WPF-heavy; 4,041 issues mostly behavioral WPF changes

## Strategy

### Upgrade Strategy
Solution has 1 .NET Framework project (demoTradingCore, net472) — Bottom-Up is fixed for Framework→modern migrations.

| Value | Description |
|-------|-------------|
| **Bottom-Up** (selected) | Upgrade leaf-node libraries first, then work upward tier by tier. Each tier validated independently. Fixed for .NET Framework solutions. |

## Project Structure

### Project Approach
`demoTradingCore` targets net472 (WinForms), no System.Web dependency. All other projects already on net8.0. In-place upgrade is appropriate — no other projects depend on demoTradingCore.

| Value | Description |
|-------|-------------|
| **In-place** (selected) | Replace net472 TFM directly with net10.0. No multi-targeting needed since no downstream consumers remain on .NET Framework. |
| Multi-targeting | Adds net10.0 alongside net472. Only needed if other projects must consume this library on both frameworks simultaneously. |

### Package Management
All 25 projects are SDK-style and upgrade is within modern .NET ecosystem (net8.0 → net10.0). Solution has 25 projects so CPM would add value — but user requested minimal migration, so deferring CPM to post-migration.

| Value | Description |
|-------|-------------|
| **Per-Project (defer CPM to post-migration)** (selected) | Each project retains its own package versions. Minimal changes, no restructuring. CPM can be added cleanly after all projects are on net10.0. |
| Central Package Management (CPM) | Consolidates all package versions in Directory.Packages.props. Better long-term, but adds scope beyond TFM bump. |

## Compatibility

### Unsupported API Handling
Assessment flags binary/source incompatible APIs across many projects — primarily WPF (1,826 issues) and Printing/XAML (45 issues). Most are behavioral changes (Api.0003), not removals. Fix Inline since user wants minimal changes and most are mechanical.

| Value | Description |
|-------|-------------|
| **Fix Inline** (selected) | Resolve every API change in the same task. Most WPF changes in net8→net10 are minor renames or behavioral — fix in place. |
| Defer Complex Changes | Apply simple replacements inline; stub complex ones for later. Adds cleanup debt. |

### Unsupported Packages
Assessment flagged NuGet.0001 (incompatible package) in VisualHFT.csproj and NuGet.0003 (package now included in framework) in demoTradingCore. Small set — resolve inline.

| Value | Description |
|-------|-------------|
| **Resolve Inline** (selected) | Research and resolve each incompatible package within the same task. Small count (1-2 packages). |
| Defer Resolution | Stub out incompatible packages and create follow-up tasks. Adds cleanup debt for minimal benefit here. |

## Modernization

### Nullable Reference Types
25 projects, >10k LOC, high-risk WPF changes already in scope. Enabling NRTs now would produce unmanageable warning volume and contradict the "keep migration minimal" directive.

| Value | Description |
|-------|-------------|
| **Leave Disabled** (selected) | Do not enable nullable. Maintain existing null handling. Enable separately after migration as a distinct effort. |
| Enable Nullable Reference Types | Adds `<Nullable>enable</Nullable>` to all projects. Recommended post-migration. |
