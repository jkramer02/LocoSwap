﻿using Serilog;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace LocoSwap
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public ObservableCollection<Route> Routes { get; } = new ObservableCollection<Route>();
        public ObservableCollection<Scenario> Scenarios { get; } = new ObservableCollection<Scenario>();
        public string WindowTitle { get; set; } = "LocoSwap";
        public MainWindow()
        {
            InitializeComponent();

            this.DataContext = this;
            this.WindowTitle = "LocoSwap " + Assembly.GetEntryAssembly().GetName().Version.ToString();

            var routeIds = Route.ListAllRoutes();

            foreach (var id in routeIds)
            {
                try
                {
                    Route route = new Route(id);
                    route.PropertyChanged += Route_PropertyChanged;
                    Routes.Add(route);
                }
                catch (Exception)
                {

                }
            }

            // Asynchronously read the scenario DB to populate the Scenario completion status
            ReadScenarioDb();

            Loaded += On_MainWindow_Loaded;
        }

        private void On_MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Add filter to RouteList
            CollectionView RouteListview = (CollectionView)CollectionViewSource.GetDefaultView(RouteList.ItemsSource);
            RouteListview.Filter = RouteFilter;

            // Add filter to ScenarioList
            CollectionView ScenarioListview = (CollectionView)CollectionViewSource.GetDefaultView(ScenarioList.ItemsSource);
            ScenarioListview.Filter = ScenarioFilter;
        }

        private void Route_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "IsFavorite")
            {
                Route route = sender as Route;
                StringCollection favorite = Properties.Settings.Default.FavoriteRoutes ?? new StringCollection();
                if (Properties.Settings.Default.FavoriteRoutes == null) Properties.Settings.Default.FavoriteRoutes = favorite;
                if (route.IsFavorite)
                {
                    Log.Debug("Adding {0} to favorite..", route.Name);
                    favorite.Add(route.Id);
                }
                else
                {
                    Log.Debug("Removing {0} from favorite..", route.Name);
                    favorite.Remove(route.Id);
                }
                Properties.Settings.Default.Save();
            }
        }

        private void RouteList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RouteList.SelectedItem != null)
            {
                Refresh_Scenario_List();
            }
            else
            {
                Scenarios.Clear();
            }
        }

        private void Refresh_Scenario_List()
        {
            Scenarios.Clear();
            var routeId = ((Route)RouteList.SelectedItem).Id;
            var scenarioIds = Scenario.ListAllScenarios(routeId);
            foreach (var id in scenarioIds)
            {
                try
                {
                    Scenarios.Add(new Scenario(routeId, id));
                }
                catch (Exception ex)
                {
                    Log.Debug("Exception caught when trying to list scenario {0}\\{1}: {2}", routeId, id, ex.Message);
                }
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var routeId = ((Route)RouteList.SelectedItem).Id;
            var scenarioId = ((Scenario)ScenarioList.SelectedItem).Id;
            var editWindow = new ScenarioEditWindow(routeId, scenarioId);
            editWindow.Show();
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            var scenario = (Scenario)ScenarioList.SelectedItem;
            Process.Start(scenario.ScenarioDirectory);
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            new SettingsWindow().ShowDialog();
        }

        private void ScenarioList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var dataContext = ((FrameworkElement)e.OriginalSource).DataContext;
            if (dataContext is Scenario)
            {
                if (RouteList.SelectedItem == null) return;
                var routeId = ((Route)RouteList.SelectedItem).Id;
                var scenarioId = ((Scenario)dataContext).Id;
                var editWindow = new ScenarioEditWindow(routeId, scenarioId);
                editWindow.Show();
            }
        }

        private void Delete_Scenarios_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult msgResult = MessageBox.Show(LocoSwap.Language.Resources.scenario_delete_prompt_message, LocoSwap.Language.Resources.scenario_delete_prompt_title, MessageBoxButton.YesNoCancel);
            if (msgResult == MessageBoxResult.Yes)
            {
                foreach (Scenario scenario in ScenarioList.SelectedItems)
                {
                    Directory.Delete(scenario.ScenarioDirectory, true);
                }
                Refresh_Scenario_List();
            }
        }

        private bool RouteFilter(object item)
        {
            if (string.IsNullOrEmpty(RouteFilterTextbox.Text))
                return true;

            Route candidateRoute = item as Route;

            string[] filteredProperties = {
                candidateRoute.Id,
                candidateRoute.Name
            };

            return RouteFilterTextbox.Text.Split(' ').All(
                filterToken => filteredProperties.Where(
                    prop => prop?.IndexOf(filterToken, StringComparison.OrdinalIgnoreCase) >= 0).ToArray().Length > 0
                );
        }

        private bool ScenarioFilter(object item)
        {
            Scenario candidateScenario = item as Scenario;

            // Hide played scenarios ?
            if (HidePlayedScenariosCheckbox.IsChecked == true && (
                candidateScenario.Completion == ScenarioDb.ScenarioCompletion.CompletedSuccessfully ||
                candidateScenario.Completion == ScenarioDb.ScenarioCompletion.CompletedFailed)
                )
            {
                return false;
            }

            // Textual filter
            if (string.IsNullOrEmpty(ScenarioFilterTextbox.Text))
                return true;

            string[] filteredProperties = {
                candidateScenario.Id,
                candidateScenario.Name,
                candidateScenario.Description,
                candidateScenario.PlayerTrainName,
                candidateScenario.Author
            };

            return ScenarioFilterTextbox.Text.Split(' ').All(
                filterToken => filteredProperties.Where(
                    prop => prop?.IndexOf(filterToken, StringComparison.OrdinalIgnoreCase) >= 0).ToArray().Length > 0
                );
        }

        public async void ReadScenarioDb()
        {
            var readDbTask = Task.Run(() =>
            {
                ScenarioDb.ParseScenarioDb();
            });

            await Task.WhenAll(readDbTask);

            // Refresh scenario list with completion
            CollectionViewSource.GetDefaultView(ScenarioList.ItemsSource).Refresh();
        }

        public void RouteFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            CollectionViewSource.GetDefaultView(RouteList.ItemsSource).Refresh();
        }

        public void ScenarioFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            CollectionViewSource.GetDefaultView(ScenarioList.ItemsSource).Refresh();
        }

        public void HidePlayedScenario_CheckboxChanged(object sender, RoutedEventArgs e)
        {
            CollectionViewSource.GetDefaultView(ScenarioList.ItemsSource).Refresh();
        }
    }
}
