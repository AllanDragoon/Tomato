using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace LS.MapClean.Addin.ViewModel.Extensions
{
    public static class VisualUtils
    {
        static public void EnableVisual(Visual myVisual, bool enabled)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(myVisual); i++)
            {
                // Retrieve child visual at specified index value.
                Visual childVisual = (Visual)VisualTreeHelper.GetChild(myVisual, i);

                if (childVisual is TextBox)
                {
                    ((TextBox)(childVisual)).IsEnabled = enabled;
                }

                else
                {
                    // Do processing of the child visual object.

                    // Enumerate children of the child visual object.
                    EnableVisual(childVisual, enabled);
                }
            }
        }

        public static T FindVisualChildByName<T>(DependencyObject parent, string name) where T : FrameworkElement
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                string controlName = child.GetValue(Control.NameProperty) as string;
                if (controlName == name)
                {
                    return child as T;
                }
                else
                {
                    T result = FindVisualChildByName<T>(child, name);
                    if (result != null)
                        return result;
                }
            }
            return null;
        }

        public static T GetFirstChildOfType<T>(DependencyObject dependencyObject) where T : DependencyObject
        {
            if (dependencyObject == null)
            {
                return null;
            }

            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(dependencyObject); i++)
            {
                var child = VisualTreeHelper.GetChild(dependencyObject, i);

                var result = (child as T) ?? GetFirstChildOfType<T>(child);

                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        public static T FindVisualParent<T>(DependencyObject obj) where T : DependencyObject
        {
            try
            {
                DependencyObject parent = VisualTreeHelper.GetParent(obj);
                while (parent != null && !parent.GetType().Equals(typeof(T)))
                {
                    parent = VisualTreeHelper.GetParent(parent);
                }
                return parent as T;
            }
            catch
            {
                return null;
            }
        }
    }
}
