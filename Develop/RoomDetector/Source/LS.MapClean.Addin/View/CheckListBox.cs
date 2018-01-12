using System;
using System.Collections;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Collections.ObjectModel;

namespace LS.MapClean.Addin.View
{
    [StyleTypedProperty(Property = "ItemContainerStyle", StyleTargetType = typeof(CheckableListBoxItem))]
    public class CheckListBox : ListBox
    {
        ObservableCollection<object> m_checkedItems = null;
        Type m_itemContainerType = typeof(FrameworkElement);//项容器的类型
        public CheckListBox()
        {
            m_checkedItems = new ObservableCollection<object>();
            // 从CheckListBox类附加的特性中获取项目容器的类型
            var attr = this.GetType().GetCustomAttributes(typeof(StyleTypedPropertyAttribute), false);
            if (attr != null && attr.Length != 0)
            {
                var sty = attr[0] as StyleTypedPropertyAttribute;
                if (sty != null)
                {
                    this.m_itemContainerType = sty.StyleTargetType;
                }
            }
        }

        public static DependencyProperty CheckedItemsProperty = DependencyProperty.Register("CheckedItems", typeof(IList), typeof(CheckListBox), new PropertyMetadata(null));

        public IList CheckedItems
        {
            get { return (IList)GetValue(CheckedItemsProperty); }
        }

        /// <summary>
        /// 创建项目容器
        /// </summary>
        protected override DependencyObject GetContainerForItemOverride()
        {
            return Activator.CreateInstance(this.m_itemContainerType) as DependencyObject;
        }

        /// <summary>
        /// 当从项目创建项容时，
        /// 为项目容器注册事件处理。
        /// </summary>
        protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
        {
            var ckItem = element as CheckableListBoxItem;
            ckItem.Checked += clbitem_Checked;
            ckItem.UnChecked += clbitem_UnChecked;
            base.PrepareContainerForItemOverride(element, item);
        }

        /// <summary>
        /// 当项容被清空时，
        /// 解除事件处理程序。
        /// </summary>
        protected override void ClearContainerForItemOverride(DependencyObject element, object item)
        {
            var ckItem = element as CheckableListBoxItem;
            ckItem.Checked -= clbitem_Checked;
            ckItem.UnChecked -= clbitem_UnChecked;
            base.ClearContainerForItemOverride(element, item);
        }


        void clbitem_UnChecked(object sender, RoutedEventArgs e)
        {
            var citem = (CheckableListBoxItem)e.Source;
            object value = citem.Content;
            m_checkedItems.Remove(value);
            SetValue(CheckedItemsProperty, m_checkedItems);
        }

        void clbitem_Checked(object sender, RoutedEventArgs e)
        {
            var citem = (CheckableListBoxItem)(e.Source);
            object value = citem.Content;
            if (m_checkedItems.SingleOrDefault(o => object.ReferenceEquals(o, value)) == null)
            {
                m_checkedItems.Add(value);
                SetValue(CheckedItemsProperty, m_checkedItems);
            }
        }
    }


    public class CheckableListBoxItem : ListBoxItem
    {
        static CheckableListBoxItem()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(CheckableListBoxItem),
                new FrameworkPropertyMetadata(typeof(CheckableListBoxItem)));
        }


        #region 属性
        public static readonly DependencyProperty IsCheckedProperty =
            DependencyProperty.Register("IsChecked", typeof(bool), typeof(CheckableListBoxItem), new PropertyMetadata(new PropertyChangedCallback(IsCheckedPropertyChanged)));

        private static void IsCheckedPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var lt = d as CheckableListBoxItem;
            if (lt !=null)
            {
                if (e.NewValue != e.OldValue)
                {
                    var b = (bool)e.NewValue;
                    if (b)
                    {
                        lt.RaiseCheckedEvent();
                    }
                    else
                    {
                        lt.RaiseUnCheckedEvent();
                    }
                }
            }
        }

        /// <summary>
        /// 获取或设置控件是否被Check
        /// </summary>
        public bool IsChecked
        {
            get { return (bool)GetValue(IsCheckedProperty); }
            set { SetValue(IsCheckedProperty, value); }
        }
        #endregion

        #region 事件
        public static readonly RoutedEvent CheckedEvent =
            EventManager.RegisterRoutedEvent("Checked", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(CheckableListBoxItem));

        /// <summary>
        /// 当控件被Check后发生的事件
        /// </summary>
        public event RoutedEventHandler Checked
        {
            add
            {
                AddHandler(CheckedEvent, value);
            }
            remove
            {
                RemoveHandler(CheckedEvent, value);
            }
        }

        void RaiseCheckedEvent()
        {
            var arg = new RoutedEventArgs(CheckableListBoxItem.CheckedEvent);
            RaiseEvent(arg);
        }

        public static readonly RoutedEvent UnCheckedEvent = EventManager.RegisterRoutedEvent("UnChecked", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(CheckableListBoxItem));

        /// <summary>
        /// 当控件未被Check后发生
        /// </summary>
        public event RoutedEventHandler UnChecked
        {
            add { AddHandler(UnCheckedEvent, value); }
            remove { RemoveHandler(UnCheckedEvent, value); }
        }
        
        void RaiseUnCheckedEvent()
        {
            RaiseEvent(new RoutedEventArgs(UnCheckedEvent));
        }
        #endregion
    }

}

