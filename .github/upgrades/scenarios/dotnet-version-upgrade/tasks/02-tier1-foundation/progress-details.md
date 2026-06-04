# Progress Details — 02-tier1-foundation

## What Was Done

### TFM Changes
- `VisualHFT.Commons\VisualHFT.Commons.csproj`: `net8.0` → `net10.0`
- `VisualHFT.Plugins\MarketConnectors.BaseDAL\BitStamp.Net\BitStamp.Net.csproj`: `net8.0` → `net10.0`
- `VisualHFT.Plugins\MarketConnectors.BaseDAL\Gemini.Net\Gemini.Net.csproj`: `net8.0` → `net10.0`
- `../oxyplot/Source/OxyPlot/OxyPlot.csproj`: `net8.0-windows8.0` → `net10.0-windows10.0.17763.0`

### Additional Change
- `../oxyplot/Source/OxyPlot/OxyPlot.csproj`: Added `<AllowMissingPrunePackageData>true</AllowMissingPrunePackageData>` to work around NETSDK1226 error in .NET 10 preview SDK. This property will likely be needed in all other oxyplot projects too.

## Build Results
- **VisualHFT.Commons**: ✅ 0 errors, 376 warnings (CA1416 Windows-platform guards, CA2265 Span null comparison — pre-existing, not introduced by this upgrade)
- **BitStamp.Net**: ✅ 0 errors, 17 warnings (CS8618 nullable, CS8600/CS8603 — pre-existing)
- **Gemini.Net**: ✅ 0 errors, 31 warnings (CS8618, CS0067 unused events — pre-existing)
- **OxyPlot**: ✅ 0 errors, 483 warnings (CS8618, CS8625 nullable — pre-existing)

## Issues Encountered
- NETSDK1226 on OxyPlot (preview SDK issue) — resolved with `AllowMissingPrunePackageData`. Will need to apply to other oxyplot projects in subsequent tiers.
- All API.0002/0003 assessment flags are `Severity=Potential` — none prevented compilation.
- Warnings are pre-existing from the net8.0 codebase; not introduced by this upgrade. Per "minimal migration" policy, no code changes made.
