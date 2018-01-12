using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Forms;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Windows;
using LS.MapClean.Addin.Algorithms;
using LS.MapClean.Addin.MapClean;
using LS.MapClean.Addin.Utils;
using LS.MapClean.Addin.View;
using LS.MapClean.Addin.ViewModel;

namespace LS.MapClean.Addin.Palettes
{
    class MapCleanPaletteSet : PaletteSetBase
    {
        private static Guid _paletteSetId = new Guid("B8168ED5-6DED-4C2B-9A78-2E72C938864E");

        private MapCleanPanelViewModel _viewModel;
        public MapCleanPanelViewModel ViewModel
        {
            get { return _viewModel; }
        }

        public MapCleanPaletteSet()
        {
            // Don't save visible state for this palette.
            SaveVisibleState = false;

            InternalName = "MCPANEL";
            DisplayName = "图形清理";
            PaletteSetId = _paletteSetId;
            PaletteSetType = PaletteSetType.MapClean;
            InitialDockState = DockSides.Left;
        }

        protected override Control CreateDefaultPaletteControl()
        {
            _viewModel = new MapCleanPanelViewModel(MapCleanService.Instance);
            var view = new MapCleanPanelView() { DataContext = _viewModel };

            view.Margin = new Thickness(10);
            // http://stackoverflow.com/questions/592017/my-images-are-blurry-why-isnt-wpfs-snapstodevicepixels-working
            view.UseLayoutRounding = true;

            //for let wpf user control docked fully in the parent winform
            var formControl = WinFormsUtils.CreateElementHost(view);
            return formControl;
        }
    }
}
