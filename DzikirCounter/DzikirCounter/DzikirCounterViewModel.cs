// FILENAME: DzikirCounterViewModel.cs
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Media.Core;
using Windows.Media.Playback;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;
using System.Diagnostics;

namespace DzikirCounter
{
    public enum InputType { None, Keyboard, Mouse }

    public class DzikirCounterViewModel : INotifyPropertyChanged
    {
        // -------------------------------------------------------------
        // Audio & Visuals
        // -------------------------------------------------------------
        // Fixed: Made nullable to resolve CS8618
        private MediaPlayer? _popPlayer;
        private MediaPlayer? _successPlayer;
        private bool _isSoundEnabled = true;

        public bool IsSoundEnabled
        {
            get => _isSoundEnabled;
            set { if (_isSoundEnabled != value) { _isSoundEnabled = value; OnPropertyChanged(nameof(IsSoundEnabled)); SaveState(); } }
        }

        // -------------------------------------------------------------
        // STOPWATCH TIMER LOGIC
        // -------------------------------------------------------------
        // Fixed: Made nullable to resolve CS8618
        private DispatcherTimer? _stopwatch;
        private TimeSpan _elapsedTime;
        private bool _isTimerRunning;

        public string TimerDisplay => _elapsedTime.ToString(@"hh\:mm\:ss");

        public bool IsTimerRunning
        {
            get => _isTimerRunning;
            set { if (_isTimerRunning != value) { _isTimerRunning = value; OnPropertyChanged(nameof(IsTimerRunning)); } }
        }

        private void InitializeTimer()
        {
            _stopwatch = new DispatcherTimer();
            _stopwatch.Interval = TimeSpan.FromSeconds(1);
            _stopwatch.Tick += (s, e) =>
            {
                _elapsedTime = _elapsedTime.Add(TimeSpan.FromSeconds(1));
                OnPropertyChanged(nameof(TimerDisplay));
            };
        }

        public void StartTimer()
        {
            if (!_isTimerRunning && _stopwatch != null)
            {
                _stopwatch.Start();
                IsTimerRunning = true;
            }
        }

        public void PauseTimer()
        {
            if (_isTimerRunning && _stopwatch != null)
            {
                _stopwatch.Stop();
                IsTimerRunning = false;
            }
        }

        public void ResetTimer()
        {
            if (_stopwatch != null) _stopwatch.Stop();
            IsTimerRunning = false;
            _elapsedTime = TimeSpan.Zero;
            OnPropertyChanged(nameof(TimerDisplay));
        }

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
                    OnPropertyChanged(nameof(TargetProgress));
                    OnPropertyChanged(nameof(CounterColor));
                }
            }
        }

        // --- PRESETS & TARGETS ---

        public List<string> DzikirPresets { get; } = new List<string>
        {
            "Free Count (∞)",
            "Custom Goal...",
            "SubhanAllah (33)",
            "Alhamdulillah (33)",
            "Allahu Akbar (34)",
            "Istighfar (100)",
            "Tahlil (1000)",
            "Salawat (100)"
        };

        private string _selectedPreset = "Free Count (∞)";
        public string SelectedPreset
        {
            get => _selectedPreset;
            set
            {
                if (_selectedPreset != value)
                {
                    _selectedPreset = value;
                    OnPropertyChanged(nameof(SelectedPreset));
                    OnPropertyChanged(nameof(IsCustomTargetVisible));
                    ApplyPresetLogic();
                }
            }
        }

        private int _targetCount = 0;
        public int TargetCount
        {
            get => _targetCount;
            set
            {
                if (_targetCount != value)
                {
                    _targetCount = value;
                    OnPropertyChanged(nameof(TargetCount));
                    OnPropertyChanged(nameof(TargetDisplay));
                    OnPropertyChanged(nameof(IsTargetVisible));
                    OnPropertyChanged(nameof(TargetProgress));
                    OnPropertyChanged(nameof(CounterColor));
                    SaveState();
                }
            }
        }

        public string TargetDisplay => TargetCount == 0 ? "∞" : TargetCount.ToString();
        public Visibility IsTargetVisible => TargetCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        public double TargetProgress => (TargetCount > 0) ? Math.Min((double)CurrentCount, TargetCount) : 0;
        public Visibility IsCustomTargetVisible => SelectedPreset == "Custom Goal..." ? Visibility.Visible : Visibility.Collapsed;

        public SolidColorBrush CounterColor
        {
            get
            {
                if (TargetCount > 0 && CurrentCount >= TargetCount) return new SolidColorBrush(Colors.Green);
                if (Application.Current.Resources.TryGetValue("SystemAccentColorDark1", out object colorObj) && colorObj is Windows.UI.Color color) return new SolidColorBrush(color);
                return new SolidColorBrush(Colors.DodgerBlue);
            }
        }

        private void ApplyPresetLogic()
        {
            if (SelectedPreset == "Custom Goal...") { if (TargetCount == 0) TargetCount = 33; }
            else if (SelectedPreset.Contains("(33)")) TargetCount = 33;
            else if (SelectedPreset.Contains("(34)")) TargetCount = 34;
            else if (SelectedPreset.Contains("(100)")) TargetCount = 100;
            else if (SelectedPreset.Contains("(1000)")) TargetCount = 1000;
            else TargetCount = 0;
            SaveState();
        }

        // -------------------------------------------------------------
        // Standard Toggles
        // -------------------------------------------------------------

        private bool _isHookEnabled;
        public bool IsHookEnabled
        {
            get => _isHookEnabled;
            set { if (_isHookEnabled != value) { _isHookEnabled = value; OnPropertyChanged(nameof(IsHookEnabled)); SaveState(); } }
        }

        private bool _isLButtonHookEnabled;
        public bool IsLButtonHookEnabled
        {
            get => _isLButtonHookEnabled;
            set { if (_isLButtonHookEnabled != value) { _isLButtonHookEnabled = value; OnPropertyChanged(nameof(IsLButtonHookEnabled)); SaveState(); } }
        }

        private bool _isCustomInputEnabled;
        public bool IsCustomInputEnabled
        {
            get => _isCustomInputEnabled;
            set { if (_isCustomInputEnabled != value) { _isCustomInputEnabled = value; OnPropertyChanged(nameof(IsCustomInputEnabled)); SaveState(); } }
        }

        private int _customInputCode;
        public int CustomInputCode
        {
            get => _customInputCode;
            set { if (_customInputCode != value) { _customInputCode = value; SaveState(); } }
        }

        private InputType _customInputType = InputType.None;
        public InputType CustomInputType
        {
            get => _customInputType;
            set { if (_customInputType != value) { _customInputType = value; SaveState(); OnPropertyChanged(nameof(CustomInputNameDisplay)); } }
        }

        private string _customInputName = "None";
        public string CustomInputName
        {
            get => _customInputName;
            set { if (_customInputName != value) { _customInputName = value; SaveState(); OnPropertyChanged(nameof(CustomInputNameDisplay)); } }
        }

        public string CustomInputNameDisplay => $"Bound: {CustomInputName}";

        // -------------------------------------------------------------
        // Constructor & Logic
        // -------------------------------------------------------------

        private const string SaveFileName = "dzikir_counter_data_v3.txt";
        private static readonly SemaphoreSlim _fileSemaphore = new SemaphoreSlim(1, 1);

        public DzikirCounterViewModel()
        {
            _currentCount = 0;
            InitializeAudio();
            InitializeTimer(); // Init Timer
            LoadState();
        }

        private async void InitializeAudio()
        {
            try
            {
                _popPlayer = new MediaPlayer();
                var file = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///sound/pop.mp3"));
                _popPlayer.Source = MediaSource.CreateFromStorageFile(file);
            }
            catch { Debug.WriteLine("[AUDIO] Failed to load pop.mp3"); }

            try
            {
                _successPlayer = new MediaPlayer();
                var file = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///sound/success.mp3"));
                _successPlayer.Source = MediaSource.CreateFromStorageFile(file);
            }
            catch { Debug.WriteLine("[AUDIO] Failed to load success.mp3"); }
        }

        private void PlayPop() => PlaySound(_popPlayer);
        private void PlaySuccess() => PlaySound(_successPlayer);

        private void PlaySound(MediaPlayer? player)
        {
            if (IsSoundEnabled && player != null)
            {
                try
                {
                    if (player.PlaybackSession.PlaybackState == MediaPlaybackState.Playing) player.PlaybackSession.Position = TimeSpan.Zero;
                    else player.Play();
                }
                catch { }
            }
        }

        public void Increment()
        {
            if (TargetCount > 0 && CurrentCount >= TargetCount)
            {
                CurrentCount = 1;
                PlayPop();
            }
            else if (CurrentCount < long.MaxValue)
            {
                CurrentCount++;

                if (TargetCount > 0 && CurrentCount == TargetCount)
                {
                    PlaySuccess();
                    PauseTimer(); // Auto-stop timer
                }
                else
                {
                    PlayPop();
                }
            }
            SaveState();
        }

        public void Decrement()
        {
            if (CurrentCount > 0) { CurrentCount--; PlayPop(); }
            SaveState();
        }

        public void Reset()
        {
            CurrentCount = 0;
            SaveState();
        }

        public async void SaveState()
        {
            await _fileSemaphore.WaitAsync();
            try
            {
                string data = $"{CurrentCount},{IsHookEnabled},{IsLButtonHookEnabled},{IsCustomInputEnabled},{CustomInputCode},{(int)CustomInputType},{CustomInputName},{SelectedPreset},{IsSoundEnabled},{TargetCount}";
                StorageFile file = await ApplicationData.Current.LocalFolder.CreateFileAsync(SaveFileName, CreationCollisionOption.ReplaceExisting);
                await FileIO.WriteTextAsync(file, data);
            }
            catch (Exception) { }
            finally { _fileSemaphore.Release(); }
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

                            if (parts.Length >= 7)
                            {
                                if (bool.TryParse(parts[3], out bool h3)) _isCustomInputEnabled = h3;
                                if (int.TryParse(parts[4], out int code)) _customInputCode = code;
                                if (int.TryParse(parts[5], out int typeVal)) _customInputType = (InputType)typeVal;
                                _customInputName = parts[6];
                            }

                            if (parts.Length >= 9)
                            {
                                SelectedPreset = parts[7];
                                if (bool.TryParse(parts[8], out bool sound)) _isSoundEnabled = sound;
                            }

                            if (parts.Length >= 10)
                            {
                                if (int.TryParse(parts[9], out int tCount)) _targetCount = tCount;
                            }

                            OnPropertyChanged(nameof(IsHookEnabled));
                            OnPropertyChanged(nameof(IsLButtonHookEnabled));
                            OnPropertyChanged(nameof(IsCustomInputEnabled));
                            OnPropertyChanged(nameof(CustomInputNameDisplay));
                            OnPropertyChanged(nameof(SelectedPreset));
                            OnPropertyChanged(nameof(IsSoundEnabled));
                            OnPropertyChanged(nameof(TargetCount));
                            OnPropertyChanged(nameof(IsCustomTargetVisible));
                        }
                    }
                }
            }
            catch (Exception) { }
            finally { if (_fileSemaphore.CurrentCount == 0) _fileSemaphore.Release(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}