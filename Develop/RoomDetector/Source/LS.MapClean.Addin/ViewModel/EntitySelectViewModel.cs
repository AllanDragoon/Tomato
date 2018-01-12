using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Documents;
using System.Windows.Input;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using GalaSoft.MvvmLight;
using LS.MapClean.Addin.Framework;
using LS.MapClean.Addin.MapClean;
using LS.MapClean.Addin.Utils;
using LS.MapClean.Addin.View;
using LS.MapClean.Addin.ViewModel.Base;

namespace LS.MapClean.Addin.ViewModel
{
    public class EntitySelectViewModel : ViewModelBase
    {
        private MapCleanService _service;
        public EntitySelectViewModel(MapCleanService service)
        {
            _service = service;
            _layerSelectVM = new LayersSelectViewModel(_service, true);
            _fixLayerSelectVM = new LayersSelectViewModel(_service, false);
        }

        private LayersSelectViewModel _layerSelectVM = null;
        private LayersSelectViewModel _fixLayerSelectVM = null;

        #region Properties
        /// <summary>
        /// Whether select all entities.
        /// </summary>
        private bool _isSelectAll = true;
        public bool IsSelectAll
        {
            get { return _isSelectAll; }
            set
            {
                _isSelectAll = value;
                if (_isSelectAll)
                    LayersText = _selectAllText;

                RaisePropertyChanged("IsSelectAll");
            }
        }

        private const string _selectAllText = "*";
        private string _layersText = _selectAllText;
        public string LayersText
        {
            get { return _layersText; }
            set
            {
                _layersText = value;
                RaisePropertyChanged("LayersText");
            }
        }

        private string _entitySelectText = "";
        public string EntitySelectText
        {
            get { return _entitySelectText; }
            set
            {
                _entitySelectText = value;
                RaisePropertyChanged("EntitySelectText");
            }
        }

        private string _fixLayersText = "";
        public string FixLayersText
        {
            get { return _fixLayersText; }
            set
            {
                _fixLayersText = value;
                RaisePropertyChanged("FixLayersText");
            }
        }

        private string _fixEntitySelectText = "";
        public string FixEntitySelectText
        {
            get { return _fixEntitySelectText; }
            set
            {
                _fixEntitySelectText = value;
                RaisePropertyChanged("FixEntitySelectText");
            }
        }
        #endregion

        #region Commands
        public ICommand SelectEntitiesCommand
        {
            get { return new RelayCommand(DoSelectEntities, CanSelectEntities);}
        }

        private bool CanSelectEntities()
        {
            return !IsSelectAll;
        }

        private void DoSelectEntities()
        {
            DialogService.Instance.HideDialog(this);
            var editor = _service.Document.Editor;
            var options = new PromptSelectionOptions() {SingleOnly = false};
            var selection = editor.GetSelection(options);
            if (selection.Status == PromptStatus.OK)
            {
                m_SelectedObjectIds = selection.Value.GetObjectIds();
            }
            DialogService.Instance.ShowDialog(this);
        }

        private IEnumerable<ObjectId> m_SelectedObjectIds = new List<ObjectId>();
        public IEnumerable<ObjectId> SelectedObjectIds 
        {
            get
            {
                var selectedObjectIdList = m_SelectedObjectIds.ToList();
                foreach (ObjectId fixedObjectId in FixedObjectIds)
                {
                    if (selectedObjectIdList.Contains(fixedObjectId))
                        selectedObjectIdList.Remove(fixedObjectId);
                }

                return selectedObjectIdList;
            }
            set { value = m_SelectedObjectIds; }
        }

        private IEnumerable<ObjectId> m_fixedObjectIds = new List<ObjectId>();
        public IEnumerable<ObjectId> FixedObjectIds
        {
            get
            {
                var layerNameCollection = String.IsNullOrEmpty(FixLayersText) ? null : FixLayersText.Split(',').ToList();
                if (layerNameCollection == null)
                    return m_fixedObjectIds;

                var fixedObjectIds = m_fixedObjectIds.ToList();
                foreach (ObjectId fixedObjectId in m_fixedObjectIds)
                {
                    if (layerNameCollection.Contains(LayerUtils.GetLayerName(fixedObjectId)))
                        fixedObjectIds.Remove(fixedObjectId);
                }

                return fixedObjectIds;
            }
            set { value = m_fixedObjectIds; }
        }

        public ICommand SelectLayersCommand
        {
            get { return new RelayCommand(DoSelectLayers);}
        }

        private void DoSelectLayers()
        {
            var dialog = DialogService.Instance.CreateDialog<LayersSelectDialog>(this, _layerSelectVM);
            var result = dialog.ShowDialog();
            if (result != null && result.Value)
            {
                if (_layerSelectVM.IsSelectAll != null)
                {
                    LayersText = _selectAllText;
                }
                else
                {
                    var stringBuilder = new StringBuilder();
                    var checkedItems = _layerSelectVM.CheckedItems;
                    foreach (var item in checkedItems)
                    {
                        if (stringBuilder.Length != 0)
                            stringBuilder.Append(",");
                        stringBuilder.Append(item);
                    }
                    LayersText = stringBuilder.ToString();
                }
            }
        }

        public ICommand SelectFixEntitiesCommand
        {
            get { return new RelayCommand(DoSelectFixEntities);}
        }

        private void DoSelectFixEntities()
        {
            DialogService.Instance.HideDialog(this);
            var editor = _service.Document.Editor;
            var options = new PromptSelectionOptions() {SingleOnly = false};
            var selection = editor.GetSelection(options);
            if (selection.Status == PromptStatus.OK)
            {
                m_fixedObjectIds = selection.Value.GetObjectIds();
            }
            DialogService.Instance.ShowDialog(this);
        }

        public ICommand SelectFixLayersCommand
        {
            get { return new RelayCommand(DoSelectFixLayers); }
        }

        private void DoSelectFixLayers()
        {
            var dialog = DialogService.Instance.CreateDialog<LayersSelectDialog>(this, _fixLayerSelectVM);
            var result = dialog.ShowDialog();
            if (result != null && result.Value)
            {
                if (_fixLayerSelectVM.IsSelectAll != null)
                {
                    FixLayersText = _selectAllText;
                }
                else
                {
                    var stringBuilder = new StringBuilder();
                    var checkedItems = _fixLayerSelectVM.CheckedItems;
                    foreach (var item in checkedItems)
                    {
                        if (stringBuilder.Length != 0)
                            stringBuilder.Append(",");
                        stringBuilder.Append(item);
                    }
                    FixLayersText = stringBuilder.ToString();
                }
            }
        }
        #endregion
    }
}
