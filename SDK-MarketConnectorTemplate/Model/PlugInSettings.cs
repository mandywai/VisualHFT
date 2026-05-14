using System.Collections.Generic;
using System.ComponentModel;
using VisualHFT.Enums;
using VisualHFT.Model;
using VisualHFT.UserSettings;

namespace MarketConnector.Template.Model
{
    // Persisted plug-in settings. Implementing ISetting is required so that
    // VisualHFT can route the plug-in's data through its symbol / provider /
    // aggregation pipeline.
    public class PlugInSettings : ISetting
    {
        [Description("API key issued by the exchange (leave empty for public data only).")]
        public string ApiKey { get; set; } = string.Empty;

        [Description("API secret issued by the exchange (leave empty for public data only).")]
        public string ApiSecret { get; set; } = string.Empty;

        [Description("Symbols to subscribe to. Format: RAW or RAW(NORMALIZED). " +
                     "Example: BTCUSDT(BTC/USD),ETHUSDT(ETH/USD).")]
        public List<string> Symbols { get; set; } = new List<string>();

        [Description("Depth of the order book to maintain (top-N price levels).")]
        public int DepthLevels { get; set; } = 20;

        // ---- ISetting ------------------------------------------------------
        // Symbol is the currently-active normalized symbol (used by single-symbol
        // study plug-ins). Market connectors typically expose a list above and
        // leave Symbol unset; the property must still exist for the contract.
        public string Symbol { get; set; } = string.Empty;

        public Provider Provider { get; set; } = new Provider { ProviderID = 999, ProviderName = "TemplateExchange" };

        public AggregationLevel AggregationLevel { get; set; } = AggregationLevel.Ms100;
    }
}
