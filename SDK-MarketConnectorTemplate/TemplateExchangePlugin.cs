using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using log4net;
using MarketConnector.Template.Model;
using MarketConnector.Template.UserControls;
using MarketConnector.Template.ViewModels;
using VisualHFT.Commons.PluginManager;
using VisualHFT.Enums;
using VisualHFT.Model;
using VisualHFT.PluginManager;
using VisualHFT.UserSettings;

namespace MarketConnector.Template
{
    // -----------------------------------------------------------------------
    // Market Connector scaffold for VisualHFT.
    //
    // To turn this into a working connector for a real exchange:
    //   1. Add a NuGet reference for your exchange's client SDK (e.g.
    //      Binance.Net, Bitfinex.Net, KrakenExchange.Net, etc.) — or implement
    //      your own WebSocket / REST client.
    //   2. Fill in InternalStartAsync() with subscription + snapshot logic.
    //   3. Convert each incoming message into a VisualHFT.Model.OrderBook,
    //      Trade, or Order and publish via RaiseOnDataReceived(...).
    //   4. Update Provider.ProviderID / ProviderName in InitializeDefaultSettings()
    //      to a stable, unique value for your exchange.
    //
    // Compare against the existing Binance / Bitfinex / Kraken plugins under
    // VisualHFT.Plugins/MarketConnectors.* for full reference implementations.
    // -----------------------------------------------------------------------
    public class TemplateExchangePlugin : BasePluginDataRetriever
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(TemplateExchangePlugin));

        private PlugInSettings _settings = new PlugInSettings();
        private new bool _disposed;

        public override string Name { get; set; } = "Template Exchange Plugin";
        public override string Version { get; set; } = "0.1.0";
        public override string Description { get; set; } = "Scaffold connector — replace with your exchange logic.";
        public override string Author { get; set; } = "VisualHFT Community";
        public override ISetting Settings
        {
            get => _settings;
            set => _settings = (PlugInSettings)value;
        }
        public override Action CloseSettingWindow { get; set; } = () => { };

        public TemplateExchangePlugin()
        {
            // Wire automatic reconnection. The base class invokes
            // InternalStartAsync() whenever HandleConnectionLost() fires.
            SetReconnectionAction(InternalStartAsync);
            log.Info($"{Name} has been loaded.");
        }

        ~TemplateExchangePlugin()
        {
            Dispose(false);
        }

        public override async Task StartAsync()
        {
            await base.StartAsync(); // sets Status = STARTING

            // TODO: instantiate your exchange client(s) here using _settings.
            //       Example:
            //   _socketClient = new MyExchangeSocketClient(_settings.ApiKey, _settings.ApiSecret);
            //   _restClient   = new MyExchangeRestClient(_settings.ApiKey, _settings.ApiSecret);

            RaiseOnDataReceived(GetProviderModel(eSESSIONSTATUS.CONNECTING));

            try
            {
                await InternalStartAsync();
                if (Status == ePluginStatus.STOPPED_FAILED)
                    return;
            }
            catch (Exception ex)
            {
                LogException(ex);
                await HandleConnectionLost(ex.Message, ex);
            }
        }

        // The reconnection-aware startup body. Do NOT call this directly from
        // outside StartAsync — the base class wires it as the reconnect target.
        private async Task InternalStartAsync()
        {
            // TODO: subscribe to order-book and trade feeds for every configured symbol.
            //       Use GetAllNonNormalizedSymbols() to iterate over the raw exchange
            //       symbols, and GetNormalizedSymbol(raw) to resolve VisualHFT's
            //       normalized representation (e.g. BTC/USD).
            foreach (var rawSymbol in GetAllNonNormalizedSymbols())
            {
                var normalized = GetNormalizedSymbol(rawSymbol);
                log.Info($"{Name}: subscribing {normalized} (raw={rawSymbol})");

                // Example shape — replace with real subscription calls:
                //   await _socketClient.SubscribeToOrderBookUpdatesAsync(rawSymbol,
                //       _settings.DepthLevels, update => OnOrderBookUpdate(update, normalized));
                //   await _socketClient.SubscribeToTradeUpdatesAsync(rawSymbol,
                //       trade => OnTrade(trade, normalized));
            }

            await Task.CompletedTask;

            // Tell VisualHFT the provider is live, then mark the plugin as STARTED.
            RaiseOnDataReceived(GetProviderModel(eSESSIONSTATUS.CONNECTED));
            Status = ePluginStatus.STARTED;
            log.Info($"{Name} started.");
        }

        public override async Task StopAsync()
        {
            Status = ePluginStatus.STOPPING;
            log.Info($"{Name} is stopping.");

            // TODO: unsubscribe and dispose any open WebSocket / REST clients.
            //   await _socketClient?.UnsubscribeAllAsync();
            //   _socketClient?.Dispose();
            //   _restClient?.Dispose();

            // Clear any provider-specific order books from VisualHFT's UI, then
            // tell the framework the provider is disconnected.
            RaiseOnDataReceived(new List<OrderBook>());
            RaiseOnDataReceived(GetProviderModel(eSESSIONSTATUS.DISCONNECTED));

            await base.StopAsync();
        }

        // -------------------------------------------------------------------
        // Conversion helpers (templates — adapt to your exchange's payloads).
        // -------------------------------------------------------------------

        // Example of how to publish a trade. Build a VisualHFT.Model.Trade and
        // hand it to the base class via RaiseOnDataReceived.
        private void OnTrade(/* raw exchange trade payload */ object rawTrade, string normalizedSymbol)
        {
            var trade = new Trade
            {
                ProviderId = _settings.Provider.ProviderID,
                ProviderName = _settings.Provider.ProviderName,
                Symbol = normalizedSymbol,
                // Price       = (double) rawTrade.Price,
                // Size        = (double) rawTrade.Quantity,
                // IsBuy       = rawTrade.Side == "buy",
                Timestamp = DateTime.UtcNow
            };
            RaiseOnDataReceived(trade);
        }

        // Example of how to publish order-book deltas. See Bitfinex/Binance for
        // how to maintain a local OrderBook per symbol and apply deltas using
        // OrderBook.AddOrUpdateLevel(DeltaBookItem) / DeleteLevel(DeltaBookItem).
        private void OnOrderBookUpdate(/* raw exchange book update */ object rawUpdate, string normalizedSymbol)
        {
            // TODO: parse rawUpdate into one or more DeltaBookItem instances
            //       and apply them to a per-symbol VisualHFT.Model.OrderBook
            //       instance, then call RaiseOnDataReceived(orderBook).
        }

        // -------------------------------------------------------------------
        // ISetting / persistence overrides.
        // -------------------------------------------------------------------

        protected override void LoadSettings()
        {
            var loaded = LoadFromUserSettings<PlugInSettings>();
            if (loaded == null)
            {
                InitializeDefaultSettings();
            }
            else
            {
                _settings = loaded;
            }
            if (_settings.Provider == null)
            {
                // Back-compat for settings files written before Provider existed.
                _settings.Provider = new Provider
                {
                    ProviderID = 999,
                    ProviderName = "TemplateExchange"
                };
            }

            // Feed the symbol list into VisualHFT's normalization pipeline.
            // Accepted formats per entry:
            //   "BTCUSDT"                  → no normalization
            //   "BTCUSDT(BTC/USD)"         → raw=BTCUSDT, normalized=BTC/USD
            if (_settings.Symbols != null && _settings.Symbols.Count > 0)
            {
                ParseSymbols(string.Join(',', _settings.Symbols));
            }
        }

        protected override void SaveSettings()
        {
            SaveToUserSettings(_settings);
        }

        protected override void InitializeDefaultSettings()
        {
            _settings = new PlugInSettings
            {
                ApiKey = string.Empty,
                ApiSecret = string.Empty,
                DepthLevels = 20,
                Provider = new Provider
                {
                    ProviderID = 999,                  // TODO: pick a unique ID for your exchange
                    ProviderName = "TemplateExchange"  // TODO: replace with your exchange name
                },
                Symbols = new List<string> { "BTCUSDT(BTC/USD)" },
                AggregationLevel = AggregationLevel.Ms100
            };
            SaveToUserSettings(_settings);
        }

        public override object GetUISettings()
        {
            var view = new PluginSettingsView();
            var viewModel = new PluginSettingsViewModel(CloseSettingWindow)
            {
                ApiKey = _settings.ApiKey,
                ApiSecret = _settings.ApiSecret,
                DepthLevels = _settings.DepthLevels,
                ProviderId = _settings.Provider.ProviderID,
                ProviderName = _settings.Provider.ProviderName,
                Symbols = _settings.Symbols
            };

            viewModel.UpdateSettingsFromUI = () =>
            {
                _settings.ApiKey = viewModel.ApiKey;
                _settings.ApiSecret = viewModel.ApiSecret;
                _settings.DepthLevels = viewModel.DepthLevels;
                _settings.Provider = new Provider
                {
                    ProviderID = viewModel.ProviderId,
                    ProviderName = viewModel.ProviderName
                };
                _settings.Symbols = viewModel.Symbols;
                SaveSettings();
                ParseSymbols(string.Join(',', _settings.Symbols));

                // Reload connection with the new values.
                RaiseOnDataReceived(GetProviderModel(eSESSIONSTATUS.CONNECTING));
                Status = ePluginStatus.STARTING;
                Task.Run(async () => await HandleConnectionLost(
                    $"{Name} is restarting after settings change.", null!, true));
            };

            view.DataContext = viewModel;
            return view;
        }

        protected override void Dispose(bool disposing)
        {
            if (_disposed) return;
            _disposed = true;

            if (disposing)
            {
                // TODO: dispose your exchange client(s), timers, queues, etc.
                base.Dispose();
            }
        }
    }
}
