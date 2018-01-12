using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows.Shapes;
using Autodesk.AutoCAD.DatabaseServices;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Messaging;
using LS.MapClean.Addin.MapClean;
using LS.MapClean.Addin.View;
using LS.MapClean.Addin.ViewModel.Events;
using LS.MapClean.Addin.ViewModel.Messaging;

namespace LS.MapClean.Addin.ViewModel
{
    /// <summary>
    /// Map clean check results view model.
    /// </summary>
    public class CheckResultsViewModel : ViewModelBase
    {
        private readonly MapCleanService _service;

        #region Properties
        /// <summary>
        /// Check result groups
        /// </summary>
        private ObservableCollection<CheckResultGroupViewModel> _resultGroupVMs = new ObservableCollection<CheckResultGroupViewModel>();
        public ObservableCollection<CheckResultGroupViewModel> ResultGroupVMs
        {
            get { return _resultGroupVMs; }
        }
        #endregion

        #region Constructors
        public CheckResultsViewModel(MapCleanService service)
        {
            _service = service;
            InitializeGroupVMs();

            RegisterServiceEvents();
            RegisterBrowserEvents();
            RegisterMessages();
        }
        
        #endregion

        #region Methods
        public void Refresh()
        {
            ResultGroupVMs.Clear();
            InitializeGroupVMs();
        }

        public void Clear()
        {
            ResultGroupVMs.Clear();
        }
        #endregion

        #region Initialization
        private void InitializeGroupVMs()
        {
            foreach (var group in _service.CheckResultGroups.Values)
            {
                var vm = new CheckResultGroupViewModel(null, group);
                ResultGroupVMs.Add(vm);
            }
        }

        /// <summary>
        /// Use GalaSoft.MvvmLight to register messages
        /// </summary>
        private void RegisterMessages()
        {
            Messenger.Default.Register<NotificationMessage<CheckResultNodeViewModel>>(this, true, OnCheckResultNodeMessageReceived);
            Messenger.Default.Register<NotificationMessage<CheckResultGroupViewModel>>(this, true, OnCheckResultGroupMessageReceived);
        }

        /// <summary>
        /// Register map clean browser events.
        /// </summary>
        private void RegisterBrowserEvents()
        {
            BrowserEventsDispatcher.BrowserEventsHandler.DoubleClicked += OnCheckResultNodeDoubleClicked;
        }

        #endregion

        #region Event Handlers
        private void RegisterServiceEvents()
        {
            _service.CheckResultGroupsAdded += OnCheckResultGroupsAdded;
            _service.CheckResultGroupsRemoved += OnCheckResultGroupsRemoved;
        }

        private void OnCheckResultGroupsAdded(object sender, CheckResultGroupEventArgs e)
        {
            foreach (var group in e.CheckResultGroups)
            {
                var vm = new CheckResultGroupViewModel(null, group);
                ResultGroupVMs.Add(vm);
            }
        }

        private void OnCheckResultGroupsRemoved(object sender, CheckResultGroupEventArgs e)
        {
            foreach (var group in e.CheckResultGroups)
            {
                var vm = _resultGroupVMs.FirstOrDefault(it => it.DataObject == group);
                if (vm != null)
                    ResultGroupVMs.Remove(vm);
            }
        }

        private void OnCheckResultGroupMessageReceived(NotificationMessage<CheckResultGroupViewModel> notification)
        {
            if (notification.Notification == Notifications.FixCheckResult)
            {
                var checkResults = notification.Content.Children.Select(it => (CheckResult)it.DataObject).ToList();
                _service.FixCheckResults(checkResults, updateScreen:true);
            }
        }

        private void OnCheckResultNodeMessageReceived(NotificationMessage<CheckResultNodeViewModel> notification)
        {
            if (notification.Notification == Notifications.ViewCheckResult)
            {
                _service.HighlightCheckResult(notification.Content.CheckResult);
            }
            else if (notification.Notification == Notifications.FixCheckResult)
            {
                _service.FixCheckResult(notification.Content.CheckResult, updateScreen:true);
            }
        }

        private void OnCheckResultNodeDoubleClicked(object sender, BrowserEventArgs e)
        {
            var checkResultNode = e.BrowserNodeViewModel as CheckResultNodeViewModel;
            if (checkResultNode == null)
                return;
            _service.HighlightCheckResult(checkResultNode.CheckResult);
        }
        #endregion
    }
}
