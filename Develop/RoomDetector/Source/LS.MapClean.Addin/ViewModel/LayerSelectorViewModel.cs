using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GalaSoft.MvvmLight;

namespace LS.MapClean.Addin.ViewModel
{
    class LayerSelectorViewModel : ViewModelBase
    {
        public IList<string> AvailableLayers { get; set; }

        public IList<string> AllLayers { get; set; }
    }
}
