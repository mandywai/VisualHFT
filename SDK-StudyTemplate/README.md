# Study Plugin Template

A working scaffold for a VisualHFT study (analytic / indicator) plugin.
Compiles against the current `VisualHFT.Commons` API out of the box — clone,
fill in the `TODO`s with your calculation, build, and the main app will pick
it up.

For a deeper walkthrough see [`StudySDK_Guidelines.md`](StudySDK_Guidelines.md).

## Layout

```
SDK-StudyTemplate/
├── TemplateStudyPlugin.cs           # Plugin class (extends BasePluginStudy)
├── Study.Template.csproj            # .NET 10 / WPF class library
├── Model/PlugInSettings.cs          # Persisted settings (implements ISetting)
├── ViewModels/
│   ├── PluginSettingsViewModel.cs   # Settings UI ViewModel
│   └── TemplateStudyViewModel.cs    # Optional: custom visualization VM
├── UserControls/
│   ├── PluginSettingsView.xaml(.cs) # Settings UI
│   └── TemplateStudyView.xaml(.cs)  # Optional: custom visualization UI
└── StudySDK_Guidelines.md
```

The scaffold gives you the `BasePluginStudy` overrides, an
`HelperOrderBook.Instance` subscription, a non-blocking `HelperCustomQueue`
for processing snapshots off the data-feed thread, an `onDataAggregation`
hook, alert wiring via `OnAlertTriggered`, and a complete MVVM settings UI.

## How a study plugin works

`BasePluginStudy` provides the plumbing; you provide the math.

- The framework calls `LoadSettings()` on startup; you populate `_settings`.
- `StartAsync()` subscribes to `HelperOrderBook.Instance` (or
  `HelperTrade.Instance`, etc.) and enqueues snapshots onto a
  `HelperCustomQueue<T>` so the calculation runs off the data-feed thread.
- Inside your queue handler, compute a value and publish it by calling
  `AddCalculation(new BaseStudyModel { Value = ..., Timestamp = ..., MarketMidPrice = ... })`.
  Do **not** raise `OnCalculated` yourself — `AddCalculation` does that for
  you and applies any aggregation configured in
  `_settings.AggregationLevel`.
- Override `onDataAggregation(...)` to control how successive values are
  merged when aggregation is enabled (e.g. last-value-wins, sum, average).
- Fire `OnAlertTriggered?.Invoke(this, value)` when an alert condition is met.

## Build your study

1. **Copy** this folder next to the other studies:
   `VisualHFT.Plugins/Studies.<YourStudy>/`.
2. **Rename** the project, namespace, and `TemplateStudyPlugin` class to
   match your study (e.g. `Studies.MyIndicator` / `MyIndicatorStudy`).
3. **Update** `Name`, `Version`, `Description`, `Author`, `TileTitle`, and
   `TileToolTip` in the plugin class — these surface in VisualHFT's UI.
4. **Edit `PlugInSettings.cs`** to add your study's parameters (lookback
   period, thresholds, smoothing flags, etc.) and remove the
   `CustomParameter1` / `CustomParameter2` placeholders.
5. **Fill in `QUEUE_onRead(OrderBookSnapshot)`** with your calculation.
   Always `snapshot.Dispose()` at the end so its pooled arrays are returned.
6. **Adjust `onDataAggregation`** if your study needs anything other than
   last-value-wins aggregation.
7. **Update the settings UI** — `PluginSettingsView.xaml` plus its ViewModel
   — to surface your new parameters.

Publishing a value looks like this:

```csharp
private void QUEUE_onRead(OrderBookSnapshot snapshot)
{
    if (snapshot.Bids.Length == 0 || snapshot.Asks.Length == 0)
    {
        snapshot.Dispose();
        return;
    }

    double value = YourCalculation(snapshot);

    AddCalculation(new BaseStudyModel
    {
        Value          = (decimal)value,
        Timestamp      = HelperTimeProvider.Now,
        MarketMidPrice = (decimal)((snapshot.Asks[0].Price + snapshot.Bids[0].Price) / 2.0)
    });

    if (value > _settings.AlertThreshold)
        OnAlertTriggered?.Invoke(this, (decimal)value);

    snapshot.Dispose();
}
```

`BaseStudyModel` only carries `Value`, `Timestamp`, `MarketMidPrice` (and a
few flags). The symbol and provider come from `_settings`, not the model.

## Load it into VisualHFT

At runtime, `VisualHFT.PluginManager.PluginManager.LoadPlugins()` scans the
app's base directory for any `*.dll` that exposes a non-abstract type
implementing `IPlugin` and instantiates it. There is no separate plugins
folder — the DLL just needs to land next to `VisualHFT.exe`.

The canonical way to get it there is to add a `ProjectReference` to the main
app's `VisualHFT.csproj`:

```xml
<ProjectReference Include="VisualHFT.Plugins\Studies.MyIndicator\Studies.MyIndicator.csproj" />
```

(See `VisualHFT.csproj` for the existing entries — LOBImbalance,
MarketResilience, VPIN, OTT_Ratio.) Rebuild the solution and your study
appears in VisualHFT's study list and can be added to any dashboard. No
manual DLL copying, no registration step.

## Reference implementations

When in doubt, copy the closest match:

- `VisualHFT.Plugins/Studies.LOBImbalance/` — the simplest study, ratio
  computed per order-book update.
- `VisualHFT.Plugins/Studies.VPIN/` — windowed statistical aggregation.
- `VisualHFT.Plugins/Studies.MarketResilience/` — multi-feed study with
  custom visualization.
