# Market Connector Template

A working scaffold for a VisualHFT market-data connector. Compiles against
the current `VisualHFT.Commons` API out of the box — clone, fill in the
`TODO`s for your exchange, build, and the main app will pick it up.

For a deeper walkthrough see [`MarketConnectorSDK_Guidelines.md`](MarketConnectorSDK_Guidelines.md).

## Layout

```
SDK-MarketConnectorTemplate/
├── TemplateExchangePlugin.cs        # Plugin class (extends BasePluginDataRetriever)
├── MarketConnector.Template.csproj  # .NET 10 / WPF class library
├── Model/PlugInSettings.cs          # Persisted settings (implements ISetting)
├── ViewModels/PluginSettingsViewModel.cs
├── UserControls/PluginSettingsView.xaml(.cs)
├── SampleMessages/                  # Reference JSON payloads
└── MarketConnectorSDK_Guidelines.md
```

The scaffold gives you the `BasePluginDataRetriever` overrides, MVVM settings
UI with `IDataErrorInfo` validation, `SetReconnectionAction(...)` reconnect
wiring, and a complete `ISetting` implementation. JSON parsing is deliberately
left out — every exchange's payload shape is different.

## Build your connector

1. **Copy** this folder next to the other connectors:
   `VisualHFT.Plugins/MarketConnectors.<YourExchange>/`.
2. **Rename** the project, namespace, and `TemplateExchangePlugin` class to
   match your exchange (e.g. `MarketConnectors.MyExchange` /
   `MyExchangePlugin`).
3. **Pick a unique `ProviderID`** in `InitializeDefaultSettings()` (don't
   collide with the existing providers in `VisualHFT.Plugins/MarketConnectors.*`).
4. **Add your exchange's client library** as a NuGet `PackageReference` in
   the `.csproj`, or implement your own WebSocket/REST client.
5. **Fill in `InternalStartAsync()`** — subscribe to order-book and trade
   feeds for every symbol returned by `GetAllNonNormalizedSymbols()`, and
   call `GetNormalizedSymbol(raw)` to map exchange-native symbols to
   VisualHFT's normalized form.
6. **Fill in `StopAsync()`** — unsubscribe and dispose your client.
7. **In your message callbacks**, build `VisualHFT.Model.OrderBook` /
   `Trade` / `Order` instances and publish them via `RaiseOnDataReceived(...)`.

Publishing a trade looks like this:

```csharp
var trade = new VisualHFT.Model.Trade
{
    ProviderId   = _settings.Provider.ProviderID,
    ProviderName = _settings.Provider.ProviderName,
    Symbol       = normalizedSymbol,
    Price        = (double)raw.Price,
    Size         = (double)raw.Quantity,
    IsBuy        = raw.Side == "buy",
    Timestamp    = DateTime.UtcNow
};
RaiseOnDataReceived(trade);
```

For order books, maintain a local `VisualHFT.Model.OrderBook` per symbol and
apply deltas via `OrderBook.AddOrUpdateLevel(DeltaBookItem)` /
`OrderBook.DeleteLevel(DeltaBookItem)`. See `MarketConnectors.Bitfinex` for a
full reference.

## Load it into VisualHFT

At runtime, `VisualHFT.PluginManager.PluginManager.LoadPlugins()` scans the
app's base directory for any `*.dll` that exposes a non-abstract type
implementing `IPlugin` and instantiates it. There is no separate plugins
folder — the DLL just needs to land next to `VisualHFT.exe`.

The canonical way to get it there is to add a `ProjectReference` to the main
app's `VisualHFT.csproj`:

```xml
<ProjectReference Include="VisualHFT.Plugins\MarketConnectors.MyExchange\MarketConnectors.MyExchange.csproj" />
```

(See `VisualHFT.csproj` for the existing entries — Binance, Bitfinex, Kraken,
etc.) Rebuild the solution and your plugin appears in VisualHFT's connector
list automatically. No manual DLL copying, no registration step.

## Reference implementations

When in doubt, copy the closest match:

- `VisualHFT.Plugins/MarketConnectors.Bitfinex/` — small, REST + WebSocket, easy to read.
- `VisualHFT.Plugins/MarketConnectors.Binance/` — uses a typed client SDK.
- `VisualHFT.Plugins/MarketConnectors.Kraken/` — direct WebSocket parsing.
