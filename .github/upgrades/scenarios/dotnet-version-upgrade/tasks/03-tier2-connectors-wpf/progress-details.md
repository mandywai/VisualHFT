# Progress Details — 03-tier2-connectors-wpf

## What Was Done

### TFM Changes (15 projects)

**OxyPlot projects** (C:\MyFiles\Development\oxyplot\Source\):
- `OxyPlot.SkiaSharp.csproj`: `net8.0-windows8.0` → `net10.0-windows10.0.17763.0`
- `OxyPlot.Wpf.Shared.csproj`: `net8.0-windows8.0` → `net10.0-windows10.0.17763.0`
- `OxyPlot.SkiaSharp.Wpf.csproj`: `net8.0-windows8.0` → `net10.0-windows10.0.17763.0`

**VisualHFT WPF shared**:
- `VisualHFT.Commons.WPF\VisualHFT.Commons.WPF.csproj`: `net8.0-windows` → `net10.0-windows`

**Market connector plugins**:
- `MarketConnectors.Binance.csproj`: `net8.0-windows8.0` → `net10.0-windows10.0.17763.0`
- `MarketConnectors.Bitfinex.csproj`: `net8.0-windows8.0` → `net10.0-windows10.0.17763.0`
- `MarketConnectors.BitStamp.csproj`: `net8.0-windows` → `net10.0-windows`
- `MarketConnectors.Coinbase.csproj`: `net8.0-windows` → `net10.0-windows`
- `MarketConnectors.Gemini.csproj`: `net8.0-windows` → `net10.0-windows`
- `MarketConnectors.Kraken.csproj`: `net8.0-windows8.0` → `net10.0-windows10.0.17763.0`
- `MarketConnectors.KuCoin.csproj`: `net8.0-windows8.0` → `net10.0-windows10.0.17763.0`

**Studies plugins**:
- `Studies.LOBImbalance.csproj`: `net8.0-windows8.0` → `net10.0-windows10.0.17763.0`
- `Studies.MarketResilience.csproj`: `net8.0-windows8.0` → `net10.0-windows10.0.17763.0`
- `Studies.OTT_Ratio.csproj`: `net8.0-windows8.0` → `net10.0-windows10.0.17763.0`
- `Studies.VPIN.csproj`: `net8.0-windows8.0` → `net10.0-windows10.0.17763.0`

### Additional Changes
- Added `<AllowMissingPrunePackageData>true</AllowMissingPrunePackageData>` to `OxyPlot.SkiaSharp.csproj`, `OxyPlot.Wpf.Shared.csproj`, `OxyPlot.SkiaSharp.Wpf.csproj` to work around NETSDK1226 preview SDK issue.

## Build Results
All 15 Tier 2 projects: ✅ 0 errors. Warnings are pre-existing nullable/WPF warnings not introduced by this upgrade.

## Issues Encountered
- Regex-based script accidentally inserted duplicate `<TargetFramework>` entries in OxyPlot.SkiaSharp and OxyPlot.SkiaSharp.Wpf — fixed manually.
- Note: `net8.0-windows8.0` → Windows 8 minimum version is not supported in .NET 10 with `windows8.0` suffix; upgraded to `windows10.0.17763.0` which is the minimum supported Windows 10 version.
