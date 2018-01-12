using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GalaSoft.MvvmLight;
using LS.MapClean.Addin.MapClean;
using LS.MapClean.Addin.ViewModel.Base;

namespace LS.MapClean.Addin.ViewModel
{
    public class ActionSelectViewModel : ViewModelBase
    {
        #region Properties
        /// <summary>
        /// Whether to break crossing objects, if it's checked, other mapclean options will be disabled.
        /// </summary>
        private bool _breakCrossingObjects = true;
        public bool BreakCrossingObjects
        {
            get { return _breakCrossingObjects; }
            set
            {
                _breakCrossingObjects = value;
                RaisePropertyChanged("BreakCrossingObjects");
            }
        }

        private CheckableObservableCollection<ActionAgent> _items = new CheckableObservableCollection<ActionAgent>();
        public CheckableObservableCollection<ActionAgent> Items
        {
            get { return _items; }
        }

        public IEnumerable<ActionAgent> CheckedItems
        {
            get
            {
                var result = new List<ActionAgent>();
                foreach (CheckWrapper<ActionAgent> checkedItem in Items.CheckedItems)
                {
                    result.Add(checkedItem.Value);
                }
                return result;
            }
        }

        private CheckWrapper<ActionAgent> _selectedItem = null;
        public CheckWrapper<ActionAgent> SelectedItem
        {
            get { return _selectedItem; }
            set
            {
                _selectedItem = value;
                // Update NeedTolerance property
                NeedTolerance = NeedToleranceForAction(_selectedItem.Value.Action);
                // Update Tolerance property
                Tolerance = _selectedItem.Value.Action.Tolerance;
                RaisePropertyChanged("SelectedItem");
            }
        }

        private bool _needTolerance = false;
        public bool NeedTolerance
        {
            get { return _needTolerance; }
            set
            {
                _needTolerance = value;
                RaisePropertyChanged("NeedTolerance");
            }
        }

        private double _tolerance = 0.0;
        public double Tolerance
        {
            get { return _tolerance; }
            set
            {
                _tolerance = value;
                if (SelectedItem != null)
                { 
                    SelectedItem.Value.Action.Tolerance = _tolerance;
                }
                RaisePropertyChanged("Tolerance");
            }
        }
        #endregion

        #region Constructors
        private MapCleanService _service;
        public ActionSelectViewModel(MapCleanService service)
        {
            _service = service;
            Initialize();
        }

        private void Initialize()
        {
            // Initialize all items
            foreach (var pair in _service.ActionAgents)
            {
                if (pair.Key == ActionType.BreakCrossing)
                    continue;

                Items.Add(pair.Value, isChecked: true);
            }

            SelectedItem = Items.First();
        }
        #endregion

        #region Methods
        public void SwitchSelectedActions()
        {
            if (BreakCrossingObjects)
            {
                BreakCrossingObjects = false;
                foreach (var checkWrapper in Items)
                {
                    checkWrapper.IsChecked = true;
                }
            }
            else
            {
                if (CheckedItems.Count() != Items.Count)
                {
                    foreach (var checkWrapper in Items)
                    {
                        checkWrapper.IsChecked = !checkWrapper.IsChecked;
                    }
                }
                else
                {
                    BreakCrossingObjects = true;
                }
            }
        }
        #endregion

        #region Utils
        bool NeedToleranceForAction(MapCleanActionBase action)
        {
            bool result = false;
            switch (action.ActionType)
            {
                case ActionType.ApparentIntersection:
                case ActionType.EraseDangling:
                case ActionType.EraseShort:
                case ActionType.ExtendUndershoots:
                case ActionType.SnapClustered:
                case ActionType.DeleteDuplicates:
                    result = true;
                    break;
            }

            return result;
        }
        #endregion
    }
}
