using VisualHFT.Model;

namespace VisualHFT.Commons.Studies
{
    public interface IStudy : IDisposable
    {
        public event EventHandler<decimal> OnAlertTriggered;
        public event EventHandler<BaseStudyModel> OnCalculated;

        Task StartAsync();
        Task StopAsync();
        string TileTitle { get; set; }
        string TileToolTip { get; set; }

        // True when this study emits a metric value (via BasePluginStudy.AddCalculation) that the
        // Trigger engine can match a rule against. Selection surfaces (e.g. the trigger-rule study
        // picker, PluginManager.GetSelectableStudies) list ONLY studies where this is true. Set it
        // on your study exactly like Name/Description. Default false. A build-time lint
        // (EmitsMetricFlagConsistencyTests) fails if a study emits but leaves this false — so a
        // forgotten flag is caught in CI, not silently shipped as an invisible-in-picker study.
        bool EmitsMetric => false;
        object GetCustomUI();   //Allow to setup own UI for the plugin
        //using object type because this csproj doesn't support UI
        bool IsChartButtonVisible { get; set; }
        bool IsSettingsButtonVisisble { get; set; }
        bool IsFooterVisible { get; set; }
    }

}