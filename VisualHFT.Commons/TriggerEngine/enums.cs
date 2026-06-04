using System.ComponentModel;

namespace VisualHFT.TriggerEngine
{
    public enum ConditionOperator
    {
        Equals,
        GreaterThan,
        LessThan,
        CrossesAbove,
        CrossesBelow
    }

    public enum TimeWindowUnit
    {
        Seconds,
        Milliseconds,
        Ticks,
        Minutes,
        Hours,
        Days
    }

    public enum ActionType
    {
        [Description("Notify In-App")]
        UIAlert,

        [Description("WebHook URL")]
        RestApi
        // Future: UI, LogFile, PluginCallback, Webhook, StrategyControl, etc.
    }

    public enum AlertSeverity
    {
        Info,
        Warning,
        Error
    }
}
