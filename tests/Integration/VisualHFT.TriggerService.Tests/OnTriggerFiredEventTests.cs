// T-MDR-045 (T-S08-2, T-S08-3, T-S08-5) — RED tests for
// TriggerEngineService.OnTriggerFired event. RED until T-MDR-065 lands
// (the event field + invocation site at TriggerEngineService.cs:285).
//
// Per the wiring manifest forbidden_test_fakes constraint these tests run
// the real TriggerEngineService.RegisterMetric → ProcessMetric →
// ExecuteActionAsync pipeline (no mocks).
//
// Cases:
//   6. OnTriggerFired_FiresOnceWhenRuleSatisfied
//      — register a rule, drive a metric across threshold via
//        TriggerEngineService.RegisterMetric, assert subscriber invoked
//        once with TriggerFiredEventArgs.Timestamp ≈ fire time.
//
//   7. OnTriggerFired_ThrowingSubscriberDoesNotBreakOthers
//      — subscribe a throwing handler + a counting handler; fire 10× →
//        counting handler reaches 10; WARN log per failure.
//
//   8. OnTriggerFired_HandlerLatency_p99_under_1ms
//      — 10K fires, measure p99 of subscriber wall-clock latency from
//        invocation site → assert ≤ 1 ms (FR-10 / T-S08-5).

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Repository.Hierarchy;
using VisualHFT.TriggerEngine;
using VisualHFT.TriggerEngine.Actions;
using Xunit;
using TriggerActionT = VisualHFT.TriggerEngine.TriggerAction;

namespace VisualHFT.TriggerEngine.IntegrationTests;

[Collection("TriggerEngineSerial")]
public sealed class OnTriggerFiredEventTests : IDisposable
{
    private const string Plugin = "TestPlugin";
    private const string Metric = "TestMetric";
    private const string Exchange = "Binance";
    private const string Symbol = "BTCUSDT";

    private readonly string _originalConfigPath;
    private readonly string _testConfigPath;
    private readonly CancellationTokenSource _workerCts = new();
    private readonly Task _workerTask;

    public OnTriggerFiredEventTests()
    {
        _originalConfigPath = TriggerEngineService.TriggerEngineConfigFilePath;
        _testConfigPath = Path.Combine(
            Path.GetTempPath(),
            $"TE_T-MDR-045_{Guid.NewGuid():N}",
            "TriggerEngineConfig.json");
        TriggerEngineService.TriggerEngineConfigFilePath = _testConfigPath;

        ResetEngineState();
        ResetOnTriggerFiredField();
        _workerTask = TriggerEngineService.StartBackgroundWorkerAsync(_workerCts.Token);
    }

    public void Dispose()
    {
        try
        {
            ResetOnTriggerFiredField();
            _workerCts.Cancel();
            try { _workerTask.Wait(2_000); } catch { /* drained */ }
            ResetEngineState();
            TriggerEngineService.TriggerEngineConfigFilePath = _originalConfigPath;
            var dir = Path.GetDirectoryName(_testConfigPath);
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
        catch { /* best-effort */ }
    }

    /// <summary>
    /// Case 6 (T-S08-2): a single CrossesAbove rule fires once when the
    /// metric crosses its threshold. The subscriber receives exactly one
    /// <see cref="TriggerFiredEventArgs"/> whose Timestamp ≈ fire time
    /// (within a generous 5 s wall-clock window so CI noise does not flake
    /// it). Per Pattern AM (TestEngine first-fire-no-execute), we drive
    /// TWO crossing cycles; the FIRST crossing records the action without
    /// firing, the SECOND crossing actually fires and reaches the new
    /// OnTriggerFired event.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task OnTriggerFired_FiresOnceWhenRuleSatisfied()
    {
        var capturedArgs = new List<TriggerFiredEventArgs>();
        var fired = new ManualResetEventSlim(initialState: false);
        Action<TriggerFiredEventArgs> handler = args =>
        {
            lock (capturedArgs) capturedArgs.Add(args);
            fired.Set();
        };

        SubscribeOnTriggerFired(handler);
        try
        {
            long ruleId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var rule = BuildRule(
                name: "FiresOnce",
                ruleId: ruleId,
                op: ConditionOperator.GreaterThan,
                threshold: 100.0,
                cooldownSeconds: 0);
            TriggerEngineService.AddOrUpdateRule(rule);

            DateTime fireTime = DateTime.UtcNow;
            // Pattern AM: GreaterThan re-triggers on every matching value, so
            // we need TWO matching events to clear first-fire-no-execute.
            TriggerEngineService.RegisterMetric(Plugin, Metric, Exchange, Symbol, 150.0, fireTime);
            await Task.Delay(50);
            TriggerEngineService.RegisterMetric(Plugin, Metric, Exchange, Symbol, 160.0,
                fireTime.AddMilliseconds(100));

            Assert.True(fired.Wait(TimeSpan.FromSeconds(5)),
                "OnTriggerFired did not fire within 5 s of the second crossing.");
            await Task.Delay(50);

            lock (capturedArgs)
            {
                Assert.Single(capturedArgs);
                var a = capturedArgs[0];
                Assert.Equal(ruleId, a.RuleID);
                Assert.Equal("FiresOnce", a.RuleName);
                Assert.Equal(Plugin, a.Plugin);
                Assert.Equal(Metric, a.Metric);
                Assert.Equal(Exchange, a.Exchange);
                Assert.Equal(Symbol, a.Symbol);
                Assert.Equal(160.0, a.Value);
                Assert.Equal(100.0, a.Threshold);
                Assert.Equal(ConditionOperator.GreaterThan, a.Operator);
                Assert.True(
                    Math.Abs((a.Timestamp - DateTime.UtcNow).TotalSeconds) < 5,
                    $"Timestamp {a.Timestamp:o} not within 5 s of UtcNow.");
            }
        }
        finally
        {
            UnsubscribeOnTriggerFired(handler);
        }
    }

    /// <summary>
    /// Case 7 (T-S08-3): a throwing subscriber must not prevent another
    /// subscriber from receiving subsequent fires. Architecture §2.2.5:
    /// TriggerEngineService catches per-subscriber exceptions and emits
    /// a WARN. We subscribe both a throwing handler and a counting
    /// handler, fire 10× (= 11 RegisterMetric calls because of first-fire-
    /// no-execute), and assert the counter reaches 10 + a WARN log per
    /// failure.
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task OnTriggerFired_ThrowingSubscriberDoesNotBreakOthers()
    {
        int failingCalls = 0;
        int counterCalls = 0;
        Action<TriggerFiredEventArgs> throwing = _ =>
        {
            Interlocked.Increment(ref failingCalls);
            throw new InvalidOperationException("Intentional throw from test subscriber.");
        };
        Action<TriggerFiredEventArgs> counter = _ =>
        {
            Interlocked.Increment(ref counterCalls);
        };

        using var capture = new CaptureAppender();
        SubscribeOnTriggerFired(throwing);
        SubscribeOnTriggerFired(counter);
        try
        {
            long ruleId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 7;
            var rule = BuildRule(
                name: "ThrowingSub",
                ruleId: ruleId,
                op: ConditionOperator.GreaterThan,
                threshold: 100.0,
                cooldownSeconds: 0);
            TriggerEngineService.AddOrUpdateRule(rule);

            // First-fire-no-execute primer (Pattern AM).
            TriggerEngineService.RegisterMetric(Plugin, Metric, Exchange, Symbol, 150.0, DateTime.UtcNow);
            await Task.Delay(50);

            for (int i = 0; i < 10; i++)
            {
                TriggerEngineService.RegisterMetric(
                    Plugin, Metric, Exchange, Symbol,
                    151.0 + i,
                    DateTime.UtcNow.AddMilliseconds(50 + i * 10));
                await Task.Delay(20);
            }

            // Allow the worker to drain.
            for (int i = 0; i < 50 && Volatile.Read(ref counterCalls) < 10; i++)
                await Task.Delay(50);

            Assert.Equal(10, Volatile.Read(ref counterCalls));
            Assert.True(Volatile.Read(ref failingCalls) >= 10,
                "Throwing subscriber must have been called at least 10 times.");

            // Architecture §2.2.5: a WARN is emitted per swallowed exception.
            int warnCount = capture.CountWarns("OnTriggerFired");
            Assert.True(warnCount >= 10,
                $"Expected ≥10 WARNs about OnTriggerFired subscriber; got {warnCount}.");
        }
        finally
        {
            UnsubscribeOnTriggerFired(throwing);
            UnsubscribeOnTriggerFired(counter);
        }
    }

    /// <summary>
    /// Case 8 (T-S08-5 / FR-10): with a no-op subscriber, the wall-clock
    /// latency between RegisterMetric → OnTriggerFired must have a p99
    /// ≤ 1 ms across 10K fires. Driven inline (no BenchmarkDotNet host)
    /// because TriggerEngineService is process-static and we're already
    /// in a test-runner host. Pattern AM applied: a primer cycle clears
    /// first-fire-no-execute before the measured run.
    /// </summary>
    [Fact(Timeout = 120_000)]
    public async Task OnTriggerFired_HandlerLatency_p99_under_1ms()
    {
        const int N = 10_000;
        var latenciesNs = new long[N];
        int idx = 0;
        long startNsTls = 0;
        var done = new ManualResetEventSlim(initialState: false);

        Action<TriggerFiredEventArgs> handler = _ =>
        {
            long endNs = Stopwatch.GetTimestamp();
            int i = Interlocked.Increment(ref idx) - 1;
            if (i < N)
                latenciesNs[i] = endNs - Volatile.Read(ref startNsTls);
            if (i >= N - 1) done.Set();
        };

        SubscribeOnTriggerFired(handler);
        try
        {
            long ruleId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 99;
            var rule = BuildRule(
                name: "LatencyP99",
                ruleId: ruleId,
                op: ConditionOperator.GreaterThan,
                threshold: 0.0,
                cooldownSeconds: 0);
            TriggerEngineService.AddOrUpdateRule(rule);

            // First-fire-no-execute primer (Pattern AM).
            TriggerEngineService.RegisterMetric(Plugin, Metric, Exchange, Symbol, 1.0, DateTime.UtcNow);
            await Task.Delay(100);

            for (int i = 0; i < N; i++)
            {
                Volatile.Write(ref startNsTls, Stopwatch.GetTimestamp());
                TriggerEngineService.RegisterMetric(
                    Plugin, Metric, Exchange, Symbol,
                    2.0 + i,
                    DateTime.UtcNow);
                // Busy-wait for this fire so we measure end-to-end wall clock.
                int spins = 0;
                while (Volatile.Read(ref idx) <= i && spins++ < 10_000_000)
                    Thread.SpinWait(20);
            }

            Assert.True(done.Wait(TimeSpan.FromSeconds(30)),
                $"Only {idx} of {N} fires observed.");

            double tickToNs = 1_000_000_000.0 / Stopwatch.Frequency;
            var ns = latenciesNs.Take(N).Select(t => t * tickToNs).ToArray();
            Array.Sort(ns);
            double p50 = ns[(int)(N * 0.50)];
            double p99 = ns[(int)(N * 0.99)];
            double p999 = ns[Math.Min(N - 1, (int)(N * 0.999))];

            Assert.True(p99 <= 1_000_000.0,
                $"p50={p50:F0} ns, p99={p99:F0} ns, p99.9={p999:F0} ns — p99 must be ≤ 1 ms (FR-10).");
        }
        finally
        {
            UnsubscribeOnTriggerFired(handler);
        }
    }

    // ---------- Reflection helpers ----------------------------------------
    //
    // Until T-MDR-065 lands, the OnTriggerFired field does not exist and
    // every Subscribe/Unsubscribe call below throws — that is the RED gate.
    // ----------------------------------------------------------------------

    private static FieldInfo GetOnTriggerFiredField()
    {
        var field = typeof(TriggerEngineService)
            .GetField("OnTriggerFired",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        if (field is null)
        {
            foreach (var f in typeof(TriggerEngineService).GetFields(
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public))
            {
                if (typeof(Delegate).IsAssignableFrom(f.FieldType) &&
                    f.FieldType.IsGenericType &&
                    f.FieldType.GetGenericTypeDefinition() == typeof(Action<>) &&
                    f.FieldType.GenericTypeArguments[0] == typeof(TriggerFiredEventArgs))
                {
                    field = f;
                    break;
                }
            }
        }
        if (field is null)
            throw new InvalidOperationException(
                "TriggerEngineService.OnTriggerFired event field not found. " +
                "Expected after T-MDR-065 wiring lands (RED until then).");
        return field;
    }

    private static void SubscribeOnTriggerFired(Action<TriggerFiredEventArgs> handler)
    {
        // Use the public event accessor (add_OnTriggerFired) so multicast
        // semantics match production. Resolve via reflection so the test
        // compiles before the event exists (compilation will still fail at
        // runtime with a clear error — RED).
        var addMethod = typeof(TriggerEngineService)
            .GetMethod("add_OnTriggerFired",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        if (addMethod is null)
            throw new InvalidOperationException(
                "TriggerEngineService.add_OnTriggerFired not found. RED until T-MDR-065.");
        addMethod.Invoke(null, new object[] { handler });
    }

    private static void UnsubscribeOnTriggerFired(Action<TriggerFiredEventArgs> handler)
    {
        var removeMethod = typeof(TriggerEngineService)
            .GetMethod("remove_OnTriggerFired",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        removeMethod?.Invoke(null, new object[] { handler });
    }

    private static void ResetOnTriggerFiredField()
    {
        var field = typeof(TriggerEngineService)
            .GetField("OnTriggerFired",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        field?.SetValue(null, null);
    }

    private static void ResetEngineState()
    {
        TriggerEngineService.ClearAllRules();
        ClearStaticDictionary("LastMetricValues");
        ClearStaticDictionary("ConditionStartTimes");
        ClearStaticDictionary("ActionLastFiredTimes");
    }

    private static void ClearStaticDictionary(string fieldName)
    {
        var field = typeof(TriggerEngineService)
            .GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic);
        if (field is null) return;
        var dict = field.GetValue(null);
        var clearMethod = dict?.GetType().GetMethod("Clear");
        clearMethod?.Invoke(dict, null);
    }

    private static TriggerRule BuildRule(
        string name, long ruleId, ConditionOperator op, double threshold, int cooldownSeconds)
    {
        return new TriggerRule
        {
            Name = name,
            RuleID = ruleId,
            IsEnabled = true,
            Condition = new List<TriggerCondition>
            {
                new TriggerCondition
                {
                    ConditionID = ruleId,
                    Plugin = Plugin,
                    Metric = Metric,
                    Operator = op,
                    Threshold = threshold,
                }
            },
            Actions = new List<TriggerActionT>
            {
                new TriggerActionT
                {
                    ActionID = ruleId,
                    Type = ActionType.UIAlert,
                    CooldownDuration = cooldownSeconds,
                    CooldownUnit = TimeWindowUnit.Seconds,
                }
            }
        };
    }

    /// <summary>
    /// Captures log4net WARN messages so the throwing-subscriber test can
    /// assert the per-failure WARN required by Architecture §2.2.5.
    /// </summary>
    private sealed class CaptureAppender : AppenderSkeleton, IDisposable
    {
        private readonly System.Collections.Concurrent.ConcurrentQueue<string> _warns = new();

        public CaptureAppender()
        {
            Name = "TeCapture-" + Guid.NewGuid().ToString("N");
            var hierarchy = (Hierarchy)LogManager.GetRepository(typeof(CaptureAppender).Assembly);
            hierarchy.Root.AddAppender(this);
            hierarchy.Configured = true;
        }

        protected override void Append(LoggingEvent loggingEvent)
        {
            if (loggingEvent.Level == null) return;
            if (loggingEvent.Level.Value >= Level.Warn.Value)
                _warns.Enqueue(loggingEvent.RenderedMessage ?? string.Empty);
        }

        public int CountWarns(string contains)
        {
            int n = 0;
            foreach (var m in _warns)
                if (m.IndexOf(contains, StringComparison.OrdinalIgnoreCase) >= 0)
                    n++;
            return n;
        }

        public new void Dispose()
        {
            try
            {
                var hierarchy = (Hierarchy)LogManager.GetRepository(typeof(CaptureAppender).Assembly);
                hierarchy.Root.RemoveAppender(this);
                base.Close();
            }
            catch { /* best-effort */ }
        }
    }
}
