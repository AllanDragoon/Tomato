using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Interop;

namespace LS.MapClean.Addin.Framework
{
    /// <summary>
    /// WindowWrapper is an IWin32Window wrapper around a WPF window.
    /// </summary>
    class WindowWrapper : System.Windows.Forms.IWin32Window
    {
        /// <summary>
        /// Construct a new wrapper taking a WPF window.
        /// </summary>
        /// <param name="window">The WPF window to wrap.</param>
        public WindowWrapper(Window window)
        {
            if (window != null)
                Handle = new WindowInteropHelper(window).Handle;
        }

        /// <summary>
        /// Construct a new wrapper taking a window handle.
        /// </summary>
        /// <param name="handle">The handle of the window to wrap.</param>
        public WindowWrapper(IntPtr handle)
        {
            Handle = handle;
        }

        /// <summary>
        /// Gets the handle to the window represented by the implementer.
        /// </summary>
        /// <returns>A handle to the window represented by the implementer.</returns>
        public IntPtr Handle { get; private set; }
    }
}
