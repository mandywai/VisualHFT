using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using VisualHFT.Helpers;

namespace MarketConnector.Template.ViewModels
{
    // Settings UI ViewModel. Mirrors the pattern used by the canonical Binance /
    // Bitfinex / Kraken plug-ins: each editable field is a property on the VM,
    // an UpdateSettingsFromUI callback hands the edited values back to the
    // owning plug-in, and the OK / Cancel commands drive the surrounding window.
    public class PluginSettingsViewModel : INotifyPropertyChanged, IDataErrorInfo
    {
        private string _apiKey = string.Empty;
        private string _apiSecret = string.Empty;
        private int _depthLevels;
        private int _providerId;
        private string _providerName = string.Empty;
        private List<string> _symbols = new List<string>();
        private string _validationMessage = string.Empty;
        private string _successMessage = string.Empty;
        private readonly Action? _closeWindow;

        public ICommand OkCommand { get; }
        public ICommand CancelCommand { get; }

        // Owner plug-in sets this to apply the edited values to its settings
        // model. Invoked when the user clicks OK.
        public Action? UpdateSettingsFromUI { get; set; }

        public PluginSettingsViewModel(Action? closeWindow)
        {
            _closeWindow = closeWindow;
            OkCommand = new RelayCommand<object>(ExecuteOk, CanExecuteOk);
            CancelCommand = new RelayCommand<object>(ExecuteCancel);
        }

        public string ApiKey
        {
            get => _apiKey;
            set { _apiKey = value; OnPropertyChanged(nameof(ApiKey)); }
        }

        public string ApiSecret
        {
            get => _apiSecret;
            set { _apiSecret = value; OnPropertyChanged(nameof(ApiSecret)); }
        }

        public int DepthLevels
        {
            get => _depthLevels;
            set { _depthLevels = value; OnPropertyChanged(nameof(DepthLevels)); }
        }

        public int ProviderId
        {
            get => _providerId;
            set { _providerId = value; OnPropertyChanged(nameof(ProviderId)); }
        }

        public string ProviderName
        {
            get => _providerName;
            set { _providerName = value; OnPropertyChanged(nameof(ProviderName)); }
        }

        public List<string> Symbols
        {
            get => _symbols;
            set
            {
                _symbols = value ?? new List<string>();
                OnPropertyChanged(nameof(Symbols));
                OnPropertyChanged(nameof(SymbolsText));
            }
        }

        // Comma-joined textbox-friendly view over Symbols. Bound to a single
        // TextBox in the XAML so the user can edit the whole list as text.
        public string SymbolsText
        {
            get => string.Join(",", _symbols);
            set
            {
                _symbols = (value ?? string.Empty)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => s.Length > 0)
                    .ToList();
                OnPropertyChanged(nameof(SymbolsText));
                OnPropertyChanged(nameof(Symbols));
            }
        }

        public string ValidationMessage
        {
            get => _validationMessage;
            set { _validationMessage = value; OnPropertyChanged(nameof(ValidationMessage)); }
        }

        public string SuccessMessage
        {
            get => _successMessage;
            set { _successMessage = value; OnPropertyChanged(nameof(SuccessMessage)); }
        }

        // ---- IDataErrorInfo ------------------------------------------------
        public string Error => string.Empty;

        public string this[string columnName] => columnName switch
        {
            nameof(SymbolsText) when _symbols.Count == 0
                => "At least one symbol is required.",
            nameof(DepthLevels) when DepthLevels <= 0
                => "Depth levels must be greater than zero.",
            nameof(ProviderId) when ProviderId <= 0
                => "Provider ID must be a positive integer.",
            nameof(ProviderName) when string.IsNullOrWhiteSpace(ProviderName)
                => "Provider name cannot be empty.",
            _ => string.Empty
        };

        private bool CanExecuteOk(object _)
            => string.IsNullOrEmpty(this[nameof(SymbolsText)])
            && string.IsNullOrEmpty(this[nameof(DepthLevels)])
            && string.IsNullOrEmpty(this[nameof(ProviderId)])
            && string.IsNullOrEmpty(this[nameof(ProviderName)]);

        private void ExecuteOk(object _)
        {
            SuccessMessage = "Settings saved.";
            UpdateSettingsFromUI?.Invoke();
            _closeWindow?.Invoke();
        }

        private void ExecuteCancel(object _) => _closeWindow?.Invoke();

        // ---- INotifyPropertyChanged ---------------------------------------
        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            (OkCommand as RelayCommand<object>)?.RaiseCanExecuteChanged();
        }
    }
}
