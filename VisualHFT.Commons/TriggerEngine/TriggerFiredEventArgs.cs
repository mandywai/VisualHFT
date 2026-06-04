using System;

namespace VisualHFT.TriggerEngine
{
    public sealed record TriggerFiredEventArgs(
        long RuleID,
        string RuleName,
        string Plugin,
        string Metric,
        string Exchange,
        string Symbol,
        double Value,
        double Threshold,
        ConditionOperator Operator,
        DateTime Timestamp);
}
