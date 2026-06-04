# 02-tier1-foundation: Upgrade Tier 1 foundation libraries

Upgrade the four leaf-level projects that have no internal project dependencies. All four are already on `net8.0` or `net8.0-windows*`; the change is a TFM bump to `net10.0` / `net10.0-windows*` plus any package updates required to compile.

Projects in scope:
- `VisualHFT.Commons\VisualHFT.Commons.csproj` — net8.0 → net10.0. Assessment flags source-incompatible API (Api.0002). Fix inline if it prevents compilation.
- `VisualHFT.Plugins\MarketConnectors.BaseDAL\BitStamp.Net\BitStamp.Net.csproj` — net8.0 → net10.0. Behavioral change (Api.0003) only.
- `VisualHFT.Plugins\MarketConnectors.BaseDAL\Gemini.Net\Gemini.Net.csproj` — net8.0 → net10.0. Source-incompatible API (Api.0002) flagged — fix inline if compilation fails.
- `C:\MyFiles\Development\oxyplot\Source\OxyPlot\OxyPlot.csproj` — net8.0-windows8.0 → net10.0-windows. Source-incompatible API (Api.0002) flagged — fix inline if compilation fails.

After upgrading, restore and build these four projects in isolation before proceeding.

**Done when**: All four Tier 1 projects build successfully on net10.0. No compilation errors. Higher tiers (Tier 2+) still build on their old framework (net8.0).
