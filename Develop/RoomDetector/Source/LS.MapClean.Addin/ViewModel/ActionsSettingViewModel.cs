using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Data;
using System.Windows.Input;
using Autodesk.AutoCAD.DatabaseServices;
using GalaSoft.MvvmLight;
using LS.MapClean.Addin.MapClean;
using LS.MapClean.Addin.ViewModel.Base;

namespace LS.MapClean.Addin.ViewModel
{
    public class ActionsSettingViewModel : ViewModelBase
    {
        private MapCleanService _service;

        public ActionsSettingViewModel(MapCleanService service)
        {
            _service = service;

            // Create a ActionSelectViewModel.
            ActionSelectVM = new ActionSelectViewModel(service);
            // Create an EntitySelectViewModel
            EntitySelectVM = new EntitySelectViewModel(service);
        }

        public ActionSelectViewModel ActionSelectVM { get; private set; }
        public EntitySelectViewModel EntitySelectVM { get; private set; }

    }
}
