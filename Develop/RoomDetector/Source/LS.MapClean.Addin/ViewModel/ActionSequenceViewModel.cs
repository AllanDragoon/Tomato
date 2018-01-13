using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GalaSoft.MvvmLight;
using LS.MapClean.Addin.MapClean;
using LS.MapClean.Addin.ViewModel.Base;

namespace LS.MapClean.Addin.ViewModel
{
    public class ActionSequenceViewModel : ViewModelBase
    {
        private ObservableCollection<ActionSequenceItem> _items = new ObservableCollection<ActionSequenceItem>();
        public ObservableCollection<ActionSequenceItem> Items
        {
            get { return _items; }
        }

        public ActionSequenceViewModel()
        {
            InitializeItems();
        }

        private void InitializeItems()
        {
            var service = MapCleanService.Instance;
            foreach (var pair in service.ActionAgents)
            {
                var commandName = GetCommandName(pair.Key);
                var item = new ActionSequenceItem(pair.Value, commandName)
                {
                    ImageSource = GetImageSource(pair.Key)
                };
                Items.Add(item);
            }

            if (MapCleanService.Instance.ShowIntegrateCheckItem)
            {
                var checkAllItem = new ActionSequenceItem(null, "JCSYX")
                {
                    ImageSource = GetImageSource("tools.png"),
                    NeedShowExecuted = false,
                    IsEnabled = true,
                    Name = "综合检查"
                };
                Items.Add(checkAllItem);
            }
        }

        public void Refresh()
        {
            Items.Clear();
            InitializeItems();
        }
        #region Utils

        private static string GetCommandName(ActionType actionType)
        {
            string commandName = "";
            switch (actionType)
            {
                case ActionType.NoneZeroElevation:
                    commandName = "GCBWL";
                    break;
                case ActionType.ArcSegment:
                    commandName = "JCHD";
                    break;
                case ActionType.RectifyPointDeviation:
                    commandName = "JHJJD";
                    break;
                case ActionType.BreakCrossing:
                    commandName = "DDJCX";
                    break;
                case ActionType.DeleteDuplicates:
                    commandName = "SCCFX";
                    break;
                case ActionType.ExtendUndershoots:
                    commandName = "YSWJD";
                    break;
                case ActionType.ApparentIntersection:
                    commandName = "XFWGJD";
                    break;
                case ActionType.SnapClustered:
                    commandName = "BZJDC";
                    break;
                case ActionType.EraseDangling:
                    commandName = "SCXGX";
                    break;
                case ActionType.ZeroAreaLoop:
                    commandName = "LMJBHX";
                    break;
                case ActionType.ZeroLength:
                    commandName = "SCLCDDX";
                    break;
                case ActionType.EraseShort:
                    commandName = "SCDDX";
                    break;
                case ActionType.UnclosedPolygon:
                    commandName = "FBHDBX";
                    break;
                case ActionType.SmallPolygon:
                    commandName = "XMJDBX";
                    break;
                case ActionType.IntersectPolygon:
                    commandName = "JCDBX";
                    break;
                case ActionType.SmallPolygonGap:
                    commandName = "DKJFX";
                    break;
                case ActionType.SelfIntersect:
                    commandName = "ZJDDX";
                    break;
            }
            return commandName;
        }

        private static ImageSource GetImageSource(ActionType actionType)
        {
            var filename = GetImageFileName(actionType);
            var img = GetImageSource(filename);
            return img;
        }

        private static ImageSource GetImageSource(string filename)
        {
            var img = new BitmapImage();
            img.BeginInit();
            var imgUri = new Uri("pack://application:,,,/LS.MapClean.Addin;component/Images/" + filename);
            img.UriSource = imgUri;
            img.EndInit();
            return img;
        }

        private static string GetImageFileName(ActionType actionType)
        {
            string filename = "";
            switch (actionType)
            {
                case ActionType.NoneZeroElevation:
                    filename = "none_zero_elevation.png";
                    break;
                case ActionType.ArcSegment:
                    filename = "arc_segment.png";
                    break;
                case ActionType.RectifyPointDeviation:
                    filename = "rectify_near_points.png";
                    break;
                case ActionType.BreakCrossing:
                    filename = "break_crossing.png";
                    break;
                case ActionType.DeleteDuplicates:
                    filename = "duplicate_entity.png";
                    break;
                case ActionType.ExtendUndershoots:
                    filename = "extend_undershoot.png";
                    break;
                case ActionType.ApparentIntersection:
                    filename = "apparent_intersection.png";
                    break;
                case ActionType.SnapClustered:
                    filename = "snap_cluster.png";
                    break;
                case ActionType.EraseDangling:
                    filename = "erase_dangling.png";
                    break;
                case ActionType.ZeroAreaLoop:
                    filename = "zero_area.png";
                    break;
                case ActionType.ZeroLength:
                    filename = "zero_length.png";
                    break;
                case ActionType.EraseShort:
                    filename = "erase_short.png";
                    break;
                case ActionType.UnclosedPolygon:
                    filename = "unclosed_polygon.png";
                    break;
                case ActionType.SmallPolygon:
                    filename = "small_area.png";
                    break;
                case ActionType.IntersectPolygon:
                    filename = "intersect_polygon.png";
                    break;
                case ActionType.SmallPolygonGap:
                    filename = "small_gap.png";
                    break;
                case ActionType.SelfIntersect:
                    filename = "self_intersect.png";
                    break;
            }
            return filename;
        }

        #endregion
    }

    public class ActionSequenceItem : ViewModelBase
    {
        #region Properties
        private ActionAgent _actionAgent = null;
        private string _name = null;
        public string Name
        {
            get { return _name; }
            set { _name = value; }
        }

        private string _commandName;

        private ImageSource _imageSource;
        public ImageSource ImageSource
        {
            get { return _imageSource; }
            set
            {
                _imageSource = value;
                RaisePropertyChanged("ImageSource");
            }
        }

        private bool _isEnabled;
        public bool IsEnabled
        {
            get { return _isEnabled; }
            set
            {
                _isEnabled = value;
                RaisePropertyChanged("IsEnabled");
            }
        }

        private bool _needShowExecuted = false;
        public bool NeedShowExecuted
        {
            get { return _needShowExecuted; }
            set
            {
                _needShowExecuted = value;
                RaisePropertyChanged("NeedShowExecuted");
            }
        }

        private bool _isExecutedFromInternal = false;
        private bool _isExecuted;
        public bool IsExecuted
        {
            get { return _isExecuted; }
            set
            {
                _isExecuted = value;
                RaisePropertyChanged("IsExecuted");

                if (!_isExecutedFromInternal)
                {
                    _isExecutedFromInternal = true;
                    if(value)
                        _actionAgent.Status |= ActionStatus.Executed;
                    else
                        _actionAgent.Status &= (~ActionStatus.Executed);

                    _isExecutedFromInternal = false;
                }
            }
        }
        #endregion

        public ActionSequenceItem(ActionAgent agent, string commandName)
        {
            _commandName = commandName;
            if (agent != null)
            {
                Name = agent.Name;
                _actionAgent = agent;
                _isExecutedFromInternal = true;
                IsEnabled = (_actionAgent.Status & ActionStatus.Pending) == ActionStatus.Pending;
                IsExecuted = (_actionAgent.Status & ActionStatus.Executed) == ActionStatus.Executed;
                _isExecutedFromInternal = false;
                agent.StatusChanged += ActionStatusChanged;
            }
        }

        private void ActionStatusChanged(object sender, EventArgs e)
        {
            var actionAgent = sender as ActionAgent;
            if (actionAgent == null)
                return;

            IsEnabled = (actionAgent.Status & ActionStatus.Pending) == ActionStatus.Pending;
            if (!_isExecutedFromInternal)
            {
                _isExecutedFromInternal = true;
                IsExecuted = (actionAgent.Status & ActionStatus.Executed) == ActionStatus.Executed;
                _isExecutedFromInternal = false;
            }    
        }

        public ICommand ActionCommand
        {
            get { return new RelayCommand(ExecuteAction);}
        }

        private void ExecuteAction()
        {
            var doc = MapCleanService.Instance.Document;
            doc.SendStringToExecute(_commandName + " ", true, false, true);
        }
    }

}
