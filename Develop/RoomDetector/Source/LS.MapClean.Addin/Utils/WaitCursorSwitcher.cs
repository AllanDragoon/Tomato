using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;

namespace LS.MapClean.Addin.Utils
{
    /// <summary>
    /// This class implements a disposable WaitCursor to show an hourglass while
    /// some long-running event occurs.
    /// </summary>
    /// <example>
    /// <![CDATA[
    /// 
    /// using (new WaitCursor())
    /// {
    ///    .. Do work here ..
    /// }
    /// 
    /// ]]>
    /// </example>
    public sealed class WaitCursorSwitcher : IDisposable
    {
        private readonly Cursor _oldCursor;

        /// <summary>
        /// Constructor
        /// </summary>
        public WaitCursorSwitcher()
        {
            _oldCursor = Mouse.OverrideCursor;
            Mouse.OverrideCursor = Cursors.Wait;
        }

        /// <summary>
        /// Returns the cursor to the default state.
        /// </summary>
        public void Dispose()
        {
            Mouse.OverrideCursor = _oldCursor;
            GC.SuppressFinalize(this);
        }
    }
}
