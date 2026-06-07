using VisualHFT.Commons.Model;
using VisualHFT.Enums;

namespace VisualHFT.Commons.Interfaces
{
    public interface IDataRetrieverTestable
    {
        void InjectSnapshot(VisualHFT.Model.OrderBook snapshotModel, long sequence);
        void InjectDeltaModel(List<DeltaBookItem> bidDeltaModel, List<DeltaBookItem> askDeltaModel);
        List<VisualHFT.Model.Order> ExecutePrivateMessageScenario(eTestingPrivateMessageScenario scenario);

        /// <summary>
        /// Test-only: simulate a connection interruption + recovery WITHOUT a live socket. Runs the
        /// connector's REAL reconnect teardown (ClearAsync — drops subscriptions/buffers/timers and,
        /// for connectors that clear it, the local order book) then reseeds from
        /// <paramref name="reseedSnapshot"/> via the existing InjectSnapshot path and republishes —
        /// the exact teardown+reseed pair the live reconnect runs. Lets a test assert a reconnect
        /// leaves a FRESH book, not a stale one, deterministically and offline.
        ///
        /// Default is a no-op: a connector that does not opt in is simply not reconnection-reseed
        /// tested (mirrors the existing seam-discovery rule — if it isn't implemented, we don't test it).
        /// </summary>
        Task SimulateConnectionInterruption(VisualHFT.Model.OrderBook reseedSnapshot) => Task.CompletedTask;
    }
}
