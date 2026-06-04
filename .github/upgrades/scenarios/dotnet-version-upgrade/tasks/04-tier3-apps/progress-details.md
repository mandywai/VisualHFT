# Progress Details — 04-tier3-apps

## What Was Done

### SDK-Style Conversion
- `demoTradingCore\demoTradingCore.csproj`: Converted from old-style (ToolsVersion="15.0") to SDK-style using `convert_project_to_sdk_style` tool.

### TFM Changes
- `demoTradingCore\demoTradingCore.csproj`: `net472` → `net10.0-windows`
- `MarketConnectors.WebSocket.csproj`: `net8.0-windows8.0` → `net10.0-windows10.0.17763.0`
- `VisualHFT.DataRetriever.TestingFramework.csproj`: `net8.0-windows8.0` → `net10.0-windows10.0.17763.0`
- `Studies.MarketResilience.Test.csproj`: `net8.0-windows8.0` → `net10.0-windows10.0.17763.0`
- `VisualHFT.TriggerService.TestingFramework.csproj`: `net8.0-windows10.0.22621.0` → `net10.0-windows10.0.22621.0` (TFM updated, but build deferred — depends on VisualHFT.csproj which is upgraded in task 05)

### Package Changes (demoTradingCore)
- Removed legacy .NET Framework `<Reference>` entries (System.ServiceModel, System.Data.Entity, etc.)
- Removed NuGet.0003 built-in packages: System.Buffers, System.Collections, System.Collections.Concurrent, System.IO, System.Memory, System.Net.Http, System.Numerics.Vectors, System.Runtime, System.Security.Cryptography.*, System.Security.Principal.Windows, System.Threading.Tasks.Extensions, System.ValueTuple
- Removed `<ImportWindowsDesktopTargets>true</ImportWindowsDesktopTargets>` (not needed in SDK-style)

### Code Changes (demoTradingCore — required to compile)
- `Program.cs`: Removed unused `using System.ServiceModel.Channels;` and `using System.Windows.Interop;` — these namespaces do not exist in .NET 10 and were not used anywhere in the file (confirmed via code search).

## Build Results
- **demoTradingCore**: ✅ 0 errors (EF6 6.5.2 is compatible with net10.0-windows)
- **MarketConnectors.WebSocket**: ✅ 0 errors
- **VisualHFT.DataRetriever.TestingFramework**: ✅ 0 errors
- **Studies.MarketResilience.Test**: ✅ 0 errors
- **VisualHFT.TriggerService.TestingFramework**: ⚠️ TFM updated; build deferred — depends on VisualHFT.csproj (Tier 4, task 05). Will be validated in task 06.

## Issues Encountered
- demoTradingCore used EF6 with EDMX model — EF6 6.5.2 is confirmed compatible with net10.0-windows, no changes needed.
- Two unused `using` directives in Program.cs caused compile errors (System.ServiceModel.Channels, System.Windows.Interop). Removed as minimal required fix.
