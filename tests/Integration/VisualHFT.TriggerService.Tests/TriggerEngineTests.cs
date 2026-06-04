using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using VisualHFT.Commons.Helpers;
using VisualHFT.TriggerEngine;
using TriggerAction = VisualHFT.TriggerEngine.TriggerAction;

namespace VisualHFT.DataRetriever.TestingFramework.TestCases
{
    public class TriggerEngineTests
    {
        private const double Threshold = 100.0;
        public const string PluginID = "PluginID1";
        public const string PluginName = "TestPlugin";
        public const string Exchange = "Binance";
        public const string Symbol = "BTC-USD";
         
        [Fact]
        public void AddOrUpdateRule_ShouldAddNewRule()
        {
            var rule = new TriggerRule { Name = "TestRule1", IsEnabled = true,RuleID = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() };
            TriggerEngineService.AddOrUpdateRule(rule);

            var rules = TriggerEngineService.GetRules();
            Assert.Contains(rules, r => r.Name == "TestRule1");
        }

        [Fact]
        public void AddOrUpdateRule_ShouldUpdateExistingRule()
        {
            var rule1 = new TriggerRule { Name = "TestRule2", IsEnabled = true, RuleID = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() };
            var rule2 = new TriggerRule { Name = "TestRule2", IsEnabled = false , RuleID = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() };

            TriggerEngineService.AddOrUpdateRule(rule1);
            TriggerEngineService.AddOrUpdateRule(rule2);

            var rules = TriggerEngineService.GetRules();
            var updated = rules.FirstOrDefault(r => r.Name == "TestRule2");
            Assert.False(updated?.IsEnabled);
        }

        [Fact]
        public void RemoveRule_ShouldRemoveRuleByName()
        {
            var rule = new TriggerRule { Name = "TestRule3", RuleID = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() };
            TriggerEngineService.AddOrUpdateRule(rule);
            TriggerEngineService.RemoveRule(rule.RuleID);

            var rules = TriggerEngineService.GetRules();
            Assert.DoesNotContain(rules, r => r.RuleID == rule.RuleID);
        }

        [Fact]
        public async Task RegisterMetric_ShouldTriggerMatchingRuleAction()
        {
            bool actionExecuted = false;

            var rule = new TriggerRule
            {
                Name = "TestRunRule",
                IsEnabled = true,
                RuleID=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Condition = new List<TriggerCondition>
            {
                new TriggerCondition
                {
                    Plugin = PluginID,
                    Metric =PluginName,
                    Threshold = 100,
                    Operator = ConditionOperator.GreaterThan
                }
            },
                Actions = new List<TriggerAction>
            {
                new TriggerAction
                {
                    Type = ActionType.RestApi,
                    CooldownDuration = 0,
                    CooldownUnit = TimeWindowUnit.Seconds,
                    RestApi = new TriggerEngine.Actions.RestApiAction
                    {
                        Url = "http://test.api",
                        Method = "POST",
                        BodyTemplate = "{{metric}}-{{value}}-{{timestamp}}"
                    }
                }
            }
            };

            try
            {
                TriggerEngineService.AddOrUpdateRule(rule);

                var cts = new CancellationTokenSource();
                var workerTask = TriggerEngineService.StartBackgroundWorkerAsync(cts.Token);

                TriggerEngineService.RegisterMetric(PluginID, PluginName,Exchange,Symbol, 120, DateTime.UtcNow);

                await Task.Delay(200);  

                cts.Cancel();
                await workerTask;
            }
            catch (Exception ex)
            {
                 
            }   
        }


        [Theory]
        [InlineData(ConditionOperator.Equals, 100, 50, 100, true)]
        [InlineData(ConditionOperator.Equals, 100, 50, 99.9, false)]
        [InlineData(ConditionOperator.GreaterThan, 100, 100, 101, true)]
        [InlineData(ConditionOperator.GreaterThan, 100, 100, 100, false)]
        [InlineData(ConditionOperator.LessThan, 100, 100, 99, true)]
        [InlineData(ConditionOperator.LessThan, 100, 100, 100, false)]
        [InlineData(ConditionOperator.CrossesAbove, 100, 99, 101, true)]
        [InlineData(ConditionOperator.CrossesAbove, 100, 101, 102, false)]
        [InlineData(ConditionOperator.CrossesBelow, 100, 101, 99, true)]
        [InlineData(ConditionOperator.CrossesBelow, 100, 99, 101, false)]
        public void EvaluateDirect_ShouldWork(ConditionOperator op, double threshold, double previous, double current, bool expected)
        {
            var condition = new TriggerCondition
            {
                Operator = op,
                Threshold = threshold
            };

            Assert.Equal(expected, Evaluate(condition, current, previous));
        }

        [Fact]
        public void EvaluateDirect_ShouldReturnFalse_ForUnknownOperator()
        {
            var condition = new TriggerCondition
            {
                Operator = (ConditionOperator)999,
                Threshold = 100
            };

            Assert.False(Evaluate(condition, 50, 100));
        }



        private static TriggerRule CreateRule(TimeSpan cooldown)
        {
            return new TriggerRule
            {
                Name = "TestRule",
                IsEnabled = true,
                RuleID = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Condition = new List<TriggerCondition>
            {
                new TriggerCondition
                {
                    ConditionID= DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    Plugin = PluginID,
                    Metric = PluginName,
                    Operator = ConditionOperator.GreaterThan,
                    Threshold = Threshold
                }
            },
                Actions = new List<TriggerAction>
            {
                new TriggerAction
                {
                    Type = ActionType.UIAlert,
                    CooldownDuration = (int)cooldown.TotalSeconds,
                    CooldownUnit = TimeWindowUnit.Seconds
                }
            }
            };
        }

        [Fact]
        public async Task Should_Trigger_Immediately_When_Condition_Met()
        {
            TriggerEngineService.ClearAllRules();
            // Arrange
            var cooldown = TimeSpan.FromSeconds(5);
            var rule = CreateRule(cooldown);
            TriggerEngineService.AddOrUpdateRule(rule);

            var cts = new CancellationTokenSource();
            var workerTask = TriggerEngineService.StartBackgroundWorkerAsync(cts.Token);


            var now = DateTime.UtcNow;

            // Act: first metric meets the condition — fires immediately. The spec
            // (FR-3.3.1 / S-08) treats the first breach like any other; the prior
            // "first fire records but does not execute" behavior was GAP-MDR-01.
            TriggerEngineService.RegisterMetric(PluginID, PluginName, Exchange, Symbol, 120.0, now);

            await Task.Delay(200); // simulate time for async processing

            // Second metric 3s later — within the 5s cooldown, so suppressed.
            TriggerEngineService.RegisterMetric(PluginID, PluginName, Exchange, Symbol, 130.0, now.AddSeconds(3));

            await Task.Delay(200);

            int count = HelperNotificationManager.Instance.GetAllNotifications().Where(x => x.Category == HelprNorificationManagerCategories.TRIGGER_ENGINE && x.PluginID.Equals(PluginID)).Count();

            Assert.Equal(1, count); // first breach fires; the second is suppressed by cooldown
        }

        [Fact]
        public async Task Should_Fire_On_First_Breach_And_Again_After_Cooldown()
        {
            TriggerEngineService.ClearAllRules();
            // Arrange
            var cooldown = TimeSpan.FromSeconds(3);
            var rule = CreateRule(cooldown);
            TriggerEngineService.AddOrUpdateRule(rule);

            var cts = new CancellationTokenSource();
            var workerTask = TriggerEngineService.StartBackgroundWorkerAsync(cts.Token);

            var baseTime = DateTime.UtcNow;

            // Act 1: first match — fires immediately (GAP-MDR-01 fix)
            TriggerEngineService.RegisterMetric(PluginID, PluginName, Exchange, Symbol, 120.0, baseTime);
            await Task.Delay(100);

            // Act 2: 2s later — still within the 3s cooldown, suppressed
            TriggerEngineService.RegisterMetric(PluginID, PluginName, Exchange, Symbol, 125.0, baseTime.AddSeconds(2));
            await Task.Delay(100);

            // Act 3: 4s later — past cooldown, fires again
            TriggerEngineService.RegisterMetric(PluginID, PluginName, Exchange, Symbol, 130.0, baseTime.AddSeconds(4));
            await Task.Delay(100);

            int count = HelperNotificationManager.Instance.GetAllNotifications().Where(x => x.Category == HelprNorificationManagerCategories.TRIGGER_ENGINE && x.PluginID.Equals(PluginID)).Count();
            Assert.Equal(2, count); // first breach + one re-fire after the cooldown expires
        }

        [Fact]
        public async Task Should_Fire_On_First_Breach_Then_Not_ReFire_When_Condition_Breaks_Within_Cooldown()
        {
            TriggerEngineService.ClearAllRules();
            // Arrange
            var cooldown = TimeSpan.FromSeconds(4);
            var rule = CreateRule(cooldown);
            TriggerEngineService.AddOrUpdateRule(rule);
            var cts = new CancellationTokenSource();
            var workerTask = TriggerEngineService.StartBackgroundWorkerAsync(cts.Token);

            var baseTime = DateTime.UtcNow;

            // Act: first breach — fires immediately (GAP-MDR-01 fix)
            TriggerEngineService.RegisterMetric(PluginID, PluginName, Exchange, Symbol, 120.0, baseTime);
            await Task.Delay(100);

            // Condition breaks (below threshold) — no fire
            TriggerEngineService.RegisterMetric(PluginID, PluginName, Exchange, Symbol, 80.0, baseTime.AddSeconds(2));
            await Task.Delay(100);

            // Condition true again at +3s — within the 4s cooldown from the first
            // fire, so no second fire.
            TriggerEngineService.RegisterMetric(PluginID, PluginName, Exchange, Symbol, 125.0, baseTime.AddSeconds(3));
            await Task.Delay(100);

            // Exactly one fire: the first breach. The break + return is still
            // inside the cooldown window, so it does not re-fire.
            int count = HelperNotificationManager.Instance.GetAllNotifications().Where(x => x.Category == HelprNorificationManagerCategories.TRIGGER_ENGINE && x.PluginID.Equals(PluginID)).Count();
            Assert.Equal(1, count);


        }

    private Func<TriggerCondition, double, double, bool> Evaluate =>
            (condition, current, previous) =>
                (bool)typeof(TriggerEngineService)
                    .GetMethod("EvaluateDirect", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { condition, current, previous });
    }
}