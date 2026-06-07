using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VisualHFT.Commons.PluginManager;
using VisualHFT.Commons.Studies;
using VisualHFT.Enums;
using VisualHFT.Helpers;
using VisualHFT.Model;
using VisualHFT.PluginManager;
using VisualHFT.Studies.VPINMulti.Model;
using VisualHFT.Studies.VPINMulti.UserControls;
using VisualHFT.Studies.VPINMulti.ViewModel;
using VisualHFT.UserSettings;

namespace VisualHFT.Studies
{
    public class VPINMultiStudy : BasePluginMultiStudy
    {
        private const string ValueFormat = "N2";
        private const string ColorGreen = "Green";
        private const string ColorWhite = "White";
        private const int DefaultNumberOfBuckets = 50;
        private readonly object _sync = new object();
        private PlugInSettings _settings;

        public override string Name { get; set; } = "VPIN Multi Study Plugin";
        public override string Version { get; set; } = "1.0.0";
        public override string Description { get; set; } = "Runs multiple VPIN profiles with different bucket settings on the same provider and symbol.";
        public override string Author { get; set; } = "VisualHFT";
        public override ISetting Settings { get => _settings; set => _settings = (PlugInSettings)value; }
        public override ePluginStatus Status { get; set; }
        public override Action CloseSettingWindow { get; set; }
        public override string TileTitle { get; set; } = "VPIN Multi";
        public override string TileToolTip { get; set; } = "Runs multiple named VPIN configurations side by side so each profile can be charted and recorded independently.";
        public override List<IStudy> Studies { get; set; } = new List<IStudy>();

        public override async Task StartAsync()
        {
            await base.StartAsync();
            BuildStudies();
            foreach (var study in Studies.OfType<VpinProfileStudy>())
            {
                await study.StartAsync();
            }

            HelperOrderBook.Instance.Subscribe(OnOrderBookReceived);
            HelperTrade.Instance.Subscribe(OnTradeReceived);

            Status = ePluginStatus.STARTED;
            log.Info($"{Name} started with {Studies.Count} profile(s).");
        }

        public override async Task StopAsync()
        {
            Status = ePluginStatus.STOPPING;
            HelperOrderBook.Instance.Unsubscribe(OnOrderBookReceived);
            HelperTrade.Instance.Unsubscribe(OnTradeReceived);

            foreach (var study in Studies.OfType<VpinProfileStudy>())
            {
                await study.StopAsync();
            }

            await base.StopAsync();
            Status = ePluginStatus.STOPPED;
        }

        protected override void LoadSettings()
        {
            _settings = LoadFromUserSettings<PlugInSettings>();
            if (_settings == null)
                InitializeDefaultSettings();
            if (_settings.Provider == null)
                _settings.Provider = new Provider();
            _settings.Profiles ??= new List<VpinProfileSettings>();
            _settings.AggregationLevel = AggregationLevel.S1;
            NormalizeProfiles();
            BuildStudies();
        }

        protected override void SaveSettings()
        {
            SaveToUserSettings(_settings);
        }

        protected override void InitializeDefaultSettings()
        {
            _settings = new PlugInSettings
            {
                Symbol = string.Empty,
                Provider = new Provider(),
                AggregationLevel = AggregationLevel.S1,
                Profiles = new List<VpinProfileSettings>
                {
                    new VpinProfileSettings
                    {
                        Name = VpinProfileNaming.FormatDisplayName(DefaultNumberOfBuckets, 1),
                        BucketVolSize = 1,
                        NumberOfBuckets = DefaultNumberOfBuckets
                    }
                }
            };
            SaveToUserSettings(_settings);
        }

        public override object GetUISettings()
        {
            var view = new PluginSettingsView();
            var viewModel = new PluginSettingsViewModel(CloseSettingWindow);
            viewModel.ApplySettings(_settings);
            viewModel.UpdateSettingsFromUI = () =>
            {
                _settings.Symbol = viewModel.SelectedSymbol;
                _settings.Provider = viewModel.SelectedProvider ?? new Provider();
                _settings.AggregationLevel = viewModel.AggregationLevelSelection;
                _settings.Profiles = viewModel.ToProfileSettings();
                NormalizeProfiles();
                SaveSettings();
                Task.Run(ReloadAfterSettingsChangeAsync);
            };
            view.DataContext = viewModel;
            return view;
        }

        public override object GetCustomUI()
        {
            return null;
        }

        private void NormalizeProfiles()
        {
            foreach (var profile in _settings.Profiles)
            {
                profile.Name = profile.Name?.Trim() ?? string.Empty;
                if (profile.NumberOfBuckets <= 0)
                    profile.NumberOfBuckets = DefaultNumberOfBuckets;
            }
        }

        private void BuildStudies()
        {
            lock (_sync)
            {
                foreach (var study in Studies.OfType<IDisposable>())
                {
                    study.Dispose();
                }

                Studies = (_settings.Profiles ?? new List<VpinProfileSettings>())
                    .Select(x => (IStudy)new VpinProfileStudy(x.BucketVolSize, x.NumberOfBuckets))
                    .ToList();
            }
        }

        private async Task ReloadAfterSettingsChangeAsync()
        {
            if (Status == ePluginStatus.STARTED || Status == ePluginStatus.STARTING)
            {
                await StopAsync();
                await StartAsync();
            }
            else
            {
                BuildStudies();
            }
        }

        private void OnOrderBookReceived(OrderBook e)
        {
            if (e == null)
                return;
            if (_settings.Provider.ProviderID != e.ProviderID || _settings.Symbol != e.Symbol)
                return;

            lock (_sync)
            {
                foreach (var study in Studies.OfType<VpinProfileStudy>())
                {
                    study.UpdateMarketMidPrice((decimal)e.MidPrice);
                }
            }
        }

        private void OnTradeReceived(Trade e)
        {
            if (e == null)
                return;
            if (_settings.Provider.ProviderID != e.ProviderId || _settings.Symbol != e.Symbol)
                return;

            lock (_sync)
            {
                foreach (var study in Studies.OfType<VpinProfileStudy>())
                {
                    study.ProcessTrade(e);
                }
            }
        }

        private sealed class VpinProfileStudy : IStudy
        {
            private readonly decimal[] _bucketImbalances;
            private readonly decimal _bucketVolumeSize;
            private decimal _currentBucketVolume;
            private decimal _lastMarketMidPrice;
            private decimal _currentBuyVolume;
            private decimal _currentSellVolume;
            private int _bufferIndex;
            private int _bufferCount;
            private decimal _rollingSum;

            public event EventHandler<decimal> OnAlertTriggered;
            public event EventHandler<BaseStudyModel> OnCalculated;

            public VpinProfileStudy(double bucketVolumeSize, int numberOfBuckets)
            {
                var safeNumberOfBuckets = numberOfBuckets > 0 ? numberOfBuckets : DefaultNumberOfBuckets;
                var displayName = VpinProfileNaming.FormatDisplayName(safeNumberOfBuckets, bucketVolumeSize);
                TileTitle = displayName;
                TileToolTip = $"VPIN profile {displayName}";
                _bucketVolumeSize = (decimal)bucketVolumeSize;
                _bucketImbalances = new decimal[safeNumberOfBuckets];
                IsChartButtonVisible = false;
                IsSettingsButtonVisisble = false;
                IsFooterVisible = false;
            }

            public string TileTitle { get; set; }
            public string TileToolTip { get; set; }
            public bool EmitsMetric => true;
            public bool IsChartButtonVisible { get; set; }
            public bool IsSettingsButtonVisisble { get; set; }
            public bool IsFooterVisible { get; set; }

            public Task StartAsync()
            {
                Reset();
                EmitCalculation(false);
                return Task.CompletedTask;
            }

            public Task StopAsync()
            {
                return Task.CompletedTask;
            }

            public object GetCustomUI()
            {
                return null;
            }

            public void UpdateMarketMidPrice(decimal midPrice)
            {
                _lastMarketMidPrice = midPrice;
                EmitCalculation(false);
            }

            public void ProcessTrade(Trade e)
            {
                if (_bucketVolumeSize <= 0)
                    return;

                bool isBuy;
                if (_lastMarketMidPrice > 0)
                    isBuy = e.Price >= _lastMarketMidPrice;
                else if (e.IsBuy.HasValue)
                    isBuy = e.IsBuy.Value;
                else
                    return;

                decimal remainingSize = e.Size;
                if (isBuy)
                    _currentBuyVolume += remainingSize;
                else
                    _currentSellVolume += remainingSize;
                _currentBucketVolume += remainingSize;

                while (_currentBucketVolume >= _bucketVolumeSize)
                {
                    decimal bucketOverflow = _currentBucketVolume - _bucketVolumeSize;
                    if (isBuy)
                        _currentBuyVolume -= bucketOverflow;
                    else
                        _currentSellVolume -= bucketOverflow;
                    _currentBucketVolume = _bucketVolumeSize;

                    EmitCalculation(true);

                    _currentBuyVolume = 0;
                    _currentSellVolume = 0;
                    if (isBuy)
                        _currentBuyVolume = bucketOverflow;
                    else
                        _currentSellVolume = bucketOverflow;
                    _currentBucketVolume = bucketOverflow;
                }

                EmitCalculation(false);
            }

            public void Dispose()
            {
            }

            private void Reset()
            {
                _currentBucketVolume = 0;
                _lastMarketMidPrice = 0;
                _currentBuyVolume = 0;
                _currentSellVolume = 0;
                _bufferIndex = 0;
                _bufferCount = 0;
                _rollingSum = 0;
                Array.Clear(_bucketImbalances, 0, _bucketImbalances.Length);
            }

            private void EmitCalculation(bool isNewBucket)
            {
                string valueColor = isNewBucket ? ColorGreen : ColorWhite;

                if (isNewBucket)
                {
                    decimal bucketImbalance = Math.Abs(_currentBuyVolume - _currentSellVolume) / _bucketVolumeSize;
                    if (_bufferCount == _bucketImbalances.Length)
                        _rollingSum -= _bucketImbalances[_bufferIndex];
                    else
                        _bufferCount++;

                    _bucketImbalances[_bufferIndex] = bucketImbalance;
                    _rollingSum += bucketImbalance;
                    _bufferIndex = (_bufferIndex + 1) % _bucketImbalances.Length;
                }

                decimal vpin = _bufferCount > 0 ? _rollingSum / _bufferCount : 0;
                OnCalculated?.Invoke(this, new BaseStudyModel
                {
                    Value = vpin,
                    Format = ValueFormat,
                    Timestamp = HelperTimeProvider.Now,
                    MarketMidPrice = _lastMarketMidPrice,
                    ValueColor = valueColor,
                    AddItemSkippingAggregation = isNewBucket
                });
            }
        }
    }
}
