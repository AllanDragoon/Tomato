using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace LS.MapClean.Addin.Framework
{
    public class DialogService
    {
        #region Singleton
        private static DialogService _instance = new DialogService();
        public static DialogService Instance
        {
            get { return _instance; }
        }
        #endregion

        private HashSet<FrameworkElement> _views;
        private Dictionary<Type, Type> _dialogTypes;

        public DialogService()
        {
            _views = new HashSet<FrameworkElement>();
            _dialogTypes = new Dictionary<Type, Type>();

            // Don't know if Application.Current.MainWindow is null in AutoCAD.
            try
            {
                var mainWindow = Application.Current.MainWindow;
                if (mainWindow != null)
                    MainHandle = new WindowInteropHelper(mainWindow).Handle;
            }
            catch
            {
            }
        }

        #region IDialogService Members
        /// <summary>
        /// Application's main window handle.
        /// </summary>
        public IntPtr MainHandle { get; set; }

        /// <summary>
        /// Registers a View.
        /// </summary>
        /// <param name="view">The registered View.</param>
        public void Register(FrameworkElement view)
        {
            Contract.Requires(view != null);
            Contract.Requires(!_views.Contains(view));

            // Get owner window
            Window owner = view as Window;
            if (owner == null)
            {
                owner = Window.GetWindow(view);
            }

            if (owner == null)
            {
                // use main window if we cannot get the owner
                owner = Application.Current.MainWindow;
            }

            if (owner == null)
            {
                throw new InvalidOperationException(/*MSG0*/"View is not contained within a Window.");
            }

            // Register for owner window closing, since we then should unregister View reference,
            // preventing memory leaks
            owner.Closed += OwnerClosed;
            _views.Add(view);
        }

        /// <summary>
        /// Unregisters a View.
        /// </summary>
        /// <param name="view">The unregistered View.</param>
        public void Unregister(FrameworkElement view)
        {
            Contract.Requires(_views.Contains(view));

            _views.Remove(view);
        }

        /// <summary>
        /// Register a dialog type and its viewmodel type.
        /// </summary>
        /// <param name="dialogType"></param>
        /// <param name="viewmodelType"></param>
        public void RegisterDialogType(Type dialogType, Type viewmodelType)
        {
            Contract.Requires(dialogType != null);
            Contract.Requires(viewmodelType != null);

            _dialogTypes[viewmodelType] = dialogType;
        }

        /// <summary>
        /// Create a dialog by its viewmodel.
        /// </summary>
        /// <param name="viewmodel"></param>
        /// <returns></returns>
        public object CreateDialog(object ownerViewModel, object viewmodel)
        {
            Contract.Requires(viewmodel != null);

            // Find the dialog type from _dialogTypes
            Type dilaogType = null;
            if (!_dialogTypes.TryGetValue(viewmodel.GetType(), out dilaogType))
                return null;

            var dialog = Activator.CreateInstance(dilaogType) as Window;
            if (dialog != null)
            {
                dialog.Owner = FindOwnerWindow(ownerViewModel);
                if (dialog.Owner == null)
                {
                    if (MainHandle != null && MainHandle != IntPtr.Zero)
                    {
                        WindowInteropHelper wih = new WindowInteropHelper(dialog);
                        wih.Owner = MainHandle;
                    }
                    else
                    {
                        dialog.Owner = Application.Current.MainWindow;
                    }
                }

                dialog.DataContext = viewmodel;
            }
            return dialog;
        }

        /// <summary>
        /// Create a dialog.
        /// </summary>
        /// <param name="ownerViewModel">A ViewModel that represents the owner window of the
        /// dialog.</param>
        /// <param name="viewModel">The ViewModel of the new dialog.</param>
        /// <returns>A nullable value of type bool that signifies how a window was closed by the
        /// user.</returns>
        public T CreateDialog<T>(object ownerViewModel, object viewModel) where T : Window
        {
            Contract.Requires(viewModel != null);

            // Create dialog and set properties
            T dialog = Activator.CreateInstance<T>();

            // Assign dialog's owner.
            dialog.Owner = FindOwnerWindow(ownerViewModel);
            if (dialog.Owner == null)
            {
                if (MainHandle != null && MainHandle != IntPtr.Zero)
                {
                    WindowInteropHelper wih = new WindowInteropHelper(dialog);
                    wih.Owner = MainHandle;
                }
                else
                {
                    dialog.Owner = Application.Current.MainWindow;
                }
            }

            dialog.DataContext = viewModel;
            return dialog;
        }

        /// <summary>
        /// Close a dialog.
        /// </summary>
        /// <param name="viewModel">The view model of the dialog or dialog's child view.</param>
        /// <param name="dialogResult"></param>
        public void CloseDialog(object viewModel, bool dialogResult)
        {
            var window = FindOwnerWindow(viewModel);
            if (window == null)
                return;

            window.DialogResult = dialogResult;
        }

        public void HideDialog(object viewModel)
        {
            var window = FindOwnerWindow(viewModel);
            if (window == null)
                return;
            window.Hide();
        }

        public void ShowDialog(object viewModel)
        {
            var window = FindOwnerWindow(viewModel);
            if (window == null)
                return;
            window.ShowDialog();
        }

        /// <summary>
        /// Shows a message box.
        /// </summary>
        /// <param name="ownerViewModel">A ViewModel that represents the owner window of the message
        /// box.</param>
        /// <param name="messageBoxText">A string that specifies the text to display.</param>
        /// <param name="caption">A string that specifies the title bar caption to display.</param>
        /// <param name="button">A MessageBoxButton value that specifies which button or buttons to
        /// display.</param>
        /// <param name="icon">A MessageBoxImage value that specifies the icon to display.</param>
        /// <returns>A MessageBoxResult value that specifies which message box button is clicked by the
        /// user.</returns>
        public MessageBoxResult ShowMessageBox(object ownerViewModel, string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon, MessageBoxResult defaultResult)
        {
            Contract.Requires(!String.IsNullOrEmpty(messageBoxText));
            Contract.Requires(!String.IsNullOrEmpty(caption));

            Window ownerWindow = FindOwnerWindow(ownerViewModel);
            if (ownerWindow != null)
                return MessageBox.Show(ownerWindow, messageBoxText, caption, button, icon, defaultResult);
            else
                return MessageBox.Show(messageBoxText, caption, button, icon, defaultResult);
        }

        /// <summary>
        /// Shows a message box for displaying exception message.
        /// </summary>
        /// <param name="ownerViewModel">A ViewModel that represents the owner window of the message box.</param>
        /// <param name="ex">A exception whose message specifies the text to display.</param>
        /// <param name="extraMessage">Extra message which will append after exception's message.</param>
        /// <param name="caption">A string that specifies the title bar caption to display.</param>
        /// <param name="icon">A MessageBoxImage value that specifies the icon to display.</param>
        /// <returns>A MessageBoxResult value that specifies which message box button is clicked by the user.</returns>
        public MessageBoxResult ShowErrorMessage(object ownerViewModel, Exception ex, string extraMessage, string caption = "", MessageBoxImage icon = MessageBoxImage.Error)
        {
            StringBuilder sb = new StringBuilder(ex.Message);
            AddInnerExceptionMessages(ex.InnerException, sb);
            if (!String.IsNullOrEmpty(extraMessage))
            {
                sb.Append("\n\n");
                sb.Append(extraMessage);
            }

            return ShowMessageBox(ownerViewModel, sb.ToString(), caption, MessageBoxButton.OK, icon, MessageBoxResult.None);
        }

        private static void AddInnerExceptionMessages(System.Exception ex, StringBuilder sb)
        {
            if (ex != null)
            {
                sb.Append("\n\n - ").Append(ex.Message);
                AddInnerExceptionMessages(ex.InnerException, sb);
            }
        }

        ///// <summary>
        ///// Shows the OpenFileDialog.
        ///// </summary>
        ///// <param name="ownerViewModel">A ViewModel that represents the owner window of the
        ///// dialog</param>
        ///// <param name="openFileDialog">The interface of a open file dialog.</param>
        ///// <returns>DialogResult.OK if successful; otherwise DialogResult.Cancel.</returns>
        //public DialogResult ShowOpenFileDialog(object ownerViewModel, IOpenFileDialog openFileDialog)
        //{
        //    Contract.Requires(ownerViewModel != null);
        //    Contract.Requires(openFileDialog != null);

        //    // Create OpenFileDialog with specified ViewModel
        //    OpenFileDialog dialog = new OpenFileDialog(openFileDialog);

        //    // Show dialog
        //    return dialog.ShowDialog(new WindowWrapper(FindOwnerWindow(ownerViewModel)));
        //}

        ///// <summary>
        ///// Shows the FolderBrowserDialog.
        ///// </summary>
        ///// <param name="ownerViewModel">A ViewModel that represents the owner window of the dialog.
        ///// </param>
        ///// <param name="folderBrowserDialog">The interface of a folder browser dialog.</param>
        ///// <returns>The DialogResult.OK if successful; otherwise DialogResult.Cancel.</returns>
        //public DialogResult ShowFolderBrowserDialog(object ownerViewModel, FolderBrowserDialogViewModel viewModel)
        //{
        //    Contract.Requires(ownerViewModel != null);
        //    Contract.Requires(viewModel != null);

        //    // Create/Show FolderBrowserDialog with specified ViewModel
        //    FolderBrowserDialog dialog = new FolderBrowserDialog(viewModel);
        //    return dialog.ShowDialog(new WindowWrapper(FindOwnerWindow(ownerViewModel)));
        //}

        #endregion

        /// <summary>
        /// Finds window corresponding to specified ViewModel.
        /// </summary>
        private Window FindOwnerWindow(object owner)
        {
            if (owner == null)
                return null;

            // Support direct use of owner window
            Window ownerWindow = owner as Window;
            if (ownerWindow != null) return ownerWindow;

            FrameworkElement view = _views.SingleOrDefault(v => ReferenceEquals(v.DataContext, owner));
            if (view == null)
            {
                return null;
                // throw new ArgumentException(/*MSG0*/"Viewmodel is not referenced by any registered View.");
            }

            // Get owner window
            ownerWindow = view as Window;
            if (ownerWindow == null)
            {
                ownerWindow = Window.GetWindow(view);
            }

            return ownerWindow;
        }

        /// <summary>
        /// Handles owner window closed, View service should then unregister all Views acting
        /// within the closed window.
        /// </summary>
        private void OwnerClosed(object sender, EventArgs e)
        {
            Window owner = sender as Window;
            if (owner != null)
            {
                // Find Views acting within closed window
                // Unregister Views in window
                IEnumerable<FrameworkElement> windowViews = _views.Where(v => Window.GetWindow(v) == owner);
                foreach (FrameworkElement view in windowViews.ToArray())  // Use array to make sure to go through enumeration before unregistering
                {
                    Unregister(view);
                }
            }
        }
    }
}
