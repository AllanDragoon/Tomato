using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Channels;
using System.Windows.Input;
using GalaSoft.MvvmLight.Messaging;
using LS.MapClean.Addin.MapClean;
using LS.MapClean.Addin.Settings;
using LS.MapClean.Addin.ViewModel.Base;
using LS.MapClean.Addin.ViewModel.Messaging;

namespace LS.MapClean.Addin.ViewModel
{
    public class CheckResultGroupViewModel : BrowserNodeViewModel
    {
        public CheckResultGroupViewModel(BrowserNodeViewModel parent, CheckResultGroup group)
            : base(parent, group, GetCheckResultGroupName(group))
        {
            if (group == null)
                throw new ArgumentNullException("group");

            InitializeChildren();
        }

        #region Properties
        public ActionType ActionType
        {
            get { return ResultGroup.ActionType; }
        }

        public CheckResultGroup ResultGroup
        {
            get { return (CheckResultGroup) DataObject; }
        }
        #endregion

        #region Overrides
        // Context menu
        protected override void AddContextMenuItems(ContextMenuItemCollection items)
        {
            base.AddContextMenuItems(items);

            var fixAllMenu = new ContextMenuViewModel("全部修复", FixAllCommand, false, false, false);
            items.Add(fixAllMenu);

            var rejectAllMenu = new ContextMenuViewModel("全部忽略", RejectAllCommand, true, _rejectAll, false);
            items.Add(rejectAllMenu);
        }

        #endregion

        #region Commands
        public ICommand FixAllCommand
        {
            get { return new RelayCommand(DoFixAll);}
        }

        private void DoFixAll()
        {
            Messenger.Default.Send<NotificationMessage<CheckResultGroupViewModel>>(
                new NotificationMessage<CheckResultGroupViewModel>(this, Notifications.FixCheckResult));
        }

        public ICommand RejectAllCommand
        {
            get { return new RelayCommand(DoRejectAll);}
        }

        private bool _rejectAll = false;
        private void DoRejectAll()
        {
            var rejectAll = !_rejectAll;
            foreach (var node in Children)
            {
                var checkResultVM = node as CheckResultNodeViewModel;
                if (checkResultVM == null)
                    continue;
                checkResultVM.RejectCheckResult(rejectAll);
            }
            _rejectAll = rejectAll;
        }
        #endregion

        #region Initialization

        private void InitializeChildren()
        {
            var index = 1;
            foreach (var checkResult in ResultGroup.CheckResults)
            {
                var nativeName = checkResult.ToChineseName() + "-" + index;
                var node = new CheckResultNodeViewModel(this, checkResult, nativeName);
                Children.Add(node);

                index++;
            }
        }
        #endregion

        #region Utils
        private static string GetCheckResultGroupName(CheckResultGroup group)
        {
            var chineseName = group.ActionType.ToChineseName();
            var count = group.CheckResults.Count();
            var result = chineseName + "(" + count + ")";
            return result;
        }
        #endregion
    }
}
