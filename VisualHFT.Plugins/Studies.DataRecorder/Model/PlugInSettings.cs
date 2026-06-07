using Newtonsoft.Json;
using System;
using VisualHFT.Enums;
using VisualHFT.Model;
using VisualHFT.UserSettings;

namespace VisualHFT.Studies.DataRecorder.Model
{
    public enum CaptureMode
    {
        OnUpdate = 0,
        FixedInterval = 1
    }

    public class PlugInSettings : ISetting
    {
        public string Symbol { get; set; } = string.Empty;
        public Provider Provider { get; set; } = new Provider();
        public AggregationLevel AggregationLevel { get; set; } = AggregationLevel.S1;

        public CaptureMode CaptureMode { get; set; } = CaptureMode.OnUpdate;
        public string OutputFolder { get; set; } = string.Empty;
        public bool RunIndefinitely { get; set; } = true;
        public int DurationMinutes { get; set; } = 0;
        public List<string> SelectedMarketFields { get; set; } = new List<string>();
        public List<string> SelectedStudyIds { get; set; } = new List<string>();

        // Backward compatibility for previously persisted recorder settings.
        [JsonProperty("DurationSeconds")]
        private int LegacyDurationSeconds
        {
            set
            {
                if (DurationMinutes <= 0 && value > 0)
                    DurationMinutes = (int)Math.Ceiling(value / 60d);
            }
        }

        public string? GetConfigurationError()
        {
            var missing = new List<string>();
            if (Provider == null || string.IsNullOrEmpty(Provider.ProviderName))
                missing.Add("exchange/provider");
            if (string.IsNullOrWhiteSpace(Symbol))
                missing.Add("symbol");
            if (string.IsNullOrWhiteSpace(OutputFolder))
                missing.Add("output folder");
            if ((SelectedMarketFields == null || SelectedMarketFields.Count == 0) &&
                (SelectedStudyIds == null || SelectedStudyIds.Count == 0))
                missing.Add("at least one market field or study metric");
            if (CaptureMode == CaptureMode.FixedInterval && AggregationLevel == AggregationLevel.None)
                missing.Add("fixed interval");
            if (!RunIndefinitely && DurationMinutes <= 0)
                missing.Add("positive duration");

            return missing.Count == 0 ? null : "Missing: " + string.Join(", ", missing) + ".";
        }
    }
}
