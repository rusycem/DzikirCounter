// FILENAME: DzikirCounterViewModel.cs
using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using System.Diagnostics;
using System.Threading;

namespace DzikirCounter
{
    public enum InputType { None, Keyboard, Mouse }

    public class DzikirCounterViewModel : INotifyPropertyChanged
    {
        // -------------------------------------------------------------
        // Public Properties
        // -------------------------------------------------------------

        private long _currentCount;
        public long CurrentCount
        {
            get => _currentCount;
            private set
            {
                if (_currentCount != value)
                {
                    _currentCount = value;
                    OnPropertyChanged(nameof(CurrentCount));
                }
            }
        }

        // Toggle 1
        private bool _isHookEnabled;
        public bool IsHookEnabled
        {
            get => _isHookEnabled;
            set
            {
                if (_isHookEnabled != value)
                {
                    _isHookEnabled = value;
                    OnPropertyChanged(nameof(IsHookEnabled));
                    SaveState();
                }
            }
        }

        // Toggle 2
        private bool _isLButtonHookEnabled;
        public bool IsLButtonHookEnabled
        {
            get => _isLButtonHookEnabled;
            set
            {
                if (_isLButtonHookEnabled != value)
                {
                    _isLButtonHookEnabled = value;
                    OnPropertyChanged(nameof(IsLButtonHookEnabled));
                    SaveState();
                }
            }
        }

        // --- NEW: Custom Input Properties (Toggle 3) ---

        private bool _isCustomInputEnabled;
        public bool IsCustomInputEnabled
        {
            get => _isCustomInputEnabled;
            set
            {
                if (_isCustomInputEnabled != value)
                {
                    _isCustomInputEnabled = value;
                    OnPropertyChanged(nameof(IsCustomInputEnabled));
                    SaveState();
                }
            }
        }

        private int _customInputCode;
        public int CustomInputCode
        {
            get => _customInputCode;
            set
            {
                if (_customInputCode != value)
                {
                    _customInputCode = value;
                    SaveState();
                }
            }
        }

        private InputType _customInputType = InputType.None;
        public InputType CustomInputType
        {
            get => _customInputType;
            set
            {
                if (_customInputType != value)
                {
                    _customInputType = value;
                    SaveState();
                    OnPropertyChanged(nameof(CustomInputNameDisplay)); // Update display text
                }
            }
        }

        private string _customInputName = "None";
        public string CustomInputName
        {
            get => _customInputName;
            set
            {
                if (_customInputName != value)
                {
                    _customInputName = value;
                    SaveState();
                    OnPropertyChanged(nameof(CustomInputNameDisplay));
                }
            }
        }

        // Helper for UI Binding
        public string CustomInputNameDisplay => $"Bound: {CustomInputName}";


        // -------------------------------------------------------------
        // Constructor
        // -------------------------------------------------------------

        private const string SaveFileName = "dzikir_counter_data.txt";
        private static readonly SemaphoreSlim _fileSemaphore = new SemaphoreSlim(1, 1);

        public DzikirCounterViewModel()
        {
            _currentCount = 0;
            OnPropertyChanged(nameof(CurrentCount));
            LoadState();
        }

        // -------------------------------------------------------------
        // Counter Logic
        // -------------------------------------------------------------

        public void Increment()
        {
            if (CurrentCount < long.MaxValue) CurrentCount++;
            SaveState();
        }

        public void Decrement()
        {
            if (CurrentCount > 0) CurrentCount--;
            SaveState();
        }

        public void Reset()
        {
            CurrentCount = 0;
            SaveState();
        }

        // -------------------------------------------------------------
        // Persistence (Safe)
        // -------------------------------------------------------------

        public async void SaveState()
        {
            await _fileSemaphore.WaitAsync();
            try
            {
                // Format: Count,Toggle1,Toggle2,Toggle3,CustomCode,CustomType,CustomName
                string data = $"{CurrentCount},{IsHookEnabled},{IsLButtonHookEnabled},{IsCustomInputEnabled},{CustomInputCode},{(int)CustomInputType},{CustomInputName}";

                StorageFile file = await ApplicationData.Current.LocalFolder.CreateFileAsync(
                    SaveFileName,
                    CreationCollisionOption.ReplaceExisting);

                await FileIO.WriteTextAsync(file, data);
            }
            catch (Exception) { }
            finally
            {
                _fileSemaphore.Release();
            }
        }

        public async void LoadState()
        {
            await _fileSemaphore.WaitAsync();
            try
            {
                IStorageItem? item = await ApplicationData.Current.LocalFolder.TryGetItemAsync(SaveFileName);

                if (item is StorageFile file)
                {
                    string data = await FileIO.ReadTextAsync(file);
                    if (!string.IsNullOrEmpty(data))
                    {
                        string[] parts = data.Split(',');

                        if (parts.Length >= 3)
                        {
                            if (long.TryParse(parts[0], out long c)) CurrentCount = c;
                            if (bool.TryParse(parts[1], out bool h1)) _isHookEnabled = h1;
                            if (bool.TryParse(parts[2], out bool h2)) _isLButtonHookEnabled = h2;

                            // Load new custom properties if available (backward compatibility)
                            if (parts.Length >= 7)
                            {
                                if (bool.TryParse(parts[3], out bool h3)) _isCustomInputEnabled = h3;
                                if (int.TryParse(parts[4], out int code)) _customInputCode = code;
                                if (int.TryParse(parts[5], out int typeVal)) _customInputType = (InputType)typeVal;
                                _customInputName = parts[6];
                            }

                            OnPropertyChanged(nameof(IsHookEnabled));
                            OnPropertyChanged(nameof(IsLButtonHookEnabled));
                            OnPropertyChanged(nameof(IsCustomInputEnabled));
                            OnPropertyChanged(nameof(CustomInputNameDisplay));
                        }
                    }
                }
                else
                {
                    // Create initial
                    _fileSemaphore.Release();
                    SaveState();
                    return;
                }
            }
            catch (Exception)
            {
                CurrentCount = 0;
            }
            finally
            {
                if (_fileSemaphore.CurrentCount == 0) _fileSemaphore.Release();
            }
        }

        // -------------------------------------------------------------
        // INotifyPropertyChanged
        // -------------------------------------------------------------

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}