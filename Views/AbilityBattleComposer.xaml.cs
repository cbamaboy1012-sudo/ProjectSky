using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using ProjectSky.ViewModels;

namespace ProjectSky.Views
{
    public partial class AbilityBattleComposer : Window
    {
        private readonly List<AbilityBattleComponent> _components = new();
        private static readonly string[] Features =
        {
            "On Switch In", "On Switch Out", "Before Move", "After Move", "Damage Modifier",
            "Status Prevention", "Status Infliction", "Stat Change", "Type Interaction",
            "Weather Interaction", "Terrain Interaction", "Speed Modifier", "Priority Modifier",
            "Critical Hit Modifier", "Move Power Modifier", "Healing", "Item Interaction",
            "Ability Suppression", "Contact Interaction", "Faint / KO Effect", "End Of Turn"
        };

        public AbilityBattleComposer()
        {
            InitializeComponent();
            var abilities = Application.Current.Properties["abilities"] as IEnumerable<string>
                            ?? new MainWindowViewModel().Abilities;
            var abilityList = abilities.ToList();
            BaseAbilityBox.ItemsSource = abilityList;
            SourceAbilityBox.ItemsSource = abilityList;
            FeatureBox.ItemsSource = Features;
            BaseAbilityBox.SelectedIndex = 0;
            SourceAbilityBox.SelectedIndex = 0;
            FeatureBox.SelectedIndex = 0;
            ComponentsGrid.ItemsSource = _components;
            UpdateSummary();
        }

        private void AddFeature_Click(object sender, RoutedEventArgs e)
        {
            var source = SourceAbilityBox.SelectedItem as string;
            var feature = FeatureBox.SelectedItem as string;
            if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(feature)) return;

            _components.Add(new AbilityBattleComponent
            {
                SourceAbility = source,
                Feature = feature,
                Description = BuildDescription(source, feature)
            });
            ComponentsGrid.Items.Refresh();
            UpdateSummary();
        }

        private void RemoveFeature_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is AbilityBattleComponent component)
            {
                _components.Remove(component);
                ComponentsGrid.Items.Refresh();
                UpdateSummary();
            }
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            _components.Clear();
            ComponentsGrid.Items.Refresh();
            UpdateSummary();
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            if (_components.Count == 0)
            {
                MessageBox.Show("Add at least one battle feature before exporting.", "Ability Composer", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var definition = new AbilityBattleDefinition
            {
                Name = string.IsNullOrWhiteSpace(AbilityNameBox.Text) ? "Custom Ability" : AbilityNameBox.Text.Trim(),
                BaseAbility = BaseAbilityBox.SelectedItem as string ?? "—",
                Components = _components.Select(x => new AbilityBattleComponent
                {
                    SourceAbility = x.SourceAbility,
                    Feature = x.Feature,
                    Description = x.Description
                }).ToList()
            };

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Export Ability Battle Function",
                FileName = definition.Name.Replace(' ', '_') + ".json",
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*"
            };
            if (dialog.ShowDialog() != true) return;

            var json = JsonSerializer.Serialize(definition, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(dialog.FileName, json);
            MessageBox.Show("Ability battle-function definition exported successfully.", "Ability Composer", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void UpdateSummary()
        {
            SummaryText.Text = _components.Count == 0
                ? "No features selected. Add features from existing abilities to build a custom battle function."
                : $"{_components.Count} feature(s) combined. Features are evaluated in the order shown above.";
        }

        private static string BuildDescription(string source, string feature) =>
            $"Use the '{feature}' behavior from {source}.";
    }

    public sealed class AbilityBattleDefinition
    {
        public string Name { get; set; } = "Custom Ability";
        public string BaseAbility { get; set; } = "—";
        public List<AbilityBattleComponent> Components { get; set; } = new();
    }

    public sealed class AbilityBattleComponent
    {
        public string SourceAbility { get; set; } = "—";
        public string Feature { get; set; } = "";
        public string Description { get; set; } = "";
    }
}
