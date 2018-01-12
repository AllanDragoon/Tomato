using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LS.MapClean.Addin.MapClean
{
    [Flags]
    public enum ActionStatus
    {
        Disabled = 0,
        Pending = 1,
        Executed = 2,
        Failed = 4
    }

    public class ActionAgent
    {
        /// <summary>
        /// Action type of action agent.
        /// </summary>
        public ActionType ActionType { get; set; }

        /// <summary>
        /// Action types that this action depends, that is, only those actions
        /// are exectued, this action is enabled.
        /// </summary>
        public ActionType[] Dependencies { get; set; }

        /// <summary>
        /// Action's status.
        /// </summary>
        private ActionStatus _status = ActionStatus.Disabled;
        public ActionStatus Status
        {
            get { return _status; }
            set
            {
                _status = value;
                RaiseStatusChangedEvent();
            }
        }

        /// <summary>
        /// Name of action
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Related action.
        /// </summary>
        public MapCleanActionBase Action { get; set; }

        #region Events
        public event EventHandler StatusChanged;
        private void RaiseStatusChangedEvent()
        {
            if (StatusChanged != null)
                StatusChanged(this, new EventArgs());
        }
        #endregion
    }
}
