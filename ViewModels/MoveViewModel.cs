using CliWrap;
using CliWrap.Buffered;
using ProjectSky.Core;
using ProjectSky.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace ProjectSky.ViewModels
{
    public class MoveViewModel : ViewModel
    {
        private readonly Config configVals;
        private MoveDevID.DevID _mvDevID;
        private WazaArray.Wazas _moveOrig;
        private WazaArray.Wazas _moveNew;
        private MoveDevID.Move _selected;
        private CurrentMove _selectedMove;
        private string _searchText = "";

        public MoveDevID.DevID MvDevID { get => _mvDevID; private set { _mvDevID = value; OnPropertyChanged(); } }
        public WazaArray.Wazas _moveOriginal => _moveOrig;
        public WazaArray.Wazas _moveWorking => _moveNew;
        public CurrentMove SelectedMove { get => _selectedMove; private set { _selectedMove = value; OnPropertyChanged(); } }
        public MoveDevID.Move Selected
        {
            get => _selected;
            set
            {
                if (ReferenceEquals(_selected, value)) return;
                _selected = value;
                UpdateSelectedMove(_selected);
                OnPropertyChanged();
            }
        }

        public string SearchText
        {
            get => _searchText;
            set { if (_searchText == value) return; _searchText = value ?? ""; OnPropertyChanged(); RefreshFilteredMoves(); }
        }

        public ObservableCollection<MoveDevID.Move> FilteredMoves { get; } = new();
        public ObservableCollection<FlagBoxItemViewModel> FlagComboBoxItems { get; } = new();
        public RelayCommand ToggleCheckBoxCommand { get; }
        public RelayCommand SaveCommand { get; }
        public RelayCommand ResetCommand { get; }
        public RelayCommand DuplicateCommand { get; }
        public INavigationService NavigationService { get; }

        public MoveViewModel(INavigationService navService)
        {
            NavigationService = navService;
            NavigationService.NavigatedToViewModel += OnNavigatedToViewModel;
            var configLocation = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "config.json");
            using var reader = new StreamReader(configLocation);
            configVals = JsonSerializer.Deserialize<Config>(reader.ReadToEnd()) ?? throw new InvalidDataException("Invalid config.json");
            ToggleCheckBoxCommand = new RelayCommand(o => ToggleCheckBox(o), o => o is FlagBoxItemViewModel && SelectedMove?.Data != null);
            SaveCommand = new RelayCommand(async _ => await SaveAsync(), _ => _moveNew?.table != null);
            ResetCommand = new RelayCommand(_ => Reset(), _ => _moveOrig?.table != null);
            DuplicateCommand = new RelayCommand(_ => DuplicateSelected(), _ => Selected?.devName != null);
        }

        private async void OnNavigatedToViewModel(object sender, Type viewModelType)
        {
            if (viewModelType != typeof(MoveViewModel)) return;
            try { await LoadDataAsync(); }
            catch (Exception ex) { MessageBox.Show($"Unable to load moves:\n{ex.Message}", "Move Editor", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private async Task LoadDataAsync()
        {
            var movePath = Path.Combine(configVals.outPath, "waza_array.json");
            string moveJson;
            if (File.Exists(movePath))
                moveJson = await File.ReadAllTextAsync(movePath);
            else
            {
                using var stream = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/JSON/waza_array.json")).Stream;
                using var reader = new StreamReader(stream);
                moveJson = await reader.ReadToEndAsync();
            }
            using var devStream = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/JSON/move_list.json")).Stream;
            using var devReader = new StreamReader(devStream);
            var devJson = await devReader.ReadToEndAsync();

            _moveOrig = JsonSerializer.Deserialize<WazaArray.Wazas>(moveJson) ?? throw new InvalidDataException("Invalid waza_array.json");
            _moveNew = JsonSerializer.Deserialize<WazaArray.Wazas>(JsonSerializer.Serialize(_moveOrig))!;
            MvDevID = JsonSerializer.Deserialize<MoveDevID.DevID>(devJson) ?? throw new InvalidDataException("Invalid move_list.json");
            RefreshFilteredMoves();
            Selected = MvDevID.moves.FirstOrDefault(x => x.devName != null) ?? MvDevID.moves[0];
        }

        private void RefreshFilteredMoves()
        {
            FilteredMoves.Clear();
            if (MvDevID?.moves == null) return;
            var query = SearchText.Trim();
            foreach (var move in MvDevID.moves.Where(x => string.IsNullOrWhiteSpace(query) ||
                (x.name?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (x.devName?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)))
                FilteredMoves.Add(move);
        }

        private void UpdateSelectedMove(MoveDevID.Move waza)
        {
            if (waza == null || _moveNew?.table == null) return;
            var data = _moveNew.table.FirstOrDefault(x => string.Equals(x.move_id, waza.devName, StringComparison.OrdinalIgnoreCase));
            if (data == null) return;
            SelectedMove = new CurrentMove { Name = waza.name, Data = data, DevID = waza.devName };
            FillList();
        }

        private void FillList()
        {
            FlagComboBoxItems.Clear();
            if (SelectedMove?.Data == null) return;
            var attributes = new List<(string Flag, string Label)>
            {
                ("flag_protect", "Affected by Protect"), ("flag_mirror", "Can be Mirrored"), ("flag_makes_contact", "Makes Contact"),
                ("flag_metronome", "Selectable by Metronome"), ("flag_punch", "Punch Move"), ("flag_no_effectiveness", "No Effectiveness"),
                ("flag_charge", "Charge Boosted"), ("flag_no_sleep_talk", "Cannot be Sleep Talk"), ("flag_fail_instruct", "Can Make Instruct Fail"),
                ("flag_snatch", "Affected by Snatch"), ("flag_dance", "Dance Move"), ("flag_slicing", "Slicing Move"),
                ("flag_distance_triple", "Hits Triple-distance Targets"), ("flag_wind", "Wind Move"), ("flag_reflectable", "Reflectable"),
                ("flag_ignore_substitute", "Ignores Substitute"), ("flag_animate_ally", "Ally Animation/Swap"), ("flag_no_assist", "No Assist"),
                ("flag_fail_copy_cat", "Can Make Copy Cat Fail"), ("flag_gravity", "Affected by Gravity"), ("flag_fail_sky_battle", "Can Fail in Sky Battle"),
                ("flag_bite", "Bite Move"), ("flag_sound", "Sound Move"), ("unknown57", "Unknown 57"), ("unknown58", "Unknown 58"),
                ("unknown59", "Unknown 59"), ("unknown60", "Unknown 60")
            };
            foreach (var item in attributes)
            {
                var property = SelectedMove.Data.GetType().GetProperty(item.Flag);
                if (property == null) continue;
                FlagComboBoxItems.Add(new FlagBoxItemViewModel { Value = item.Label, Flag = item.Flag, IsChecked = property.GetValue(SelectedMove.Data) as bool? ?? false });
            }
        }

        public void ToggleCheckBox(object value)
        {
            if (value is not FlagBoxItemViewModel item || SelectedMove?.Data == null) return;
            var property = SelectedMove.Data.GetType().GetProperty(item.Flag);
            if (property == null || !property.CanWrite) return;
            property.SetValue(SelectedMove.Data, item.IsChecked);
        }

        private void DuplicateSelected()
        {
            if (SelectedMove?.Data == null || _moveNew?.table == null) return;
            var baseId = SelectedMove.Data.move_id + "_custom";
            var newId = baseId;
            var i = 2;
            while (_moveNew.table.Any(x => string.Equals(x.move_id, newId, StringComparison.OrdinalIgnoreCase))) newId = baseId + i++;
            var copy = JsonSerializer.Deserialize<WazaArray.Waza>(JsonSerializer.Serialize(SelectedMove.Data))!;
            copy.move_id = newId;
            _moveNew.table.Add(copy);
            var display = new MoveDevID.Move { devName = newId, name = SelectedMove.Name + " (Custom)", id = MvDevID.moves.Count + 1, type = copy.type ?? 0, power = copy.power };
            MvDevID.moves.Add(display);
            RefreshFilteredMoves();
            Selected = display;
        }

        public async Task SaveAsync()
        {
            try
            {
                var path = Path.Combine(configVals.outPath, "waza_array.json");
                Directory.CreateDirectory(configVals.outPath);
                var options = new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull, WriteIndented = true };
                await File.WriteAllTextAsync(path, JsonSerializer.Serialize(_moveNew, options));

                using var flatcExe = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/Flatc/flatc.exe")).Stream;
                using var fbs = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/Flatc/waza_array.bfbs")).Stream;
                var tempExePath = Path.Combine(configVals.outPath, "flatc.exe");
                var fbsPath = Path.Combine(configVals.outPath, "waza_array.fbs");
                await using (var exeOut = File.Create(tempExePath)) await flatcExe.CopyToAsync(exeOut);
                await using (var fbsOut = File.Create(fbsPath)) await fbs.CopyToAsync(fbsOut);
                var outputDir = Path.Combine(configVals.outPath, "romfs");
                Directory.CreateDirectory(outputDir);
                var result = await Cli.Wrap(tempExePath).WithArguments("-o romfs/world/data/trainer/trdata/ -b waza_array.fbs waza_array.json").WithWorkingDirectory(configVals.outPath).ExecuteBufferedAsync();
                if (result.ExitCode != 0) throw new InvalidOperationException(result.StandardError);
                File.Delete(tempExePath); File.Delete(fbsPath);
                var zipPath = Path.Combine(configVals.outPath, "project_sky_pokemon_mod.zip");
                if (File.Exists(zipPath)) File.Delete(zipPath);
                ZipFile.CreateFromDirectory(Path.Combine(configVals.outPath, "romfs"), zipPath);
                MessageBox.Show("Move changes compiled and the mod ZIP was created.", "Move Editor", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) { MessageBox.Show($"Save/compile failed:\n{ex.Message}", "Move Editor", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        public void Reset()
        {
            if (_moveOrig == null) return;
            _moveNew = JsonSerializer.Deserialize<WazaArray.Wazas>(JsonSerializer.Serialize(_moveOrig))!;
            RefreshFilteredMoves();
            if (Selected != null) UpdateSelectedMove(Selected);
        }

        public void Exit() => NavigationService.NavigateTo<HomeViewModel>();
    }
}
