using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using LS.MapClean.Addin.ViewModel.Base;
using LS.MapClean.Addin.ViewModel.Extensions;

namespace LS.MapClean.Addin.ViewModel.Events
{
    public class DragInfo
    {
        public object Data { get; set; }
        public Point DragStartPosition { get; private set; }
        public DragDropEffects Effects { get; set; }
        public MouseButton MouseButton { get; private set; }
        public IEnumerable SourceCollection { get; private set; }
        public object SourceItem { get; private set; }
        public IEnumerable SourceItems { get; private set; }
        public UIElement VisualSource { get; private set; }
        public UIElement VisualSourceItem { get; private set; }

        public DragInfo(object sender, MouseButtonEventArgs e)
        {
            //use the relative mouse position for browser
            DragStartPosition = Mouse.GetPosition(sender as IInputElement);
            Effects = DragDropEffects.None;
            MouseButton = e.ChangedButton;
            VisualSource = sender as UIElement;
            ItemsControl itemsControl = sender as ItemsControl;

            if (itemsControl != null)
            {
                UIElement item = null;
                // Don't remove try/catch, sometimes exception is thrown here.
                // For example, when clicking "Loading more..." item, the item will be removed immediately,
                // so when code goes here, the item has gone and exception is thrown.
                try
                {
                    item = itemsControl.GetItemContainer((UIElement)e.OriginalSource);
                }
                catch
                {
                }

                if (item != null)
                {
                    ItemsControl itemParent = ItemsControl.ItemsControlFromItemContainer(item);

                    if (itemParent != null)
                    {
                        SourceCollection = itemParent.ItemsSource ?? itemParent.Items;
                        SourceItem = itemParent.ItemContainerGenerator.ItemFromContainer(item);
                    }
                    SourceItems = itemsControl.GetSelectedItems();

                    // Some controls (TreeView) haven't updated their
                    // SelectedItem by this point. Check to see if there 1 or less item in 
                    // the SourceItems collection, and if so, override the SelectedItems
                    // with the clicked item.
                    if (SourceItems.Cast<object>().Count() <= 1)
                    {
                        SourceItems = Enumerable.Repeat(SourceItem, 1);
                    }

                    VisualSourceItem = item;
                }
                else
                {
                    SourceCollection = itemsControl.ItemsSource ?? itemsControl.Items;
                }
            }

            if (SourceItems == null)
            {
                SourceItems = Enumerable.Empty<object>();
            }
        }
    }

    /// <summary>
    /// BrowserEventsDispatcher is a center which dispatch the ui events to BrowserEvents,
    /// BrowserEvents is usually used in view model side and business workflow.
    /// </summary>
    public static class BrowserEventsDispatcher
    {
        public static BrowserEvents BrowserEventsHandler = new BrowserEvents(Guid.NewGuid().ToString());

        #region Attached Properties
        public static readonly DependencyProperty HookBrowserEventsProperty =
            DependencyProperty.RegisterAttached(/*MSG0*/"HookBrowserEvents", typeof(bool), typeof(BrowserEventsDispatcher),
            new UIPropertyMetadata(false, HookBrowserEventsChanged));

        /// <summary>
        /// IsDragSource attached to the UI element
        /// </summary>
        public static readonly DependencyProperty IsDragSourceProperty =
            DependencyProperty.RegisterAttached(/*MSG0*/"IsDragSource", typeof(bool), typeof(BrowserEventsDispatcher));

        /// <summary>
        /// IsDropTarget attached to the UI element
        /// </summary>
        public static readonly DependencyProperty IsDropTargetProperty =
            DependencyProperty.RegisterAttached(/*MSG0*/"IsDropTarget", typeof(bool), typeof(BrowserEventsDispatcher),
            new UIPropertyMetadata(false, IsDropTargetChanged));

        public static readonly DependencyProperty HookScrollToEndProperty =
            DependencyProperty.RegisterAttached(/*MSG0*/"HookScrollToEnd", typeof(bool), typeof(BrowserEventsDispatcher),
            new UIPropertyMetadata(false, HookScrollToEndChanged));

        public static readonly DependencyProperty ScrollByUserProperty =
            DependencyProperty.RegisterAttached(/*MSG0*/"ScrollByUser", typeof(bool), typeof(BrowserEventsDispatcher));

        public static bool GetHookBrowserEvents(UIElement target)
        {
            return (bool)target.GetValue(HookBrowserEventsProperty);
        }

        public static void SetHookBrowserEvents(UIElement target, bool value)
        {
            target.SetValue(HookBrowserEventsProperty, value);
        }

        public static bool GetIsDragSource(UIElement target)
        {
            return (bool)target.GetValue(IsDragSourceProperty);
        }

        public static void SetIsDragSource(UIElement target, bool value)
        {
            target.SetValue(IsDragSourceProperty, value);
        }

        public static bool GetIsDropTarget(UIElement target)
        {
            return (bool)target.GetValue(IsDropTargetProperty);
        }

        public static void SetIsDropTarget(UIElement target, bool value)
        {
            target.SetValue(IsDropTargetProperty, value);
        }

        public static bool GetHookScrollToEnd(UIElement target)
        {
            return (bool)target.GetValue(HookScrollToEndProperty);
        }

        public static void SetHookScrollToEnd(UIElement target, bool value)
        {
            target.SetValue(HookScrollToEndProperty, value);
        }

        public static bool GetScrollByUser(UIElement target)
        {
            return (bool)target.GetValue(ScrollByUserProperty);
        }

        public static void SetScrollByUser(UIElement target, bool value)
        {
            target.SetValue(ScrollByUserProperty, value);
        }

        private static void HookBrowserEventsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            RegisterUIEvents(d, e);
        }

        private static void HookBrowserDetailEventsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            RegisterUIEvents(d, e);
        }

        private static void RegisterUIEvents(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            UIElement uiElement = (UIElement)d;
            if (e.NewValue != null)
            {
                uiElement.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
                uiElement.PreviewMouseLeftButtonUp += OnPreviewMouseLeftButtonUp;
                uiElement.PreviewMouseMove += OnPreviewMouseMove;
                uiElement.Drop += OnDrop;
                uiElement.KeyDown += OnKeyDown;
            }
            else
            {
                uiElement.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
                uiElement.PreviewMouseLeftButtonUp -= OnPreviewMouseLeftButtonUp;
                uiElement.PreviewMouseMove -= OnPreviewMouseMove;
                uiElement.Drop -= OnDrop;
                uiElement.KeyDown -= OnKeyDown;
            }
        }

        private static void IsDropTargetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            UIElement uiElement = (UIElement)d;
            if ((bool)e.NewValue == true)
            {
                // Set the AllowDrop property to True on the elements you want to allow dropping.
                uiElement.AllowDrop = true;

                // Handle more event later...
            }
            else
            {
                uiElement.AllowDrop = false;

                // Handle more event later...
            }
        }

        private static void HookScrollToEndChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var itemsControl = d as ItemsControl;
            if (itemsControl == null)
                return;

            RoutedEventHandler loadedHandler = null;
            loadedHandler = new RoutedEventHandler((o, s) =>
            {
                itemsControl.Loaded -= loadedHandler;
                var scrollViewer = VisualUtils.GetFirstChildOfType<ScrollViewer>(d);
                if (scrollViewer == null)
                    return;
                if (e.NewValue != null)
                {
                    // Also set scroll viewer's HookScrollToEnd true, it will be used in mouse move event.
                    SetHookScrollToEnd(scrollViewer, true);
                    scrollViewer.ScrollChanged += OnScrollChanged;
                    scrollViewer.PreviewMouseWheel += OnScrollPreviewMouseWheel;
                    scrollViewer.PreviewKeyDown += OnScrollPreviewKeyDown;
                }
                else
                {
                    SetHookScrollToEnd(scrollViewer, false);
                    scrollViewer.ScrollChanged -= OnScrollChanged;
                    scrollViewer.PreviewMouseWheel -= OnScrollPreviewMouseWheel;
                    scrollViewer.PreviewKeyDown -= OnScrollPreviewKeyDown;
                }
            });
            itemsControl.Loaded += loadedHandler;
        }

        #endregion

        #region Event Handler
        private static void OnPreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (BrowserEventsHandler == null)
                return;

            // Handle drag&drop, click and double click
            if (s_isDragging)
                return;

            // Ignore the click if the user has clicked on a scrollbar.
            if (HitTestScrollBar(sender, e))
            {
                s_dragInfo = null;
                return;
            }

            // If the mouse doesn't click on any item.
            // Return if the mouse is not click on the element
            var dataContext = HitTestValidItem(sender, e);
            if (dataContext == null)
                return;

            // Handle double click
            if (e.ClickCount == 2)
            {
                // Make sure the first clicked item is equal to the second clicked item.
                BrowserNodeViewModel viewModel = GetSelectedBrowserNodeViewModel(sender);
                if (viewModel == null || dataContext != viewModel)
                    return;

                // collect event arguments
                BrowserValidateEventArgs args = new BrowserValidateEventArgs() { Sender = sender };
                args.BrowserNodeViewModel = viewModel;
                args.DataObject = viewModel.DataObject;
                args.StopEvent = false;

                // If hook to browser events, raise double click event.
                bool hookBrowserEvents = GetHookBrowserEvents((UIElement)sender);
                if (hookBrowserEvents && BrowserEventsHandler != null)
                {
                    // Validate the drag operation.
                    BrowserEventsHandler.RaiseOnBeforeDoubleClick(args);
                    if (!args.StopEvent)
                    {
                        // Raise event
                        BrowserEventsHandler.RaiseOnDoubleClick(args);
                    }
                }

            }
            else
            {
                // Create a drag info which will be used in next events.
                s_dragInfo = new DragInfo(sender, e);
            }
        }

        private static void OnPreviewMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (BrowserEventsHandler == null)
                return;

            if (s_isDragging)
                return;
            if (s_dragInfo != null)
                s_dragInfo = null;

            var dataContext = HitTestValidItem(sender, e);
            if (dataContext == null)
                return;
            if (e.ClickCount == 1)
            {
                BrowserNodeViewModel viewModel = GetSelectedBrowserNodeViewModel(sender);
                if (viewModel == null || viewModel != dataContext)
                    return;

                // collect event arguments
                BrowserEventArgs args = new BrowserEventArgs() { Sender = sender };
                args.BrowserNodeViewModel = viewModel;
                args.DataObject = viewModel.DataObject;

                // Raise event if hook browser events.
                bool hookBrowserEvents = GetHookBrowserEvents((UIElement)sender);
                if (hookBrowserEvents && BrowserEventsHandler != null)
                    BrowserEventsHandler.RaiseOnNodeClick(args);

            }

        }

        private static void OnPreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            // If browserEvents of sender is null, needn't handle it more.
            if (BrowserEventsHandler == null)
                return;

            // If this dispatcher is being in dragging, just return.
            if (s_isDragging)
                return;

            // If this control is a drag source and left button is pressed, then start drag.
            bool isDragSource = GetIsDragSource(sender as UIElement);
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                if (s_dragInfo != null && isDragSource)
                {
                    Point dragStart = s_dragInfo.DragStartPosition;
                    Point position = Mouse.GetPosition(sender as IInputElement);//e.GetPosition(null);

                    // we need to ensure the drag distance is larger than MinimumHorizontalDragDistance
                    // MinimumVerticalDragDistance
                    // http://msdn.microsoft.com/en-us/library/system.windows.systemparameters.minimumhorizontaldragdistance.aspx
                    // Gets the width of a rectangle centered on a drag point to allow for 
                    // limited movement of the mouse pointer before a drag operation begins. 
                    if (Math.Abs(position.X - dragStart.X) > SystemParameters.MinimumHorizontalDragDistance ||
                        Math.Abs(position.Y - dragStart.Y) > SystemParameters.MinimumVerticalDragDistance)
                    {
                        // Inventor has weird mouse move events - when a message box pops up in a mouse event handler
                        // function, the next mouse event could still be sent out. So we set s_beDragging true to indicate 
                        // a dragging is being executed, and the next mouse move won't be handled.
                        using (DraggingFlagSwitcher switcher = new DraggingFlagSwitcher())
                        {
                            bool hookBrowserEvents = GetHookBrowserEvents((UIElement)sender);
                            if (hookBrowserEvents && BrowserEventsHandler != null)
                            {
                                StartDrag(s_dragInfo, BrowserEventsHandler);

                                // start drag
                                if (s_dragInfo.Effects != DragDropEffects.None && s_dragInfo.Data != null)
                                {
                                    // Start windows drag and drop
                                    DataObject data = new DataObject(s_Format.Name, s_dragInfo.Data);
                                    System.Windows.DragDrop.DoDragDrop(s_dragInfo.VisualSource, data, s_dragInfo.Effects);
                                    s_dragInfo = null;
                                }
                            }
                        }
                    }
                }
                else
                {
                    // http://social.msdn.microsoft.com/Forums/vstudio/en-US/9a73b1b0-ec76-4b2c-8da6-91c71e3c406f/wpf-mouse-click-event-on-scrollbar-issue?forum=wpf
                    object original = e.OriginalSource;
                    var scrollbar = VisualUtils.FindVisualParent<ScrollBar>(original as DependencyObject);
                    if (scrollbar != null)
                    {
                        var scrollViewer = VisualUtils.FindVisualParent<ScrollViewer>(original as DependencyObject);
                        if (scrollViewer != null && GetHookScrollToEnd(scrollViewer))
                        {
                            SetScrollByUser(scrollViewer, true);
                        }
                    }
                }
            }
        }

        private static void OnDrop(object sender, DragEventArgs e)
        {
            if (BrowserEventsHandler == null)
                return;

            bool isDropTarget = (bool)((UIElement)sender).GetValue(BrowserEventsDispatcher.IsDropTargetProperty);
            if (isDropTarget)
            {
                bool hookBrowserEvents = GetHookBrowserEvents((UIElement)sender);
                if (hookBrowserEvents && BrowserEventsHandler != null)
                {
                    BrowserEventsHandler.RaiseOnBrowserDrop(sender, e);
                }

            }
        }

        private static void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (BrowserEventsHandler == null)
                return;

            ItemsControl itemsControl = sender as ItemsControl;

            // get selected objects
            IEnumerable SourceItems = itemsControl.GetSelectedItems();

            // click only apply to one object
            object dataObject = SourceItems.Cast<object>().FirstOrDefault();
            if (dataObject == null)
                return;

            // dynamic cast to view model
            BrowserNodeViewModel viewModel = dataObject as BrowserNodeViewModel;

            // collect event arguments
            BrowserKeyEventArgs args = new BrowserKeyEventArgs() { Sender = sender };
            args.BrowserNodeViewModel = viewModel;
            args.DataObject = viewModel == null ? null : viewModel.DataObject;
            args.Key = e.Key;

            // Raise event
            bool hookBrowserEvents = GetHookBrowserEvents((UIElement)sender);
            if (hookBrowserEvents && BrowserEventsHandler != null)
            {
                BrowserEventsHandler.RaiseOnKeyDown(args);
            }

        }

        private static void OnScrollPreviewKeyDown(object sender, KeyEventArgs e)
        {
            var scrollViewer = sender as ScrollViewer;
            if (scrollViewer == null)
                return;

            SetScrollByUser(scrollViewer, true);
        }

        private static void OnScrollPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var scrollViewer = sender as ScrollViewer;
            if (scrollViewer == null)
                return;

            // Avoid BrowserEventsHandler.RaiseOnScrollToEnd(sender, e) to be called twice in OnScrollChanged
            // Don't know why OnScrollPreviewKeyDown is called twice when user scroll the wheel.
            if (scrollViewer.VerticalOffset != scrollViewer.ScrollableHeight)
                SetScrollByUser(scrollViewer, true);
        }

        private static void OnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            var scrollViewer = (ScrollViewer)sender;
            // If scroll is not changed from user, just return.
            if (!GetScrollByUser(scrollViewer))
                return;

            // Set it back to false, and raise browser event.
            SetScrollByUser(scrollViewer, false);
            if (scrollViewer.VerticalOffset == scrollViewer.ScrollableHeight)
            {
                if (BrowserEventsHandler != null)
                    BrowserEventsHandler.RaiseOnScrollToEnd(sender, e);
                e.Handled = true;
            }
        }
        #endregion

        #region Drag & Drop
        static DragInfo s_dragInfo;
        static DataFormat s_Format = DataFormats.GetDataFormat(DataFormats.FileDrop);

        /// <summary>
        /// Whether this dispatcher is being in dragging.
        /// </summary>
        static bool s_isDragging = false;
        /// <summary>
        /// A switcher class to handle s_isDragging flag.
        /// </summary>
        class DraggingFlagSwitcher : IDisposable
        {
            public DraggingFlagSwitcher()
            {
                BrowserEventsDispatcher.s_isDragging = true;
            }
            public void Dispose()
            {
                BrowserEventsDispatcher.s_isDragging = false;
            }
        }

        private static void StartDrag(DragInfo dragInfo, BrowserEvents browserEvents)
        {
            var sourceItems = dragInfo.SourceItems.Cast<object>();

            // only support single selection
            int itemCount = sourceItems.Count();
            if (itemCount != 1)
                return;

            dragInfo.Data = sourceItems.Single();

            // browser node 
            BrowserNodeViewModel viewModel = dragInfo.Data as BrowserNodeViewModel;
            if (viewModel == null) return;

            // If view model doesn't support dragging, then bail
            if (!viewModel.SupportsDrag)
            {
                dragInfo.Effects = DragDropEffects.None;
                return;
            }

            // collect event arguments
            BrowserValidateEventArgs args = new BrowserValidateEventArgs
            {
                BrowserNodeViewModel = viewModel,
                DataObject = dragInfo,
            };

            // Validate the drag operation.
            if (browserEvents != null)
            {
                browserEvents.RaiseOnBeforeStartDrag(args);
                if (!args.StopEvent)
                {
                    // Raise start drag event
                    browserEvents.RaiseOnStartDrag(args);
                }
            }

            dragInfo.Effects = (dragInfo.Data != null) ? DragDropEffects.Copy : DragDropEffects.None;
        }
        #endregion

        #region Utils
        static bool HitTestScrollBar(object sender, MouseButtonEventArgs e)
        {
            HitTestResult hit = VisualTreeHelper.HitTest((Visual)sender, e.GetPosition((IInputElement)sender));
            if (hit == null)
                return false;
            return hit.VisualHit.GetVisualAncestor<System.Windows.Controls.Primitives.ScrollBar>() != null;
        }

        static Object HitTestValidItem(object sender, MouseButtonEventArgs e)
        {
            // return true if hit TreeViewItem or ListBoxItem
            HitTestResult hit = VisualTreeHelper.HitTest((Visual)sender, e.GetPosition((IInputElement)sender));
            if (hit == null)
                return null;

            var treeviewItem = hit.VisualHit.GetVisualAncestor<System.Windows.Controls.TreeViewItem>();
            if (treeviewItem != null)
                return treeviewItem.DataContext;

            var listboxItem = hit.VisualHit.GetVisualAncestor<System.Windows.Controls.ListBoxItem>();
            if (listboxItem != null)
                return listboxItem.DataContext;

            return null;
        }

        static BrowserNodeViewModel GetSelectedBrowserNodeViewModel(object sender)
        {
            ItemsControl itemsControl = sender as ItemsControl;

            // get selected objects
            IEnumerable SourceItems = itemsControl.GetSelectedItems();

            // click only apply to one object
            object dataObject = SourceItems.Cast<object>().FirstOrDefault();
            if (dataObject != null)
            {
                BrowserNodeViewModel viewModel = dataObject as BrowserNodeViewModel;
                return viewModel;
            }
            else
                return null;
        }
        #endregion
    }
}
