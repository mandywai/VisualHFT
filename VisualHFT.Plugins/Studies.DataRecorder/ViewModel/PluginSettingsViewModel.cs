using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using VisualHFT.Commons.Helpers;
using VisualHFT.Enums;
using VisualHFT.Helpers;
using VisualHFT.PluginManager;
using VisualHFT.Studies.DataRecorder.Model;
using VisualHFT.ViewModel.Model;
using RecorderCaptureMode = VisualHFT.Studies.DataRecorder.Model.CaptureMode;

namespace VisualHFT.Studies.DataRecorder.ViewModel
{
    public class SelectableItem : INotifyPropertyChanged
    {
        private bool _isSelected;

        public string Id { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value)
                    return;
                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    public class PluginSettingsViewModel : INotifyPropertyChanged, IDataErrorInfo
    {
        private ObservableCollection<Provider> _providers;
        private ObservableCollection<string> _symbols;
        private Provider _selectedProvider;
        private int? _selectedProviderID;
        private string _selectedSymbol;
        private AggregationLevel _aggregationLevelSelection;
        private RecorderCaptureMode _captureMode;
        private string _outputFolder = string.Empty;
        private bool _runIndefinitely = true;
        private int _durationMinutes;
        private string _validationMessage;
        private readonly Action _actionCloseWindow;

        public ICommand OkCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand BrowseFolderCommand { get; }
        public Action UpdateSettingsFromUI { get; set; }

        public PluginSettingsViewModel(Action actionCloseWindow)
        {
            _actionCloseWindow = actionCloseWindow;
            OkCommand = new RelayCommand<object>(ExecuteOkCommand, CanExecuteOkCommand);
            CancelCommand = new RelayCommand<object>(ExecuteCancelCommand);
            BrowseFolderCommand = new RelayCommand<object>(ExecuteBrowseFolder);

            _symbols = new ObservableCollection<string>(HelperSymbol.Instance);
            _providers = Provider.CreateObservableCollection();

            HelperProvider.Instance.OnDataReceived += PROVIDERS_OnDataReceived;
            HelperSymbol.Instance.OnCollectionChanged += ALLSYMBOLS_CollectionChanged;

            AggregationLevels = new ObservableCollection<Tuple<string, AggregationLevel>>();
            foreach (AggregationLevel level in Enum.GetValues(typeof(AggregationLevel)))
            {
                AggregationLevels.Add(new Tuple<string, AggregationLevel>(HelperCommon.GetEnumDescription(level), level));
            }

            CaptureModes = new ObservableCollection<Tuple<string, RecorderCaptureMode>>
            {
                new Tuple<string, RecorderCaptureMode>("Event driven", RecorderCaptureMode.OnUpdate),
                new Tuple<string, RecorderCaptureMode>("Time driven", RecorderCaptureMode.FixedInterval)
            };

            MarketFieldOptions = new ObservableCollection<SelectableItem>(DataRecorderStudy.GetMarketFieldDefinitions().Select(x => new SelectableItem
            {
                Id = x.Id,
                Label = x.Label
            }));
            StudyOptions = new ObservableCollection<SelectableItem>(DataRecorderStudy.GetSelectableStudyDescriptors().Select(x => new SelectableItem
            {
                Id = x.Id,
                Label = string.IsNullOrWhiteSpace(x.GroupName)
                    ? $"{x.DisplayName} [{x.ProviderName} {x.Symbol}]"
                    : $"{x.GroupName} / {x.DisplayName} [{x.ProviderName} {x.Symbol}]"
            }));

            LoadSelectedProviderID();
        }

        public ObservableCollection<Provider> Providers
        {
            get => _providers;
            set
            {
                _providers = value;
                OnPropertyChanged(nameof(Providers));
            }
        }

        public ObservableCollection<string> Symbols
        {
            get => _symbols;
            set
            {
                _symbols = value;
                OnPropertyChanged(nameof(Symbols));
            }
        }

        public ObservableCollection<Tuple<string, AggregationLevel>> AggregationLevels { get; }
        public ObservableCollection<Tuple<string, RecorderCaptureMode>> CaptureModes { get; }
        public ObservableCollection<SelectableItem> MarketFieldOptions { get; }
        public ObservableCollection<SelectableItem> StudyOptions { get; }

        public int? SelectedProviderID
        {
            get => _selectedProviderID;
            set
            {
                _selectedProviderID = value;
                OnPropertyChanged(nameof(SelectedProviderID));
                RaiseCanExecuteChanged();
                LoadSelectedProviderID();
            }
        }

        public Provider SelectedProvider
        {
            get => _selectedProvider;
            set
            {
                _selectedProvider = value;
                OnPropertyChanged(nameof(SelectedProvider));
                RaiseCanExecuteChanged();
                LoadSelectedProviderID();
            }
        }

        public string SelectedSymbol
        {
            get => _selectedSymbol;
            set
            {
                _selectedSymbol = value;
                OnPropertyChanged(nameof(SelectedSymbol));
                RaiseCanExecuteChanged();
            }
        }

        public AggregationLevel AggregationLevelSelection
        {
            get => _aggregationLevelSelection;
            set
            {
                _aggregationLevelSelection = value;
                OnPropertyChanged(nameof(AggregationLevelSelection));
                RaiseCanExecuteChanged();
                OnPropertyChanged(nameof(IsFixedInterval));
            }
        }

        public RecorderCaptureMode CaptureModeSelection
        {
            get => _captureMode;
            set
            {
                _captureMode = value;
                OnPropertyChanged(nameof(CaptureModeSelection));
                RaiseCanExecuteChanged();
                OnPropertyChanged(nameof(IsFixedInterval));
            }
        }

        public bool IsFixedInterval => CaptureModeSelection == RecorderCaptureMode.FixedInterval;

        public string OutputFolder
        {
            get => _outputFolder;
            set
            {
                _outputFolder = value ?? string.Empty;
                OnPropertyChanged(nameof(OutputFolder));
                RaiseCanExecuteChanged();
            }
        }

        public bool RunIndefinitely
        {
            get => _runIndefinitely;
            set
            {
                _runIndefinitely = value;
                OnPropertyChanged(nameof(RunIndefinitely));
                OnPropertyChanged(nameof(IsDurationEnabled));
                RaiseCanExecuteChanged();
            }
        }

        public bool IsDurationEnabled => !RunIndefinitely;

        public int DurationMinutes
        {
            get => _durationMinutes;
            set
            {
                _durationMinutes = value;
                OnPropertyChanged(nameof(DurationMinutes));
                RaiseCanExecuteChanged();
            }
        }

        public string ValidationMessage
        {
            get => _validationMessage;
            set
            {
                _validationMessage = value;
                OnPropertyChanged(nameof(ValidationMessage));
            }
        }

        public string Error => null;

        public string this[string columnName]
        {
            get
            {
                switch (columnName)
                {
                    case nameof(SelectedProvider):
                        if (SelectedProvider == null)
                            return "Select the exchange/provider.";
                        break;
                    case nameof(SelectedSymbol):
                        if (string.IsNullOrWhiteSpace(SelectedSymbol))
                            return "Select the symbol.";
                        break;
                    case nameof(OutputFolder):
                        if (string.IsNullOrWhiteSpace(OutputFolder))
                            return "Select an output folder.";
                        break;
                    case nameof(DurationMinutes):
                        if (!RunIndefinitely && DurationMinutes <= 0)
                            return "Duration must be greater than zero.";
                        break;
                    case nameof(AggregationLevelSelection):
                        if (CaptureModeSelection == RecorderCaptureMode.FixedInterval && AggregationLevelSelection == AggregationLevel.None)
                            return "Select a fixed interval.";
                        break;
                    default:
                        return null;
                }

                if (!MarketFieldOptions.Any(x => x.IsSelected) && !StudyOptions.Any(x => x.IsSelected))
                    return "Select at least one field or study.";

                return null;
            }
        }

        public void ApplySettings(PlugInSettings settings)
        {
            SelectedSymbol = settings.Symbol;
            SelectedProviderID = settings.Provider?.ProviderID;
            AggregationLevelSelection = settings.AggregationLevel;
            CaptureModeSelection = settings.CaptureMode;
            OutputFolder = settings.OutputFolder;
            RunIndefinitely = settings.RunIndefinitely;
            DurationMinutes = settings.DurationMinutes;
            var selectedFields = new HashSet<string>(settings.SelectedMarketFields ?? new List<string>());
            foreach (var item in MarketFieldOptions)
                item.IsSelected = selectedFields.Contains(item.Id);

            var selectedStudies = new HashSet<string>(settings.SelectedStudyIds ?? new List<string>());
            foreach (var item in StudyOptions)
                item.IsSelected = selectedStudies.Contains(item.Id);
        }

        private void ExecuteBrowseFolder(object obj)
        {
            var dialog = new OpenFolderDialog();
            if (!string.IsNullOrWhiteSpace(OutputFolder))
                dialog.InitialDirectory = OutputFolder;

            if (dialog.ShowDialog() == true)
                OutputFolder = dialog.FolderName;
        }

        private void ExecuteOkCommand(object obj)
        {
            ValidationMessage = string.Empty;
            UpdateSettingsFromUI?.Invoke();
            _actionCloseWindow?.Invoke();
        }

        private void ExecuteCancelCommand(object obj)
        {
            _actionCloseWindow?.Invoke();
        }

        private bool CanExecuteOkCommand(object obj)
        {
            return string.IsNullOrWhiteSpace(this[nameof(SelectedProvider)]) &&
                   string.IsNullOrWhiteSpace(this[nameof(SelectedSymbol)]) &&
                   string.IsNullOrWhiteSpace(this[nameof(OutputFolder)]) &&
                   string.IsNullOrWhiteSpace(this[nameof(DurationMinutes)]) &&
                   string.IsNullOrWhiteSpace(this[nameof(AggregationLevelSelection)]);
        }

        private void RaiseCanExecuteChanged()
        {
            (OkCommand as RelayCommand<object>)?.RaiseCanExecuteChanged();
        }

        private void LoadSelectedProviderID()
        {
            if (_selectedProvider != null)
            {
                _selectedProviderID = _selectedProvider.ProviderID;
                OnPropertyChanged(nameof(SelectedProviderID));
            }
            else if (_selectedProviderID.HasValue && _providers.Any())
            {
                _selectedProvider = _providers.FirstOrDefault(x => x.ProviderID == _selectedProviderID.Value);
                OnPropertyChanged(nameof(SelectedProvider));
            }
        }

        private void ALLSYMBOLS_CollectionChanged(object? sender, string e)
        {
            Symbols = new ObservableCollection<string>(HelperSymbol.Instance);
        }

        private void PROVIDERS_OnDataReceived(object? sender, VisualHFT.Model.Provider e)
        {
            Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
            {
                var item = new Provider(e);
                if (!_providers.Any(x => x.ProviderCode == e.ProviderCode))
                    _providers.Add(item);
                if (_selectedProvider == null && e.Status == eSESSIONSTATUS.CONNECTED)
                    SelectedProvider = item;
            }));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}




