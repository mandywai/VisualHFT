# 04-tier3-apps: Upgrade Tier 3 — standalone apps and testing frameworks

Upgrade the five Tier 3 projects. These include the WinForms .NET Framework project (`demoTradingCore`), the main testing frameworks, and the `MarketConnectors.WebSocket` standalone app.

Projects in scope:
- `demoTradingCore\demoTradingCore.csproj` — **net472 → net10.0-windows** (WinForms). The only .NET Framework project. Assessment flags NuGet.0003 (package functionality now included in framework — remove the package reference). In-place approach.
- `MarketConnectors.WebSocket\MarketConnectors.WebSocket.csproj` — net8.0-windows* → net10.0-windows. 8 mandatory issues (source and binary incompatible APIs, behavioral).
- `VisualHFT.DataRetriever.TestingFramework\VisualHFT.DataRetriever.TestingFramework.csproj` — net8.0-windows* → net10.0-windows. 1 mandatory issue (source incompatible API).
- `VisualHFT.TriggerService.TestingFramework\VisualHFT.TriggerService.TestingFramework.csproj` — net10.0-windows* → confirmed. 1 mandatory issue.
- `Studies.MarketResilience.Test\Studies.MarketResilience.Test.csproj` — net8.0-windows* → net10.0-windows. 1 mandatory issue.

**Done when**: All 5 Tier 3 projects build on net10.0 / net10.0-windows. The `demoTradingCore` .NET Framework → .NET 10 migration compiles cleanly. Tests in testing framework projects pass.
