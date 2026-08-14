using ProjectSky.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;

namespace ProjectSky.Views
{
    public partial class AreaRandomizer : Window
    {
        private string? _folder;
        private List<string> _files = new();

        public AreaRandomizer()
        {
            InitializeComponent();
            PropertyBox.ItemsSource = AreaRandomizerService.GetSupportedPropertyNames();
            PropertyBox.SelectedIndex = 0;
            StatusText.Text = "Choose a folder containing one JSON file per area, then select the areas to randomize.";
        }

        private void ChooseFolder_Click(object sender, RoutedEventArgs e)
        {
            using var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select the folder containing the area JSON files",
                UseDescriptionForTitle = true
            };
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

            _folder = dialog.SelectedPath;
            _files = Directory.GetFiles(_folder, "*.json", SearchOption.TopDirectoryOnly)
                .OrderBy(Path.GetFileName)
                .ToList();
            AreaList.ItemsSource = _files.Select(Path.GetFileNameWithoutExtension).ToList();
            FolderText.Text = $"{_files.Count} JSON area file(s) found:\n{_folder}";
            StatusText.Text = _files.Count == 0 ? "No JSON files were found in this folder." : "Select one, several, or all areas.";
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e) => AreaList.SelectAll();

        private void Clear_Click(object sender, RoutedEventArgs e) => AreaList.UnselectAll();

        private void Randomize_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_folder) || _files.Count == 0)
            {
                MessageBox.Show("Choose an area folder first.", "Area Randomizer", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var selectedNames = AreaList.SelectedItems.Cast<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
            var selectedFiles = _files.Where(x => selectedNames.Contains(Path.GetFileNameWithoutExtension(x))).ToList();
            if (selectedFiles.Count == 0)
            {
                MessageBox.Show("Select at least one area.", "Area Randomizer", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var property = PropertyBox.SelectedItem as string ?? "species";
            var mode = ModeBox.SelectedIndex == 0 ? AreaRandomizationMode.AllAreasTogether : AreaRandomizationMode.EachAreaSeparately;
            int? seed = null;
            if (!string.IsNullOrWhiteSpace(SeedBox.Text))
            {
                if (!int.TryParse(SeedBox.Text.Trim(), out var parsed))
                {
                    MessageBox.Show("The seed must be a whole number.", "Area Randomizer", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                seed = parsed;
            }

            var output = Path.Combine(_folder, "Randomized");
            try
            {
                var results = AreaRandomizerService.Randomize(selectedFiles, output, mode, property, seed);
                var slots = results.Sum(x => x.SlotsChanged);
                StatusText.Text = $"Randomized {results.Count} area(s) and {slots} slot(s). Output: {output}";
                MessageBox.Show($"Done!\n\nAreas: {results.Count}\nSlots randomized: {slots}\nOutput folder: {output}", "Area Randomizer", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Area Randomizer", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
