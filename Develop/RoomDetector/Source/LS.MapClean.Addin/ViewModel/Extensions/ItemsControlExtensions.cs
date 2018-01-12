using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace LS.MapClean.Addin.ViewModel.Extensions
{
    public static class ItemsControlExtensions
    {
        public static bool CanSelectMultipleItems(this ItemsControl itemsControl)
        {
            MultiSelector multiSelector = itemsControl as MultiSelector;
            if (multiSelector != null)
            {
                // The CanSelectMultipleItems property is protected. Use reflection to
                // get it's value anyway.
                return (bool)multiSelector.GetType()
                    .GetProperty("CanSelectMultipleItems", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(itemsControl, null);
            }

            ListBox listBox = itemsControl as ListBox;
            if (listBox != null)
            {
                return listBox.SelectionMode != SelectionMode.Single;
            }
            return false;
        }

        public static UIElement GetItemContainer(this ItemsControl itemsControl, DependencyObject child)
        {
            if (itemsControl == null)
                throw new ArgumentNullException(/*MSG0*/"itemsControl");

            if (child == null)
                throw new ArgumentNullException(/*MSG0*/"child");

            Type itemType = GetItemContainerType(itemsControl);
            if (itemType != null)
            {
                return (UIElement)child.GetVisualAncestor(itemType);
            }

            return null;
        }

        public static UIElement GetItemContainerAt(this ItemsControl itemsControl, Point position)
        {
            if (itemsControl == null)
                throw new ArgumentNullException(/*MSG0*/"itemsControl");

            IInputElement inputElement = itemsControl.InputHitTest(position);
            UIElement uiElement = inputElement as UIElement;

            if (uiElement != null)
            {
                return GetItemContainer(itemsControl, uiElement);
            }

            return null;
        }

        public static Type GetItemContainerType(this ItemsControl itemsControl)
        {
            if (itemsControl == null)
                throw new ArgumentNullException(/*MSG0*/"itemsControl");

            // There is no safe way to get the item container type for an ItemsControl. The
            // best we can do is to look for the control's ItemsPresenter, get it's child 
            // panel and the first child of that *should* be an item container.
            //
            // If the control currently has no items, we're out of luck.
            if (itemsControl.Items.Count > 0)
            {
                IEnumerable<ItemsPresenter> itemsPresenters = itemsControl.GetVisualDescendents<ItemsPresenter>();

                foreach (ItemsPresenter itemsPresenter in itemsPresenters)
                {
                    DependencyObject panel = VisualTreeHelper.GetChild(itemsPresenter, 0);
                    DependencyObject itemContainer = VisualTreeHelper.GetChild(panel, 0);

                    // Ensure that this actually *is* an item container by checking it with
                    // ItemContainerGenerator.
                    if (itemContainer != null &&
                        itemsControl.ItemContainerGenerator.IndexFromContainer(itemContainer) != -1)
                    {
                        return itemContainer.GetType();
                    }
                }
            }

            return null;
        }

        public static Orientation GetItemsPanelOrientation(this DependencyObject dependencyObject)
        {
            ItemsPresenter itemsPresenter = dependencyObject.GetVisualDescendent<ItemsPresenter>();
            DependencyObject itemsPanel = VisualTreeHelper.GetChild(itemsPresenter, 0);
            PropertyInfo orientationProperty = itemsPanel.GetType().GetProperty("Orientation", typeof(Orientation));

            if (orientationProperty != null)
            {
                return (Orientation)orientationProperty.GetValue(itemsPanel, null);
            }
            else
            {
                // Make a guess!
                return Orientation.Vertical;
            }
        }

        public static IEnumerable GetSelectedItems(this ItemsControl itemsControl)
        {
            MultiSelector multiSelector = itemsControl as MultiSelector;
            if (multiSelector != null)
            {
                return multiSelector.SelectedItems;
            }

            ListBox listBox = itemsControl as ListBox;
            if (listBox != null)
            {
                if (listBox.SelectionMode == SelectionMode.Single)
                {
                    return Enumerable.Repeat(listBox.SelectedItem, 1);
                }
                else
                {
                    return listBox.SelectedItems;
                }
            }

            TreeView treeView = itemsControl as TreeView;
            if (treeView != null)
            {
                return Enumerable.Repeat(treeView.SelectedItem, 1);
            }

            Selector selector = itemsControl as Selector;
            if (selector != null)
            {
                return Enumerable.Repeat(selector.SelectedItem, 1);
            }

            return Enumerable.Empty<object>();
        }
    }
}
