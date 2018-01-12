using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Windows.Forms.Integration;

namespace LS.MapClean.Addin.Utils
{
    public static class WinFormsUtils
    {
        /// <summary>
        /// Create and initialize an ElementHost object in an exception-safe way
        /// </summary>
        public static ElementHost CreateElementHost(System.Windows.UIElement child)
        {
            // see http://msdn.microsoft.com/en-us/library/ms182289(VS.100).aspx
            ElementHost host = null;
            ElementHost tempHost = null;
            try
            {
                tempHost = new ElementHost();
                tempHost.AutoSize = true;
                tempHost.Dock = DockStyle.Fill;
                tempHost.Child = child;

                // if we're here, no exceptions occurred while calling initialization properties/methods
                host = tempHost;
                tempHost = null; // don't Dispose() in "finally" block
            }
            finally
            {
                // tempHost will be null UNLESS an exception occurred
                if (tempHost != null)
                    tempHost.Dispose(); // in the middle of an exception, clean up new()'d instance
            }
            return host;
        }
    }
}
