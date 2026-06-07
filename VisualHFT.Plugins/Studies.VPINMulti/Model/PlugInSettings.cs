using System.Collections.Generic;
using System.Linq;
using VisualHFT.Enums;
using VisualHFT.Model;
using VisualHFT.UserSettings;

namespace VisualHFT.Studies.VPINMulti.Model
{
    public class PlugInSettings : ISetting
    {
        public string Symbol { get; set; } = string.Empty;
        public Provider Provider { get; set; } = new Provider();
        public AggregationLevel AggregationLevel { get; set; } = AggregationLevel.S1;
        public List<VpinProfileSettings> Profiles { get; set; } = new List<VpinProfileSettings>();

        public string? GetConfigurationError()
        {
            var missing = new List<string>();
            if (Provider == null || string.IsNullOrEmpty(Provider.ProviderName))
                missing.Add("data provider");
            if (string.IsNullOrWhiteSpace(Symbol))
                missing.Add("symbol");
            if (Profiles == null || Profiles.Count == 0)
                missing.Add("at least one VPIN profile");

            if (missing.Count > 0)
                return "Missing: " + string.Join(", ", missing) + ".";

            if (Profiles.Any(x => x.BucketVolSize <= 0))
                return "Each VPIN profile needs a bucket volume size greater than zero.";

            if (Profiles.Any(x => x.NumberOfBuckets <= 0))
                return "Each VPIN profile needs a number of buckets greater than zero.";

            if (Profiles
                .GroupBy(x => VpinProfileNaming.FormatDisplayName(x.NumberOfBuckets, x.BucketVolSize), System.StringComparer.OrdinalIgnoreCase)
                .Any(x => x.Count() > 1))
                return "VPIN profiles must have unique bucket-count and bucket-size combinations.";

            return null;
        }
    }

    public class VpinProfileSettings
    {
        public string Name { get; set; } = string.Empty;
        public double BucketVolSize { get; set; }
        public int NumberOfBuckets { get; set; }
    }

    public static class VpinProfileNaming
    {
        public static string FormatDisplayName(int numberOfBuckets, double bucketVolumeSize)
        {
            return $"VPIN_{numberOfBuckets}_{bucketVolumeSize:0.###}";
        }
    }
}
