using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Documents;
using LS.MapClean.Addin.ViewModel;

namespace LS.MapClean.Addin.View
{
    public partial class LayersSelectorWindow
    {
        public LayersSelectorWindow()
        {
            InitializeComponent();
        }

        public void UpdateLayerNames()
        {
            var viewModel = DataContext as LayerSelectorViewModel;

            if (viewModel == null)
                return;

            var allLayerNames = new List<LayerName>();
            foreach (string layer in viewModel.AllLayers)
            {
                var layerName = new LayerName() { Name = layer };
                allLayerNames.Add(layerName);
            }
            lb.ItemsSource = allLayerNames;
        }

        private void SelectionButtonClick(object sender, RoutedEventArgs e)
        {
            DialogResult = true;

            var viewModel = DataContext as LayerSelectorViewModel;

            if (viewModel == null)
                return;

            if(lb.SelectedItems.Count < 1)
                return;

            viewModel.AvailableLayers.Clear();
            foreach (var selectedItem in lb.SelectedItems)
            {
                var layerName = selectedItem as LayerName;
                if(layerName != null)
                    viewModel.AvailableLayers.Add(layerName.Name);
            }
        }
    }

    public class LayerName
    {
        public string Name { get; set; }
    }
}
