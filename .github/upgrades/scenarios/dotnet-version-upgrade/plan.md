# .NET Version Upgrade Plan

## Overview

**Target**: Upgrade all 25 projects in VisualHFT solution from `net8.0*` / `net472` to `net10.0` / `net10.0-windows*`.
**Scope**: Large solution — 25 projects across two repos (VisualHFT + OxyPlot), 4-tier dependency graph, WPF-heavy, one .NET Framework project.

### Selected Strategy
**Bottom-Up (Dependency-First)** — Upgrade from leaf nodes to root applications, tier by tier.
**Rationale**: 1 .NET Framework project (demoTradingCore, net472) triggers mandatory Bottom-Up for Framework→modern migration with 25 projects across a 4-tier dependency graph.

### Dependency Graph

```
Tier 4: [VisualHFT.TriggerService.TestingFramework]
			 ↓
Tier 3: [VisualHFT (main app)]  [VisualHFT.DataRetriever.TestingFramework]  [Studies.MarketResilience.Test]  [demoTradingCore*]  [MarketConnectors.WebSocket]
			 ↓
Tier 2: [MarketConnectors.Binance]  [MarketConnectors.Bitfinex]  [MarketConnectors.BitStamp]
		[MarketConnectors.Coinbase]  [MarketConnectors.Gemini]   [MarketConnectors.Kraken]
		[MarketConnectors.KuCoin]   [VisualHFT.Commons.WPF]
		[OxyPlot.SkiaSharp.Wpf]  [OxyPlot.Wpf.Shared]  [OxyPlot.SkiaSharp]
		[Studies.LOBImbalance]  [Studies.MarketResilience]  [Studies.OTT_Ratio]  [Studies.VPIN]
			 ↓
Tier 1: [VisualHFT.Commons]  [BitStamp.Net]  [Gemini.Net]  [OxyPlot]

* demoTradingCore has no project dependencies — standalone WinForms app on net472
```

---

## Tasks

### 01-prerequisites: Verify SDK and toolchain

Verify that .NET 10 SDK is installed on the machine and that any `global.json` files in both repos are compatible with net10.0. Both the VisualHFT repo and the OxyPlot repo should be checked. This is a zero-code-change task — only toolchain validation.

**Done when**: `dotnet --list-sdks` shows a .NET 10 SDK installed; no `global.json` file pins to a version incompatible with net10.0; both repos confirm ready to build with net10.0 toolchain.

---

### 02-tier1-foundation: Upgrade Tier 1 foundation libraries

Upgrade the four leaf-level projects that have no internal project dependencies. All four are already on `net8.0` or `net8.0-windows*`; the change is a TFM bump to `net10.0` / `net10.0-windows*` plus any package updates required to compile.

Projects in scope:
- `VisualHFT.Commons\VisualHFT.Commons.csproj` — net8.0 → net10.0. Assessment flags source-incompatible API (Api.0002). Fix inline if it prevents compilation.
- `VisualHFT.Plugins\MarketConnectors.BaseDAL\BitStamp.Net\BitStamp.Net.csproj` — net8.0 → net10.0. Behavioral change (Api.0003) only.
- `VisualHFT.Plugins\MarketConnectors.BaseDAL\Gemini.Net\Gemini.Net.csproj` — net8.0 → net10.0. Source-incompatible API (Api.0002) flagged — fix inline if compilation fails.
- `C:\MyFiles\Development\oxyplot\Source\OxyPlot\OxyPlot.csproj` — net8.0-windows8.0 → net10.0-windows. Source-incompatible API (Api.0002) flagged — fix inline if compilation fails.

After upgrading, restore and build these four projects in isolation before proceeding.

**Done when**: All four Tier 1 projects build successfully on net10.0. No compilation errors. Higher tiers (Tier 2+) still build on their old framework (net8.0).

---

### 03-tier2-connectors-wpf: Upgrade Tier 2 — market connectors, WPF shared, OxyPlot layers, Studies

Upgrade all Tier 2 projects that depend only on Tier 1. This is the largest tier — 15 projects. All are on net8.0/net8.0-windows*; the bulk of issues are WPF behavioral changes (Api.0003) and binary-incompatible APIs (Api.0001). Most are WPF plugin projects that share the same TFM pattern.

Projects in scope (grouped by concern):

**OxyPlot layers** (C:\MyFiles\Development\oxyplot\Source\):
- `OxyPlot.SkiaSharp.csproj` — net8.0-windows → net10.0-windows
- `OxyPlot.Wpf.Shared.csproj` — net8.0-windows8.0 → net10.0-windows. 1,269 mandatory issues (WPF printing/XAML infrastructure) — the largest single-project risk. Fix inline anything that blocks compilation.
- `OxyPlot.SkiaSharp.Wpf.csproj` — net8.0-windows → net10.0-windows. 120 mandatory issues (WPF).

**VisualHFT WPF shared**:
- `VisualHFT.Commons.WPF\VisualHFT.Commons.WPF.csproj` — net8.0-windows → net10.0-windows. 52 mandatory issues (binary and source incompatible APIs).

**Market connector plugins** (VisualHFT.Plugins\):
- `MarketConnectors.Binance`, `MarketConnectors.Bitfinex`, `MarketConnectors.BitStamp`, `MarketConnectors.Coinbase`, `MarketConnectors.Gemini`, `MarketConnectors.Kraken`, `MarketConnectors.KuCoin` — all net8.0-windows* → net10.0-windows*. Each has ~22 mandatory issues (Api.0001 binary incompatible).

**Studies plugins** (VisualHFT.Plugins\):
- `Studies.LOBImbalance`, `Studies.MarketResilience`, `Studies.OTT_Ratio`, `Studies.VPIN` — net8.0-windows* → net10.0-windows*. Each ~18-19 mandatory issues (Api.0001).

Build and validate the entire tier after all project files are updated. Run tests for any test projects in this tier.

**Done when**: All 15 Tier 2 projects build successfully on net10.0-windows*. No compilation errors. Tier 3 projects still build on net8.0.

---

### 04-tier3-apps: Upgrade Tier 3 — standalone apps and testing frameworks

Upgrade the five Tier 3 projects. These include the WinForms .NET Framework project (`demoTradingCore`), the main testing frameworks, and the `MarketConnectors.WebSocket` standalone app.

Projects in scope:
- `demoTradingCore\demoTradingCore.csproj` — **net472 → net10.0-windows** (WinForms). The only .NET Framework project. Assessment flags NuGet.0003 (package functionality now included in framework — remove the package reference). In-place approach.
- `MarketConnectors.WebSocket\MarketConnectors.WebSocket.csproj` — net8.0-windows* → net10.0-windows. 8 mandatory issues (source and binary incompatible APIs, behavioral).
- `VisualHFT.DataRetriever.TestingFramework\VisualHFT.DataRetriever.TestingFramework.csproj` — net8.0-windows* → net10.0-windows. 1 mandatory issue (source incompatible API).
- `VisualHFT.TriggerService.TestingFramework\VisualHFT.TriggerService.TestingFramework.csproj` — net10.0-windows* → confirmed. 1 mandatory issue.
- `Studies.MarketResilience.Test\Studies.MarketResilience.Test.csproj` — net8.0-windows* → net10.0-windows. 1 mandatory issue.

**Done when**: All 5 Tier 3 projects build on net10.0 / net10.0-windows. The `demoTradingCore` .NET Framework → .NET 10 migration compiles cleanly. Tests in testing framework projects pass.

---

### 05-tier4-main-app: Upgrade Tier 4 — main VisualHFT application

Upgrade the root application project and the final testing framework that wraps it.

Projects in scope:
- `VisualHFT.csproj` — net8.0-windows10.0.22621.0 → net10.0-windows10.0.22621.0. 2,035 mandatory issues — the highest count in the solution, dominated by WPF/XAML behavioral changes, binary-incompatible APIs (Api.0001), source-incompatible APIs (Api.0002), and one incompatible NuGet package (NuGet.0001). The NuGet.0001 package must be resolved inline — identify the package, find a net10.0-compatible version or remove it. Fix compilation failures inline; behavioral-only issues that don't block compilation are acceptable per the minimal migration approach.
- `VisualHFT.TriggerService.TestingFramework\VisualHFT.TriggerService.TestingFramework.csproj` — net8.0-windows10.0.22621.0 → net10.0-windows10.0.22621.0. 1 mandatory issue.

This is the highest-risk task due to the large issue count in the main app. Take a methodical approach: update TFM and packages first, then iteratively fix compilation errors before addressing warnings.

**Done when**: Full solution builds without errors on net10.0. All tests pass. No incompatible NuGet packages remain. Solution is ready for user review and manual commit.

---

### 06-solution-validation: Full solution validation and cleanup

Perform a final end-to-end build and test run of the entire solution on net10.0. Address any remaining compilation warnings in modified projects (treat warnings as errors per workflow rules). Document any deferred items such as post-migration CPM adoption recommendation.

**Done when**: `dotnet build` on the full solution succeeds with zero errors and zero warnings in modified projects. All unit tests pass. execution-log.md updated. Post-migration recommendations (CPM, nullable reference types) documented for user's reference.
