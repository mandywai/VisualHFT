using System.Collections.Generic;
using VisualHFT.Enums;
using VisualHFT.Model;
using VisualHFT.UserSettings;

namespace VisualHFT.Studies.VPIN.Model
{
    public class PlugInSettings : ISetting
    {
        public double BucketVolSize { get; set; }
        public int? NumberOfBuckets { get; set; } // Rolling window size (nullable for backward compat)
        public string Symbol { get; set; }
        public Provider Provider { get; set; }
        public AggregationLevel AggregationLevel { get; set; }

        // Overrides ISetting.GetConfigurationError: VPIN also needs a positive bucket
        // size and bucket count, not just provider + symbol. (IsConfigured is derived
        // from this on the interface, so it stays in sync automatically.)
        public string? GetConfigurationError()
        {
            var missing = new List<string>();
            if (Provider == null || string.IsNullOrEmpty(Provider.ProviderName))
                missing.Add("data provider");
            if (string.IsNullOrEmpty(Symbol))
                missing.Add("symbol");
            if (BucketVolSize <= 0)
                missing.Add("bucket volume size");
            if (NumberOfBuckets is not > 0)
                missing.Add("number of buckets");
            return missing.Count == 0 ? null : "Missing: " + string.Join(", ", missing) + ".";
        }
    }
}