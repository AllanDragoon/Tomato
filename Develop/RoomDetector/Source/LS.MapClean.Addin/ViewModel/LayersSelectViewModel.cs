using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.DatabaseServices;
using GalaSoft.MvvmLight;
using LS.MapClean.Addin.MapClean;
using LS.MapClean.Addin.ViewModel.Base;

namespace LS.MapClean.Addin.ViewModel
{
    public class LayersSelectViewModel : ViewModelBase
    {
        private MapCleanService _service;
        public LayersSelectViewModel(MapCleanService service, bool selectAll)
        {
            _service = service;
            IsSelectAll = selectAll;
            Initialize(selectAll);
        }

        private void Initialize(bool selectAll)
        {
            // Get all layers of document
            var layerNames = GetAllLayerNames();
            foreach (var layerName in layerNames)
            {
                Layers.Add(layerName, isChecked: selectAll);
            }
            foreach (var checkWrapper in Layers)
            {
                checkWrapper.PropertyChanged += OnIsCheckPropertyChanged;
            }
        }

        private CheckableObservableCollection<string> _layers = new CheckableObservableCollection<string>();
        public CheckableObservableCollection<string> Layers
        {
            get { return _layers; }
        }

        public IEnumerable<string> CheckedItems
        {
            get
            {
                var result = new List<string>();
                foreach (CheckWrapper<string> checkedItem in Layers.CheckedItems)
                {
                    result.Add(checkedItem.Value);
                }
                return result;
            }
        }

        private bool? _isSelectAll = true;
        public bool? IsSelectAll
        {
            get { return _isSelectAll; }
            set
            {
                _isSelectAll = value;
                if (!_fromInternal)
                {
                    _fromInternal = true;
                    if (_isSelectAll != null)
                    {
                        foreach (CheckWrapper<string> checkWrapper in Layers)
                        {
                            checkWrapper.IsChecked = _isSelectAll.Value;
                        }
                    }
                    _fromInternal = false;
                }
                RaisePropertyChanged("IsSelectAll");
            }
        }

        private bool _fromInternal = false;
        private void OnIsCheckPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (_fromInternal)
                return;

            if (e.PropertyName == "IsChecked")
            {
                _fromInternal = true;
                var checkedCount = Layers.Count(it => it.IsChecked);
                if (Layers.Count == checkedCount)
                    IsSelectAll = true;
                else if (checkedCount == 0)
                    IsSelectAll = false;
                else
                    IsSelectAll = null;

                _fromInternal = false;
            }
        }

        private IEnumerable<string> GetAllLayerNames()
        {
            var result = new List<string>();
            var database = _service.Document.Database;
            using (var transaction = database.TransactionManager.StartTransaction())
            {
                LayerTable layerTable = transaction.GetObject(database.LayerTableId, OpenMode.ForRead) as LayerTable;
                foreach (ObjectId layerId in layerTable)
                {
                    var layerTableRecord = transaction.GetObject(layerId, OpenMode.ForRead) as LayerTableRecord;
                    result.Add(layerTableRecord.Name);
                }
            }
            return result;
        }
    }
}
