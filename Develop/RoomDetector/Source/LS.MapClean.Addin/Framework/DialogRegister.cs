using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows;

namespace LS.MapClean.Addin.Framework
{
    public static class DialogRegister
    {
        private static Dictionary<object, bool> m_needRegisterViews = new Dictionary<object, bool>();
        #region Attached properties
        /// <summary>
        /// Attached property describing whether a FrameworkElement is acting as a View in MVVM.
        /// </summary>
        static readonly DependencyProperty IsRegisteredViewProperty = DependencyProperty.RegisterAttached(
            "IsRegisteredView", typeof(bool), typeof(DialogService), new UIPropertyMetadata(IsRegisteredViewPropertyChanged));

        /// <summary>
        /// Gets value describing whether FrameworkElement is acting as View in MVVM.
        /// </summary>
        public static bool GetIsRegisteredView(FrameworkElement target)
        {
            return (bool)target.GetValue(IsRegisteredViewProperty);
        }

        /// <summary>
        /// Sets value describing whether FrameworkElement is acting as View in MVVM.
        /// </summary>
        public static void SetIsRegisteredView(FrameworkElement target, bool value)
        {
            target.SetValue(IsRegisteredViewProperty, value);
        }

        /// <summary>
        /// Is responsible for handling IsRegisteredViewProperty changes, i.e. whether
        /// FrameworkElement is acting as View in MVVM or not.
        /// </summary>
        static void IsRegisteredViewPropertyChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
        {
            // The Visual Studio Designer or Blend will run this code when setting the attached
            // property, however at that point there is no IDialogService registered
            // in the ServiceLocator which will cause the Resolve method to throw a ArgumentException.
            if (DesignerProperties.GetIsInDesignMode(target)) return;

            FrameworkElement view = target as FrameworkElement;
            if (view == null)
                return;

            if (view.IsLoaded)
            {
                RegisterView(view, (bool)e.NewValue);
            }
            else
            {
                m_needRegisterViews.Add(view, (bool)e.NewValue);
                view.Loaded += View_Loaded;
            }
        }

        private static void View_Loaded(object sender, RoutedEventArgs e)
        {
            FrameworkElement view = sender as FrameworkElement;
            if (view == null)
                return;
            view.Loaded -= View_Loaded;
            bool register = true;
            m_needRegisterViews.TryGetValue(view, out register);
            m_needRegisterViews.Remove(view);

            RegisterView(view, register);
        }

        private static void RegisterView(FrameworkElement view, bool register)
        {
            if (register)
            {
                DialogService.Instance.Register(view);
            }
            else
            {
                DialogService.Instance.Unregister(view);
            }
        }

        #endregion
    }
}
