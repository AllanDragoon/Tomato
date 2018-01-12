using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LS.MapClean.Addin.ViewModel.Base;

namespace LS.MapClean.Addin.ViewModel.Events
{
    public class BrowserEventArgs : CoreEventArgs
    {
        public object DataObject { get; set; }
        public BrowserNodeViewModel BrowserNodeViewModel { get; set; }
    }

    public class BrowserKeyEventArgs : BrowserEventArgs
    {
        public Key Key { get; set; }
    }

    public class BrowserDragDropEventArgs : BrowserEventArgs
    {
        public BrowserNodeViewModel DragDropTarget { get; set; }
        public DragDropEffects DragDropEffects { get; set; }
    }

    public class BrowserStartDragDropEventArgs : BrowserEventArgs
    {
        public object DragDropObject { get; set; }
        public System.Windows.DependencyObject DragDropDataSource { get; set; }
    }

    public class BrowserValidateEventArgs : BrowserEventArgs
    {
        public bool StopEvent { get; set; }
    }

    public class BrowserEvents : CoreEvents
    {
        private EventHandler<BrowserEventArgs> m_onDoubleClick;
        private EventHandler<BrowserEventArgs> m_onNodeClick;
        private EventHandler<BrowserKeyEventArgs> m_onKeyDown;
        private EventHandler<BrowserValidateEventArgs> m_onBeforeDoubleClick;
        private EventHandler<BrowserValidateEventArgs> m_onBeforeStartDrag;
        private EventHandler<BrowserEventArgs> m_onStartDrag;
        private EventHandler<DragEventArgs> m_onBrowserDrop;
        private EventHandler<ScrollChangedEventArgs> m_onScrollToEnd;
        private EventHandler<BrowserEventArgs> m_onBeforeShowNodeDetail;
        private EventHandler<BrowserEventArgs> m_onBeforeShowNodePreview;
        // Open asset's attachment file.
        private EventHandler<BrowserEventArgs> m_onOpenAttachmentFile;
        //log in requested
        private EventHandler<BrowserEventArgs> m_onLoginRequested;
        // Batch assign keywords requested.
        private EventHandler<BrowserEventArgs> m_onBatchKeywordsRequested;

        private string m_uniqueIdenfifier;

        public BrowserEvents(string uniqueIdenfifier)
        {
            m_uniqueIdenfifier = uniqueIdenfifier;
        }

        public event EventHandler<BrowserEventArgs> DoubleClicked
        {
            add { m_onDoubleClick = (EventHandler<BrowserEventArgs>)this.IncrementListeners(m_onDoubleClick, value); }
            remove { m_onDoubleClick = (EventHandler<BrowserEventArgs>)this.DecrementListeners(m_onDoubleClick, value); }
        }

        public event EventHandler<BrowserEventArgs> NodeClicked
        {
            add { m_onNodeClick = (EventHandler<BrowserEventArgs>)this.IncrementListeners(m_onNodeClick, value); }
            remove { m_onNodeClick = (EventHandler<BrowserEventArgs>)this.DecrementListeners(m_onNodeClick, value); }
        }

        public event EventHandler<BrowserKeyEventArgs> OnKeyDown
        {
            add { m_onKeyDown = (EventHandler<BrowserKeyEventArgs>)this.IncrementListeners(m_onKeyDown, value); }
            remove { m_onKeyDown = (EventHandler<BrowserKeyEventArgs>)this.DecrementListeners(m_onKeyDown, value); }
        }

        public event EventHandler<BrowserValidateEventArgs> OnBeforeDoubleClick
        {
            add { m_onBeforeDoubleClick = (EventHandler<BrowserValidateEventArgs>)this.IncrementListeners(m_onBeforeDoubleClick, value); }
            remove { m_onBeforeDoubleClick = (EventHandler<BrowserValidateEventArgs>)this.DecrementListeners(m_onBeforeDoubleClick, value); }
        }

        public event EventHandler<BrowserValidateEventArgs> OnBeforeStartDrag
        {
            add { m_onBeforeStartDrag = (EventHandler<BrowserValidateEventArgs>)this.IncrementListeners(m_onBeforeStartDrag, value); }
            remove { m_onBeforeStartDrag = (EventHandler<BrowserValidateEventArgs>)this.DecrementListeners(m_onBeforeStartDrag, value); }
        }

        public event EventHandler<BrowserEventArgs> DragStarted
        {
            add { m_onStartDrag = (EventHandler<BrowserEventArgs>)this.IncrementListeners(m_onStartDrag, value); }
            remove { m_onStartDrag = (EventHandler<BrowserEventArgs>)this.DecrementListeners(m_onStartDrag, value); }
        }

        public event EventHandler<DragEventArgs> BrowserDropped
        {
            add { m_onBrowserDrop = (EventHandler<DragEventArgs>)this.IncrementListeners(m_onBrowserDrop, value); }
            remove { m_onBrowserDrop = (EventHandler<DragEventArgs>)this.DecrementListeners(m_onBrowserDrop, value); }
        }

        public event EventHandler<ScrollChangedEventArgs> ScrollToEnd
        {
            add { m_onScrollToEnd = (EventHandler<ScrollChangedEventArgs>)this.IncrementListeners(m_onScrollToEnd, value); }
            remove { m_onScrollToEnd = (EventHandler<ScrollChangedEventArgs>)this.DecrementListeners(m_onScrollToEnd, value); }
        }

        public event EventHandler<BrowserEventArgs> BeforeShowNodeDetail
        {
            add { m_onBeforeShowNodeDetail = (EventHandler<BrowserEventArgs>)this.IncrementListeners(m_onBeforeShowNodeDetail, value); }
            remove { m_onBeforeShowNodeDetail = (EventHandler<BrowserEventArgs>)this.DecrementListeners(m_onBeforeShowNodeDetail, value); }
        }

        public event EventHandler<BrowserEventArgs> BeforeShowNodePreview
        {
            add { m_onBeforeShowNodePreview = (EventHandler<BrowserEventArgs>)this.IncrementListeners(m_onBeforeShowNodePreview, value); }
            remove { m_onBeforeShowNodePreview = (EventHandler<BrowserEventArgs>)this.DecrementListeners(m_onBeforeShowNodePreview, value); }
        }

        public event EventHandler<BrowserEventArgs> OpenAttachmentFile
        {
            add { m_onOpenAttachmentFile = (EventHandler<BrowserEventArgs>)this.IncrementListeners(m_onOpenAttachmentFile, value); }
            remove { m_onOpenAttachmentFile = (EventHandler<BrowserEventArgs>)this.DecrementListeners(m_onOpenAttachmentFile, value); }
        }

        public event EventHandler<BrowserEventArgs> LogInRequested
        {
            add { m_onLoginRequested = (EventHandler<BrowserEventArgs>)this.IncrementListeners(m_onLoginRequested, value); }
            remove { m_onLoginRequested = (EventHandler<BrowserEventArgs>)this.DecrementListeners(m_onLoginRequested, value); }
        }

        public event EventHandler<BrowserEventArgs> BatchKeywordsRequested
        {
            add { m_onBatchKeywordsRequested = (EventHandler<BrowserEventArgs>)this.IncrementListeners(m_onBatchKeywordsRequested, value); }
            remove { m_onBatchKeywordsRequested = (EventHandler<BrowserEventArgs>)this.DecrementListeners(m_onBatchKeywordsRequested, value); }
        }

        internal void RaiseOnBeforeDoubleClick(BrowserValidateEventArgs args)
        {
            if (null != this.m_onBeforeDoubleClick)
                this.m_onBeforeDoubleClick(this, args);
        }

        internal void RaiseOnBeforeStartDrag(BrowserValidateEventArgs args)
        {
            if (null != this.m_onBeforeStartDrag)
                this.m_onBeforeStartDrag(this, args);
        }

        internal void RaiseOnStartDrag(BrowserEventArgs args)
        {
            if (null != this.m_onStartDrag)
                this.m_onStartDrag(this, args);
        }

        internal void RaiseOnDoubleClick(BrowserEventArgs args)
        {
            if (null != this.m_onDoubleClick)
            {
                this.m_onDoubleClick(this, args);
            }
        }

        internal void RaiseOnNodeClick(BrowserEventArgs args)
        {
            if (null != this.m_onNodeClick)
            {
                this.m_onNodeClick(this, args);
            }
        }

        internal void RaiseOnKeyDown(BrowserKeyEventArgs args)
        {
            if (null != this.m_onKeyDown)
            {
                this.m_onKeyDown(this, args);
            }
        }

        public void RaiseOnBrowserDrop(object sender, DragEventArgs e)
        {
            if (null != this.m_onBrowserDrop)
            {
                this.m_onBrowserDrop(sender, e);
            }
        }

        internal void RaiseOnScrollToEnd(object sender, ScrollChangedEventArgs e)
        {
            if (null != this.m_onScrollToEnd)
            {
                this.m_onScrollToEnd(sender, e);
            }
        }

        internal void RaiseOnBeforeShowNodePreview(object sender, BrowserEventArgs e)
        {
            if (null != this.m_onBeforeShowNodePreview)
            {
                this.m_onBeforeShowNodePreview(sender, e);
            }
        }

        internal void RaiseOnBeforeShowNodeDetail(object sender, BrowserEventArgs e)
        {
            if (null != this.m_onBeforeShowNodeDetail)
            {
                this.m_onBeforeShowNodeDetail(sender, e);
            }
        }

        internal void RaiseOnOpenAttachmentFile(object sender, BrowserEventArgs e)
        {
            if (null != this.m_onOpenAttachmentFile)
                this.m_onOpenAttachmentFile(sender, e);
        }

        public void RaiseOnLoginRequested(object sender, BrowserEventArgs e)
        {
            if (null != this.m_onLoginRequested)
                this.m_onLoginRequested(sender, e);
        }

        public void RaiseOnBatchKeywordsRequested(object sender, BrowserEventArgs e)
        {
            if (null != this.m_onBatchKeywordsRequested)
                this.m_onBatchKeywordsRequested(sender, e);
        }

        public override string UniqueIdentifier
        {
            get { return m_uniqueIdenfifier; }
        }
    }
}
