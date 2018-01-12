using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LS.MapClean.Addin.MapClean
{
    /// <summary>
    /// Event args for adding/removing CheckResultGroup.
    /// </summary>
    public class CheckResultGroupEventArgs : EventArgs
    {
        public CheckResultGroup[] CheckResultGroups { get; set; }
    }
}
