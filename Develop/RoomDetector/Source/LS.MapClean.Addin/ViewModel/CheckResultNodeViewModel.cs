using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;
using System.Windows.Media;
using Autodesk.AutoCAD.DatabaseServices;
using GalaSoft.MvvmLight.Messaging;
using LS.MapClean.Addin.MapClean;
using LS.MapClean.Addin.Settings;
using LS.MapClean.Addin.ViewModel.Base;
using LS.MapClean.Addin.ViewModel.Messaging;

namespace LS.MapClean.Addin.ViewModel
{
    public class CheckResultNodeViewModel : BrowserNodeViewModel
    {
        private string _nativeName = null;

        public CheckResultNodeViewModel(BrowserNodeViewModel parent, CheckResult checkResult, string nativeName)
            : base(parent, checkResult, null)
        {
            if (checkResult == null)
                throw new ArgumentNullException("checkResult");
            _nativeName = nativeName;

            checkResult.StatusChanged += OnStatusChanged;
            Name = BuildCheckResultNodeName();
        }

        #region Properties
        public ActionType ActionType
        {
            get { return CheckResult.ActionType; }
        }
 
        public CheckResult CheckResult
        {
            get { return (CheckResult) DataObject; }
        }
        #endregion

        #region Overrides

        protected override void AddContextMenuItems(ContextMenuItemCollection items)
        {
            base.AddContextMenuItems(items);
            var viewMenu = new ContextMenuViewModel("查看图形", ViewCommand, false, false, false);
            items.Add(viewMenu);

            var fixMenu = new ContextMenuViewModel("修复", FixCommand, false, false, false);
            items.Add(fixMenu);

            // Seperator
            items.Add(null);

            var isChecked = CheckResult.Status == Status.Rejected;
            var rejectMenu = new ContextMenuViewModel("忽略", RejectCommand, true, isChecked, false);
            items.Add(rejectMenu);
        }

        #endregion

        #region Methods
        public void RejectCheckResult(bool reject)
        {
            // If it has been fixed, just return.
            if (CheckResult.Status != Status.Pending && CheckResult.Status != Status.Rejected)
                return;

            if (reject)
                CheckResult.Status = Status.Rejected;
            else
                CheckResult.Status = Status.Pending;
        }
        #endregion

        #region Commands

        /// <summary>
        /// Command to view check result
        /// </summary>
        public ICommand ViewCommand
        {
            get { return new RelayCommand(DoView);}
        }

        private void DoView()
        {
            Messenger.Default.Send<NotificationMessage<CheckResultNodeViewModel>>(
                new NotificationMessage<CheckResultNodeViewModel>(this, Notifications.ViewCheckResult));
        }

        /// <summary>
        /// Command to fix the check result.
        /// </summary>
        public ICommand FixCommand
        {
            get { return new RelayCommand(DoFix, CanDoFix);}
        }

        private void DoFix()
        {
            Messenger.Default.Send<NotificationMessage<CheckResultNodeViewModel>>(
                new NotificationMessage<CheckResultNodeViewModel>(this, Notifications.FixCheckResult));
        }

        private bool CanDoFix()
        {
            return CheckResult.Status == Status.Pending;
        }

        /// <summary>
        /// Keep the original entities - that is, won't fix.
        /// </summary>
        public ICommand RejectCommand
        {
            get { return new RelayCommand(DoReject, CanDoReject);}
        }

        private void DoReject()
        {
            bool reject = CheckResult.Status != Status.Rejected;
            RejectCheckResult(reject);
        }

        private bool CanDoReject()
        {
            return CheckResult.Status != Status.Fixed;
        }
        #endregion

        #region Utils
        private string BuildCheckResultNodeName()
        {
            if(CheckResult.Status == Status.Pending)
                return _nativeName;

            return _nativeName + ": " + CheckResult.Status.ToChineseName();
        }
        #endregion

        #region Events
        private void OnStatusChanged(object sender, EventArgs e)
        {
            Name = BuildCheckResultNodeName();
        }
        #endregion
    }
}
