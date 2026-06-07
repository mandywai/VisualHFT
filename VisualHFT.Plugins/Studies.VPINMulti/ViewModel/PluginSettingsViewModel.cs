using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using VisualHFT.Enums;
using VisualHFT.Helpers;
using VisualHFT.Studies.VPINMulti.Model;
using VisualHFT.ViewModel.Model;

namespace VisualHFT.Studies.VPINMulti.ViewModel
{
    public sealed class VpinProfileItem : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private double _bucketVolumeSize;
        private int _numberOfBuckets;

        public string Name
        {
            get => _name;
            set
            {
                if (_name == value)
                    return;
                _name = value;
                OnPropertyChanged(nameof(Name));
            }
        }

        public double BucketVolumeSize
        {
            get => _bucketVolumeSize;
            set
            {
                if (_bucketVolumeSize == value)
                    return;
                _bucketVolumeSize = value;
                OnPropertyChanged(nameof(BucketVolumeSize));
            }
        }

        public int NumberOfBuckets
        {
            get => _numberOfBuckets;
            set
            {
                if (_numberOfBuckets == value)
                    return;
                _numberOfBuckets = value;
                OnPropertyChanged(nameof(NumberOfBuckets));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class PluginSettingsViewModel : INotifyPropertyChanged, IDataErrorInfo
    {
        private ObservableCollection<Provider> _providers;
        private ObservableCollection<string> _symbols;
        private Provider _selectedProvider;
        private int? _selectedProviderID;
        private string _selectedSymbol = string.Empty;
        private AggregationLevel _aggregationLevelSelection;
        private string _validationMessage = string.Empty;
        private readonly Action _actionCloseWindow;
        private int _nextProfileNumber = 1;

        public ICommand OkCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand AddProfileCommand { get; }
        public ICommand RemoveProfileCommand { get; }
        public Action UpdateSettingsFromUI { get; set; }

        public PluginSettingsViewModel(Action actionCloseWindow)
        {
            _actionCloseWindow = actionCloseWindow;
            OkCommand = new RelayCommand<object>(ExecuteOkCommand, CanExecuteOkCommand);
            CancelCommand = new RelayCommand<object>(ExecuteCancelCommand);
            AddProfileCommand = new RelayCommand<object>(_ => AddProfile());
            RemoveProfileCommand = new RelayCommand<object>(ExecuteRemoveProfile);

            _symbols = new ObservableCollection<string>(HelperSymbol.Instance);
            _providers = Provider.CreateObservableCollection();

            HelperProvider.Instance.OnDataReceived += PROVIDERS_OnDataReceived;
            HelperSymbol.Instance.OnCollectionChanged += ALLSYMBOLS_CollectionChanged;

            AggregationLevels = new ObservableCollection<Tuple<string, AggregationLevel>>();
            foreach (AggregationLevel level in Enum.GetValues(typeof(AggregationLevel)))
            {
                AggregationLevels.Add(new Tuple<string, AggregationLevel>(Commons.Helpers.HelperCommon.GetEnumDescription(level), level));
            }

            Profiles = new ObservableCollection<VpinProfileItem>();
            LoadSelectedProviderID();
        }

        public ObservableCollection<Provider> Providers { get => _providers; set => _providers = value; }
        public ObservableCollection<string> Symbols { get => _symbols; set => _symbols = value; }
        public ObservableCollection<Tuple<string, AggregationLevel>> AggregationLevels { get; }
        public ObservableCollection<VpinProfileItem> Profiles { get; }

        public int? SelectedProviderID
        {
            get => _selectedProviderID;
            set
            {
                _selectedProviderID = value;
                OnPropertyChanged(nameof(SelectedProviderID));
                RaiseCanExecuteChanged();
                LoadSelectedProviderID();
                RefreshValidationMessage();
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
                RefreshValidationMessage();
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
                RefreshValidationMessage();
            }
        }

        public AggregationLevel AggregationLevelSelection
        {
            get => _aggregationLevelSelection;
            set
            {
                _aggregationLevelSelection = value;
                OnPropertyChanged(nameof(AggregationLevelSelection));
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
                            return "Select the Provider.";
                        break;
                    case nameof(SelectedSymbol):
                        if (string.IsNullOrWhiteSpace(SelectedSymbol))
                            return "Select the Symbol.";
                        break;
                    default:
                        return null;
                }

                return null;
            }
        }

        public void ApplySettings(PlugInSettings settings)
        {
            SelectedSymbol = settings.Symbol;
            SelectedProviderID = settings.Provider?.ProviderID;
            AggregationLevelSelection = settings.AggregationLevel;

            foreach (var existingProfile in Profiles)
            {
                existingProfile.PropertyChanged -= Profile_PropertyChanged;
            }
            Profiles.Clear();
            foreach (var profile in settings.Profiles ?? new System.Collections.Generic.List<VpinProfileSettings>())
            {
                AddProfile(profile.Name, profile.BucketVolSize, profile.NumberOfBuckets);
            }

            if (Profiles.Count == 0)
                AddProfile();

            _nextProfileNumber = Math.Max(_nextProfileNumber, Profiles.Count + 1);
            RefreshValidationMessage();
        }

        public System.Collections.Generic.List<VpinProfileSettings> ToProfileSettings()
        {
            return Profiles.Select(x => new VpinProfileSettings
            {
                Name = x.Name?.Trim() ?? string.Empty,
                BucketVolSize = x.BucketVolumeSize,
                NumberOfBuckets = x.NumberOfBuckets
            }).ToList();
        }

        private void ExecuteOkCommand(object obj)
        {
            RefreshValidationMessage();
            if (!string.IsNullOrWhiteSpace(ValidationMessage))
                return;

            UpdateSettingsFromUI?.Invoke();
            _actionCloseWindow?.Invoke();
        }

        private void ExecuteCancelCommand(object obj)
        {
            _actionCloseWindow?.Invoke();
        }

        private void ExecuteRemoveProfile(object obj)
        {
            if (obj is not VpinProfileItem profile)
                return;

            profile.PropertyChanged -= Profile_PropertyChanged;
            Profiles.Remove(profile);
            RaiseCanExecuteChanged();
            RefreshValidationMessage();
        }

        private bool CanExecuteOkCommand(object obj)
        {
            return string.IsNullOrWhiteSpace(this[nameof(SelectedProvider)]) &&
                   string.IsNullOrWhiteSpace(this[nameof(SelectedSymbol)]) &&
                   string.IsNullOrWhiteSpace(GetProfilesValidationMessage());
        }

        private void AddProfile(string? name = null, double bucketVolumeSize = 1, int numberOfBuckets = 50)
        {
            var profile = new VpinProfileItem
            {
                Name = string.IsNullOrWhiteSpace(name) ? $"Profile {_nextProfileNumber++}" : name,
                BucketVolumeSize = bucketVolumeSize,
                NumberOfBuckets = numberOfBuckets
            };
            profile.PropertyChanged += Profile_PropertyChanged;
            Profiles.Add(profile);
            RaiseCanExecuteChanged();
            RefreshValidationMessage();
        }

        private void Profile_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            RaiseCanExecuteChanged();
            RefreshValidationMessage();
        }

        private string? GetProfilesValidationMessage()
        {
            if (Profiles.Count == 0)
                return "Add at least one VPIN profile.";

            if (Profiles.Any(x => string.IsNullOrWhiteSpace(x.Name)))
                return "Each VPIN profile needs a name.";

            if (Profiles.GroupBy(x => x.Name.Trim(), StringComparer.OrdinalIgnoreCase).Any(x => x.Count() > 1))
                return "VPIN profile names must be unique.";

            if (Profiles.Any(x => x.BucketVolumeSize <= 0))
                return "Each VPIN profile needs a bucket volume size greater than zero.";

            if (Profiles.Any(x => x.NumberOfBuckets <= 0))
                return "Each VPIN profile needs a number of buckets greater than zero.";

            return null;
        }

        private void RefreshValidationMessage()
        {
            ValidationMessage = this[nameof(SelectedProvider)]
                ?? this[nameof(SelectedSymbol)]
                ?? GetProfilesValidationMessage()
                ?? string.Empty;
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
            _symbols = new ObservableCollection<string>(HelperSymbol.Instance);
            OnPropertyChanged(nameof(Symbols));
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

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
