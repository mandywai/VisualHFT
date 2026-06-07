using Newtonsoft.Json;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Collections.Concurrent;
using System.Threading.Channels;
using VisualHFT.Commons.PluginManager;
using VisualHFT.Commons.Studies;
using VisualHFT.Enums;
using VisualHFT.Helpers;
using VisualHFT.Model;
using VisualHFT.PluginManager;
using VisualHFT.Studies.DataRecorder.Model;
using VisualHFT.Studies.DataRecorder.UserControls;
using VisualHFT.Studies.DataRecorder.ViewModel;
using VisualHFT.UserSettings;

namespace VisualHFT.Studies
{
    public class DataRecorderStudy : BasePluginStudy
    {
        private const string RecordsValueFormat = "N0";

        private readonly JsonSerializerSettings _jsonSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore
        };

        private PlugInSettings _settings;
        private int _stopRequested;
        private long _writtenRecords;
        private DateTime _sessionStartedUtc;
        private DateTime? _sessionEndedUtc;
        private string _sessionFolder = string.Empty;
        private string _dataFilePath = string.Empty;
        private string _metadataFilePath = string.Empty;
        private StreamWriter _writer;
        private Channel<RecordEnvelope> _writeChannel;
        private Task _writerTask;
        private Timer _intervalTimer;
        private CancellationTokenSource _durationCts;
        private readonly object _latestStateLock = new object();
        private MarketSnapshot _latestMarketSnapshot;
        private readonly ConcurrentDictionary<string, StudyMetricSnapshot> _latestStudySnapshots = new ConcurrentDictionary<string, StudyMetricSnapshot>();
        private readonly Dictionary<string, EventHandler<BaseStudyModel>> _studyHandlers = new Dictionary<string, EventHandler<BaseStudyModel>>();

        public override event EventHandler<decimal> OnAlertTriggered;

        public override string Name { get; set; } = "Data Recorder Study Plugin";
        public override string Version { get; set; } = "1.0.0";
        public override string Description { get; set; } = "Stores selected market and study data streams to local JSONL files.";
        public override string Author { get; set; } = "VisualHFT";
        public override ISetting Settings { get => _settings; set => _settings = (PlugInSettings)value; }
        public override Action CloseSettingWindow { get; set; }
        public override string TileTitle { get; set; } = "Data Recorder";
        public override string TileToolTip { get; set; } = "Persists selected market and study outputs into streaming JSONL files with a session metadata file at completion.";
        public override bool EmitsMetric => false;
        public override bool AutoStart => false;


        public DataRecorderStudy()
        {
            IsChartButtonVisible = false;
        }

        public static IReadOnlyList<MarketFieldDefinition> GetMarketFieldDefinitions()
        {
            return new List<MarketFieldDefinition>
            {
                new MarketFieldDefinition("best_bid_price", "Best Bid Price"),
                new MarketFieldDefinition("best_bid_size", "Best Bid Size"),
                new MarketFieldDefinition("best_ask_price", "Best Ask Price"),
                new MarketFieldDefinition("best_ask_size", "Best Ask Size"),
                new MarketFieldDefinition("mid_price", "Mid Price"),
                new MarketFieldDefinition("spread", "Spread"),
                new MarketFieldDefinition("imbalance", "Order Book Imbalance"),
                new MarketFieldDefinition("sequence", "Sequence"),
                new MarketFieldDefinition("last_updated", "Market Last Updated"),
                new MarketFieldDefinition("bid_levels", "Bid Levels"),
                new MarketFieldDefinition("ask_levels", "Ask Levels")
            };
        }

        public override async Task StartAsync()
        {
            await base.StartAsync();

            if (_settings.GetConfigurationError() is string configurationError)
                throw new InvalidOperationException(configurationError);

            Interlocked.Exchange(ref _stopRequested, 0);
            Interlocked.Exchange(ref _writtenRecords, 0);
            _latestStudySnapshots.Clear();
            _studyHandlers.Clear();
            _latestMarketSnapshot = null;

            InitializeSessionFiles();
            WriteMetadata();
            StartWriter();
            SubscribeToStreams();
            StartDurationStopIfNeeded();
            StartIntervalTimerIfNeeded();

            AddStatusCalculation("Recording started.");
            Status = ePluginStatus.STARTED;
            log.Info($"{Name} started. Writing to {_dataFilePath}");
        }

        public override async Task StopAsync()
        {
            if (Interlocked.Exchange(ref _stopRequested, 1) == 1)
                return;

            Status = ePluginStatus.STOPPING;

            UnsubscribeFromStreams();
            DisposeTimers();

            if (_settings.CaptureMode == CaptureMode.FixedInterval)
                EnqueueIntervalSnapshot();

            if (_writeChannel != null)
            {
                _writeChannel.Writer.TryComplete();
                if (_writerTask != null)
                    await _writerTask;
            }

            _sessionEndedUtc = DateTime.UtcNow;
            WriteMetadata();

            _writer?.Dispose();
            _writer = null;
            _writeChannel = null;
            _writerTask = null;

            AddStatusCalculation("Recording stopped.");
            await base.StopAsync();
            Status = ePluginStatus.STOPPED;
            log.Info($"{Name} stopped. Metadata written to {_metadataFilePath}");
        }

        protected override void LoadSettings()
        {
            _settings = LoadFromUserSettings<PlugInSettings>();
            if (_settings == null)
                InitializeDefaultSettings();
            if (_settings.Provider == null)
                _settings.Provider = new Provider();
            _settings.SelectedMarketFields ??= new List<string>();
            _settings.SelectedStudyIds ??= new List<string>();
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
                CaptureMode = CaptureMode.OnUpdate,
                RunIndefinitely = true,
                DurationMinutes = 0,
                SelectedMarketFields = new List<string> { "best_bid_price", "best_ask_price", "mid_price", "spread" },
                SelectedStudyIds = new List<string>()
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
                _settings.CaptureMode = viewModel.CaptureModeSelection;
                _settings.OutputFolder = viewModel.OutputFolder;
                _settings.RunIndefinitely = viewModel.RunIndefinitely;
                _settings.DurationMinutes = viewModel.DurationMinutes;
                _settings.SelectedMarketFields = viewModel.MarketFieldOptions.Where(x => x.IsSelected).Select(x => x.Id).ToList();
                _settings.SelectedStudyIds = viewModel.StudyOptions.Where(x => x.IsSelected).Select(x => x.Id).ToList();

                SaveSettings();
                Task.Run(async () => await HandleRestart($"{Name} reloading settings.", null, true));
            };

            view.DataContext = viewModel;
            return view;
        }

        private void InitializeSessionFiles()
        {
            Directory.CreateDirectory(_settings.OutputFolder);

            _sessionStartedUtc = DateTime.UtcNow;
            var safeProvider = MakeSafeFileName(_settings.Provider.ProviderName);
            var safeSymbol = MakeSafeFileName(_settings.Symbol);
            var sessionName = $"{safeProvider}_{safeSymbol}_{_sessionStartedUtc:yyyyMMdd_HHmmss}";
            _sessionFolder = Path.Combine(_settings.OutputFolder, sessionName);
            Directory.CreateDirectory(_sessionFolder);

            _dataFilePath = Path.Combine(_sessionFolder, "data.jsonl");
            _metadataFilePath = Path.Combine(_sessionFolder, "metadata.json");
            _writer = new StreamWriter(new FileStream(_dataFilePath, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                AutoFlush = true
            };
        }

        private void StartWriter()
        {
            _writeChannel = Channel.CreateUnbounded<RecordEnvelope>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            });
            _writerTask = Task.Run(RunWriterAsync);
        }

        private async Task RunWriterAsync()
        {
            await foreach (var record in _writeChannel.Reader.ReadAllAsync())
            {
                var json = JsonConvert.SerializeObject(record.Payload, _jsonSettings);
                await _writer.WriteLineAsync(json);
                Interlocked.Increment(ref _writtenRecords);
                AddStatusCalculation($"Records written: {Interlocked.Read(ref _writtenRecords):N0}");
            }
        }

        private void SubscribeToStreams()
        {
            HelperOrderBook.Instance.Subscribe(OnOrderBookReceived);

            foreach (var descriptorId in _settings.SelectedStudyIds.Distinct())
            {
                var study = ResolveStudyByDescriptorId(descriptorId);
                if (study == null)
                    continue;

                EventHandler<BaseStudyModel> handler = (sender, metric) => OnStudyMetricReceived(descriptorId, study, metric);
                _studyHandlers[descriptorId] = handler;
                study.OnCalculated += handler;
            }
        }

        private void UnsubscribeFromStreams()
        {
            HelperOrderBook.Instance.Unsubscribe(OnOrderBookReceived);

            foreach (var pair in _studyHandlers.ToList())
            {
                var study = ResolveStudyByDescriptorId(pair.Key);
                if (study != null)
                    study.OnCalculated -= pair.Value;
            }
            _studyHandlers.Clear();
        }

        private void StartIntervalTimerIfNeeded()
        {
            if (_settings.CaptureMode != CaptureMode.FixedInterval)
                return;

            var interval = ToTimeSpan(_settings.AggregationLevel);
            if (interval <= TimeSpan.Zero)
                throw new InvalidOperationException("A positive fixed interval is required.");

            _intervalTimer = new Timer(_ => EnqueueIntervalSnapshot(), null, interval, interval);
        }

        private void StartDurationStopIfNeeded()
        {
            if (_settings.RunIndefinitely || _settings.DurationMinutes <= 0)
                return;

            _durationCts = new CancellationTokenSource();
            var token = _durationCts.Token;
            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(_settings.DurationMinutes), token);
                    if (!token.IsCancellationRequested)
                        await StopAsync();
                }
                catch (TaskCanceledException)
                {
                }
            }, token);
        }

        private void DisposeTimers()
        {
            _intervalTimer?.Dispose();
            _intervalTimer = null;

            _durationCts?.Cancel();
            _durationCts?.Dispose();
            _durationCts = null;
        }

        private void OnOrderBookReceived(OrderBook orderBook)
        {
            if (orderBook == null)
                return;
            if (_settings.Provider.ProviderID != orderBook.ProviderID || _settings.Symbol != orderBook.Symbol)
                return;

            var snapshot = CreateMarketSnapshot(orderBook);
            lock (_latestStateLock)
            {
                _latestMarketSnapshot = snapshot;
            }

            if (_settings.CaptureMode == CaptureMode.OnUpdate)
            {
                var row = new Dictionary<string, object>
                {
                    ["row_timestamp_utc"] = DateTime.UtcNow
                };
                AppendSelectedMarketFields(row, snapshot);
                EnqueueRecord(row);
            }
        }

        private void OnStudyMetricReceived(string descriptorId, IStudy study, BaseStudyModel metric)
        {
            if (metric == null)
                return;

            var snapshot = new StudyMetricSnapshot
            {
                DescriptorId = descriptorId,
                DisplayName = study.TileTitle,
                TimestampUtc = metric.Timestamp,
                Value = metric.Value,
                Format = metric.Format,
                MarketMidPrice = metric.MarketMidPrice,
                ValueColor = metric.ValueColor,
                Tag = metric.Tag,
                Tooltip = metric.Tooltip
            };

            _latestStudySnapshots[descriptorId] = snapshot;

            if (_settings.CaptureMode == CaptureMode.OnUpdate)
            {
                var row = new Dictionary<string, object>
                {
                    ["row_timestamp_utc"] = DateTime.UtcNow
                };
                var studyFieldKey = ToStudyFieldKey(study.TileTitle);
                row[studyFieldKey] = metric.Value;
                EnqueueRecord(row);
            }
        }

        private void EnqueueIntervalSnapshot()
        {
            MarketSnapshot marketSnapshot;
            lock (_latestStateLock)
            {
                marketSnapshot = _latestMarketSnapshot;
            }

            var row = new Dictionary<string, object>
            {
                ["row_timestamp_utc"] = DateTime.UtcNow
            };

            if (marketSnapshot != null)
                AppendSelectedMarketFields(row, marketSnapshot);
            AppendSelectedStudyFields(row);

            EnqueueRecord(row);
        }

        private void EnqueueRecord(object payload)
        {
            _writeChannel?.Writer.TryWrite(new RecordEnvelope { Payload = payload });
        }

        private MarketSnapshot CreateMarketSnapshot(OrderBook orderBook)
        {
            var bestBid = orderBook.GetTOB(true);
            var bestAsk = orderBook.GetTOB(false);
            var selectedFields = new HashSet<string>(_settings.SelectedMarketFields ?? new List<string>());

            var snapshot = new MarketSnapshot
            {
                RowTimestampUtc = DateTime.UtcNow,
                BestBidPrice = bestBid?.Price,
                BestBidSize = bestBid?.Size,
                BestAskPrice = bestAsk?.Price,
                BestAskSize = bestAsk?.Size,
                MidPrice = orderBook.MidPrice,
                Spread = orderBook.Spread,
                Imbalance = orderBook.ImbalanceValue,
                Sequence = orderBook.Sequence,
                LastUpdatedUtc = orderBook.LastUpdated
            };

            if (selectedFields.Contains("bid_levels"))
            {
                snapshot.BidLevels = orderBook.Bids?
                    .Select(x => new MarketLevel { Price = x.Price, Size = x.Size })
                    .ToList() ?? new List<MarketLevel>();
            }
            if (selectedFields.Contains("ask_levels"))
            {
                snapshot.AskLevels = orderBook.Asks?
                    .Select(x => new MarketLevel { Price = x.Price, Size = x.Size })
                    .ToList() ?? new List<MarketLevel>();
            }

            return snapshot;
        }

        private void AppendSelectedMarketFields(Dictionary<string, object> row, MarketSnapshot snapshot)
        {
            foreach (var field in _settings.SelectedMarketFields ?? new List<string>())
            {
                switch (field)
                {
                    case "best_bid_price":
                        row["market_best_bid_price"] = snapshot.BestBidPrice;
                        break;
                    case "best_bid_size":
                        row["market_best_bid_size"] = snapshot.BestBidSize;
                        break;
                    case "best_ask_price":
                        row["market_best_ask_price"] = snapshot.BestAskPrice;
                        break;
                    case "best_ask_size":
                        row["market_best_ask_size"] = snapshot.BestAskSize;
                        break;
                    case "mid_price":
                        row["market_mid_price"] = snapshot.MidPrice;
                        break;
                    case "spread":
                        row["market_spread"] = snapshot.Spread;
                        break;
                    case "imbalance":
                        row["market_imbalance"] = snapshot.Imbalance;
                        break;
                    case "sequence":
                        row["market_sequence"] = snapshot.Sequence;
                        break;
                    case "last_updated":
                        row["market_last_updated"] = snapshot.LastUpdatedUtc;
                        break;
                    case "bid_levels":
                        row["market_bid_levels"] = snapshot.BidLevels;
                        break;
                    case "ask_levels":
                        row["market_ask_levels"] = snapshot.AskLevels;
                        break;
                }
            }
        }

        private void AppendSelectedStudyFields(Dictionary<string, object> row)
        {
            foreach (var descriptorId in _settings.SelectedStudyIds ?? new List<string>())
            {
                if (!_latestStudySnapshots.TryGetValue(descriptorId, out var snapshot))
                    continue;

                row[ToStudyFieldKey(snapshot.DisplayName)] = snapshot.Value;
            }
        }

        private void WriteMetadata()
        {
            var selectedFieldLabels = GetMarketFieldDefinitions()
                .Where(x => _settings.SelectedMarketFields.Contains(x.Id))
                .Select(x => x.Label)
                .ToList();

            var selectedStudies = GetSelectableStudyDescriptors()
                .Where(x => _settings.SelectedStudyIds.Contains(x.Id))
                .Select(x => string.IsNullOrWhiteSpace(x.GroupName) ? x.DisplayName : $"{x.GroupName} / {x.DisplayName}")
                .ToList();

            var metadata = new
            {
                started_at_utc = _sessionStartedUtc,
                ended_at_utc = _sessionEndedUtc ?? DateTime.UtcNow,
                exchange = _settings.Provider.ProviderName,
                provider = _settings.Provider.ProviderName,
                provider_id = _settings.Provider.ProviderID,
                symbol = _settings.Symbol,
                capture_mode = _settings.CaptureMode.ToString(),
                fixed_interval = _settings.CaptureMode == CaptureMode.FixedInterval ? _settings.AggregationLevel.ToString() : null,
                duration_minutes = _settings.RunIndefinitely ? (int?)null : _settings.DurationMinutes,
                output_folder = _sessionFolder,
                data_file = _dataFilePath,
                record_count = Interlocked.Read(ref _writtenRecords),
                selected_market_fields = selectedFieldLabels,
                selected_studies = selectedStudies,
                format = "jsonl"
            };

            File.WriteAllText(_metadataFilePath, JsonConvert.SerializeObject(metadata, Formatting.Indented, _jsonSettings));
        }

        private void AddStatusCalculation(string tooltip)
        {
            AddCalculation(new BaseStudyModel
            {
                Value = Interlocked.Read(ref _writtenRecords),
                Format = RecordsValueFormat,
                Timestamp = HelperTimeProvider.Now,
                Tooltip = tooltip,
                AddItemSkippingAggregation = true
            });
        }

        public static IReadOnlyList<SelectableStudyDescriptor> GetSelectableStudyDescriptors()
        {
            var pluginManagerType = GetPluginManagerType();
            var method = pluginManagerType?.GetMethod("GetSelectableStudies", BindingFlags.Public | BindingFlags.Static);
            if (method?.Invoke(null, null) is not IEnumerable rawStudies)
                return new List<SelectableStudyDescriptor>();

            var descriptors = new List<SelectableStudyDescriptor>();
            foreach (var item in rawStudies)
            {
                if (item == null)
                    continue;

                descriptors.Add(new SelectableStudyDescriptor
                {
                    Id = item.GetType().GetProperty("Id")?.GetValue(item)?.ToString() ?? string.Empty,
                    DisplayName = item.GetType().GetProperty("DisplayName")?.GetValue(item)?.ToString() ?? string.Empty,
                    GroupName = item.GetType().GetProperty("GroupName")?.GetValue(item)?.ToString(),
                    ProviderName = item.GetType().GetProperty("ProviderName")?.GetValue(item)?.ToString() ?? string.Empty,
                    Symbol = item.GetType().GetProperty("Symbol")?.GetValue(item)?.ToString() ?? string.Empty
                });
            }
            return descriptors;
        }

        private static IEnumerable<object> GetLoadedPlugins()
        {
            var pluginManagerType = GetPluginManagerType();
            var property = pluginManagerType?.GetProperty("AllPlugins", BindingFlags.Public | BindingFlags.Static);
            if (property?.GetValue(null) is IEnumerable plugins)
            {
                foreach (var plugin in plugins)
                    yield return plugin;
            }
        }

        private static Type GetPluginManagerType()
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Select(x => x.GetType("VisualHFT.PluginManager.PluginManager", false))
                .FirstOrDefault(x => x != null);
        }
        private static IStudy ResolveStudyByDescriptorId(string descriptorId)
        {
            foreach (var plugin in GetLoadedPlugins())
            {
                if (plugin is not VisualHFT.PluginManager.IPlugin pluginBase)
                    continue;

                if (plugin is IMultiStudy multi)
                {
                    var parentId = pluginBase.GetPluginUniqueID();
                    foreach (var child in multi.Studies ?? new List<IStudy>())
                    {
                        if ($"{parentId}|{child.TileTitle}" == descriptorId)
                            return child;
                    }
                }
                else if (plugin is IStudy study && pluginBase.GetPluginUniqueID() == descriptorId)
                {
                    return study;
                }
            }
            return null;
        }

        private static TimeSpan ToTimeSpan(AggregationLevel level)
        {
            return level switch
            {
                AggregationLevel.Ms1 => TimeSpan.FromMilliseconds(1),
                AggregationLevel.Ms10 => TimeSpan.FromMilliseconds(10),
                AggregationLevel.Ms100 => TimeSpan.FromMilliseconds(100),
                AggregationLevel.Ms500 => TimeSpan.FromMilliseconds(500),
                AggregationLevel.S1 => TimeSpan.FromSeconds(1),
                AggregationLevel.S3 => TimeSpan.FromSeconds(3),
                AggregationLevel.S5 => TimeSpan.FromSeconds(5),
                AggregationLevel.D1 => TimeSpan.FromDays(1),
                _ => TimeSpan.Zero
            };
        }

        private static string MakeSafeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "unknown";

            var invalidChars = Path.GetInvalidFileNameChars();
            var chars = value.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray();
            return new string(chars);
        }

        private static string EmptyToNull(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        private static string ToStudyFieldKey(string displayName)
        {
            return "study_" + Slugify(displayName);
        }

        private static string Slugify(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "unknown";

            var builder = new System.Text.StringBuilder(value.Length);
            var previousWasUnderscore = false;

            foreach (var ch in value.ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(ch))
                {
                    builder.Append(ch);
                    previousWasUnderscore = false;
                }
                else if (!previousWasUnderscore)
                {
                    builder.Append('_');
                    previousWasUnderscore = true;
                }
            }

            return builder.ToString().Trim('_');
        }

        private sealed class RecordEnvelope
        {
            public object Payload { get; set; }
        }

        private sealed class MarketSnapshot
        {
            public DateTime RowTimestampUtc { get; set; }
            public double? BestBidPrice { get; set; }
            public double? BestBidSize { get; set; }
            public double? BestAskPrice { get; set; }
            public double? BestAskSize { get; set; }
            public double MidPrice { get; set; }
            public double Spread { get; set; }
            public double Imbalance { get; set; }
            public long Sequence { get; set; }
            public DateTime? LastUpdatedUtc { get; set; }
            public List<MarketLevel> BidLevels { get; set; }
            public List<MarketLevel> AskLevels { get; set; }
        }

        private sealed class StudyMetricSnapshot
        {
            public string DescriptorId { get; set; }
            public string DisplayName { get; set; }
            public DateTime TimestampUtc { get; set; }
            public decimal Value { get; set; }
            public string Format { get; set; }
            public decimal MarketMidPrice { get; set; }
            public string ValueColor { get; set; }
            public string Tag { get; set; }
            public string Tooltip { get; set; }
        }

        private sealed class MarketLevel
        {
            public double? Price { get; set; }
            public double? Size { get; set; }
        }

        public sealed class SelectableStudyDescriptor
        {
            public string Id { get; set; }
            public string DisplayName { get; set; }
            public string GroupName { get; set; }
            public string ProviderName { get; set; }
            public string Symbol { get; set; }
        }
        public sealed class MarketFieldDefinition
        {
            public MarketFieldDefinition(string id, string label)
            {
                Id = id;
                Label = label;
            }

            public string Id { get; }
            public string Label { get; }
        }
    }
}






