using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.Runtime;

namespace DbxUtils.Utils
{
    /// <summary>
    /// An utility to temporarily disable overrule
    /// </summary>
    public sealed class OverruleDisabler : IDisposable
    {
        private bool _mOldOverruleState;

        public OverruleDisabler()
        {
            _mOldOverruleState = Overrule.Overruling;
            Overrule.Overruling = false;
        }

        public void Dispose()
        {
            Overrule.Overruling = _mOldOverruleState;
        }
    }
}
