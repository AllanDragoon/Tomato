using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace LS.MapClean.Addin.ViewModel.Base
{
    public class BrowserNodeViewModel : TreeNode
    {
        string m_secondaryName;

        bool m_isFontweightBold;
        bool m_isExpanded;
        bool m_isSelected;
        bool m_isActive;
        bool m_isVisible = true;
        bool m_isContextMenuVisible = true;

        protected BrowserNodeViewModel(BrowserNodeViewModel parent, object dataObject, string name)
            : base(parent)
        {
            Name = name;
            DataObject = dataObject;
        }

        public BrowserNodeViewModel(BrowserNodeViewModel parent, string name)
            : this(parent, null /*dataObject*/, name)
        {
        }

        public bool IsVisible
        {
            get { return m_isVisible; }
            set
            {
                if (m_isVisible != value)
                {
                    m_isVisible = value;
                    RaisePropertyChanged(/*MSG0*/"IsVisible");
                }
            }
        }

        /// <summary>
        /// is bold
        /// </summary>
        public bool IsFontWeightBold
        {
            get { return m_isFontweightBold; }
            set
            {
                m_isFontweightBold = value;
                RaisePropertyChanged(/*MSG0*/"IsFontWeightBold");
            }
        }


        public bool IsContextMenuVisible
        {
            get { return m_isContextMenuVisible; }
            set
            {
                m_isContextMenuVisible = value;
                RaisePropertyChanged("IsContextMenuVisible");
            }
        }

        public ObservableCollection<ContextMenuViewModel> ContextMenus
        {
            get { return BuildContextMenus(); }
        }

        ContextMenuItemCollection BuildContextMenus()
        {
            var items = new ContextMenuItemCollection();
            AddContextMenuItems(items);

            // Hide the context menu if no context menu item is added
            if (items.Count == 0)
                this.IsContextMenuVisible = false;

            return items;
        }

        protected virtual void AddContextMenuItems(ContextMenuItemCollection items)
        {
            // Base class implementation is no-op. Subclasses override to add items.
        }

        public void UpdateNodeContextMenu()
        {
            RaisePropertyChanged("ContextMenus");
        }

        public string SecondaryName
        {
            get { return m_secondaryName; }
            set
            {
                m_secondaryName = value;
                RaisePropertyChanged(/*MSG0*/"SecondaryName");
            }
        }

        string m_tooltip;
        public virtual string ToolTip
        {
            get { return m_tooltip; }
            set
            {
                m_tooltip = value;
                RaisePropertyChanged("ToolTip");
            }
        }

        public bool IsExpanded
        {
            get { return m_isExpanded; }
            set
            {
                m_isExpanded = value;
                if (m_isExpanded && Parent != null)
                    ((BrowserNodeViewModel)Parent).IsExpanded = true;
                // Call virtual method OnNodeIsExpanded.
                OnNodeIsExpanded();
                RaisePropertyChanged(/*MSG0*/"IsExpanded");
            }
        }

        /// <summary>
        /// Invoked when node's IsExpanded become true.
        /// </summary>
        protected virtual void OnNodeIsExpanded()
        {
        }

        public virtual bool IsSelected
        {
            get { return m_isSelected; }
            set
            {
                m_isSelected = value;
                RaisePropertyChanged(/*MSG0*/"IsSelected");
            }
        }

        /// <summary>
        /// If the node is active, like by double clicking
        /// </summary>
        public bool IsActive
        {
            get { return m_isActive; }
            set
            {
                m_isActive = value;
                RaisePropertyChanged(/*MSG0*/"IsActive");
            }
        }

        public void Refresh()
        {
            Children.Clear();
            LoadChildren();
        }

        public void Clear()
        {
            if (Children != null)
                Children.Clear();
        }

        public override string ToString()
        {
            return Name;
        }

        protected virtual void LoadChildren()
        {
        }

        /// <summary>
        /// Can be set to true to support dragging via DragDropDispatcher
        /// </summary>
        public bool SupportsDrag { get; set; }
    }
}
