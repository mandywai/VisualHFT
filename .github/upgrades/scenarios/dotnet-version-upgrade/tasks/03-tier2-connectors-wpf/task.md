# 03-tier2-connectors-wpf: Upgrade Tier 2 — market connectors, WPF shared, OxyPlot layers, Studies

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
