using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Forms;
using Autodesk.AutoCAD.Windows;
using LS.MapClean.Addin.MapClean;
using LS.MapClean.Addin.Utils;
using LS.MapClean.Addin.View;
using LS.MapClean.Addin.ViewModel;

namespace LS.MapClean.Addin.Palettes
{
    class ActionSequencePaletteSet : PaletteSetBase
    {
        private static Guid _paletteSetId = new Guid("273762BE-164A-45B1-9834-C728F5F7F792");
        private ActionSequenceViewModel _viewModel;
        public ActionSequenceViewModel ViewModel
        {
            get { return _viewModel; }
        }

        public ActionSequencePaletteSet()
        {
            // Don't save visible state for this palette.
            SaveVisibleState = false;

            InternalName = "ASPANEL";
            DisplayName = "图形清理";
            PaletteSetId = _paletteSetId;
            PaletteSetType = PaletteSetType.ActionSequence;
            InitialDockState = DockSides.None;
        }

        protected override Control CreateDefaultPaletteControl()
        {
            _viewModel = new ActionSequenceViewModel();
            var view = new ActionSequenceView() { DataContext = _viewModel };

            view.Margin = new Thickness(10);
            // http://stackoverflow.com/questions/592017/my-images-are-blurry-why-isnt-wpfs-snapstodevicepixels-working
            view.UseLayoutRounding = true;

            //for let wpf user control docked fully in the parent winform
            var formControl = WinFormsUtils.CreateElementHost(view);
            return formControl;
        }
    }
}
