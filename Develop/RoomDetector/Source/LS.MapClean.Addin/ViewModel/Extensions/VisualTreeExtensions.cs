using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;

namespace LS.MapClean.Addin.ViewModel.Extensions
{
    /// <summary>
    /// http://www.cnblogs.com/mgen/archive/2011/08/31/2160581.html
    /// </summary>
    public static class VisualTreeExtensions
    {
        public static T GetVisualAncestor<T>(this DependencyObject dependencyObject) where T : class
        {
            DependencyObject item = VisualTreeHelper.GetParent(dependencyObject);

            while (item != null)
            {
                T itemAsT = item as T;
                if (itemAsT != null)
                    return itemAsT;
                item = VisualTreeHelper.GetParent(item);
            }

            return null;
        }

        public static DependencyObject GetVisualAncestor(this DependencyObject dependencyObject, Type type)
        {
            DependencyObject item = VisualTreeHelper.GetParent(dependencyObject);

            while (item != null)
            {
                if (item.GetType() == type)
                    return item;
                item = VisualTreeHelper.GetParent(item);
            }

            return null;
        }

        public static T GetVisualDescendent<T>(this DependencyObject dependencyObject) where T : DependencyObject
        {
            return dependencyObject.GetVisualDescendents<T>().FirstOrDefault();
        }

        public static IEnumerable<T> GetVisualDescendents<T>(this DependencyObject dependencyObject) where T : DependencyObject
        {
            int childCount = VisualTreeHelper.GetChildrenCount(dependencyObject);

            for (int n = 0; n < childCount; n++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(dependencyObject, n);

                if (child is T)
                {
                    yield return (T)child;
                }

                foreach (T match in GetVisualDescendents<T>(child))
                {
                    yield return match;
                }
            }

            yield break;
        }
    }
}
