using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using GalaSoft.MvvmLight;

namespace LS.MapClean.Addin.ViewModel.Base
{
    /// <summary>
    /// TreeNode implements the basic hierarchy behavor of a node of tree
    /// </summary>
    public abstract class TreeNode : ViewModelBase, IDisposable
    {
        public TreeNode(TreeNode parent)
        {
            Parent = parent;
        }

        TreeNode m_parent;
        public TreeNode Parent
        {
            get { return m_parent; }
            set { m_parent = value; }
        }

        ObservableCollection<TreeNode> m_children = new ObservableCollection<TreeNode>();
        public ObservableCollection<TreeNode> Children
        {
            get { return m_children; }
        }

        /// <summary>
        /// UniqueIdentifier is used to identify the browser node after persistent
        /// we can lookup the TreeNode after refresh or another application session.
        /// We'd better to assign an UniqueIdentifier when creating a TreeNode.
        /// </summary>
        public string UniqueIdentifier { get; set; }

        string m_name;
        public string Name
        {
            get { return m_name; }
            set
            {
                m_name = value;
                RaisePropertyChanged("Name");
            }
        }

        public object DataObject { get; internal set; }

        public TreeNode[] NodePath
        {
            get { return GetNodePathArray(); }
        }

        private TreeNode[] GetNodePathArray()
        {
            var list = new List<TreeNode>();
            list.Add(this);
            if (Parent != null)
                list.InsertRange(0, Parent.GetNodePathArray());
            return list.ToArray();
        }

        public string[] IdPath
        {
            get { return GetNodeIdPathArray(); }
        }

        private string[] GetNodeIdPathArray()
        {
            var list = new List<string>();
            list.Add(this.UniqueIdentifier);
            if (Parent != null)
                list.InsertRange(0, Parent.GetNodeIdPathArray());
            return list.ToArray();
        }

        public KeyValuePair<string, string>[] InfoPath
        {
            get { return GetNodeInfoPathArray(); }
        }
        private KeyValuePair<string, string>[] GetNodeInfoPathArray()
        {
            var list = new List<KeyValuePair<string, string>>();
            list.Add(new KeyValuePair<string, string>(UniqueIdentifier, Name));
            if (Parent != null)
            {
                list.InsertRange(0, Parent.GetNodeInfoPathArray());
            }
            return list.ToArray();
        }

        public TreeNode SearchNodeByIdPath(string[] nodeIdPath)
        {
            string[] myPath = IdPath;
            if (nodeIdPath.Length < myPath.Length)
                return null;

            for (int i = 0; i < myPath.Length; i++)
            {
                if (!String.Equals(nodeIdPath[i], myPath[i]))
                    return null;
            }

            var result = this;
            for (int i = myPath.Length; i < nodeIdPath.Length; i++)
            {
                var subNode = result.Children.FirstOrDefault(it => String.Equals(it.UniqueIdentifier, nodeIdPath[i]));
                result = subNode;
                if (result == null)
                    break;
            }
            return result;
        }

        /// <summary>
        /// Get this node's root node.
        /// </summary>
        public TreeNode RootNode
        {
            get
            {
                if (Parent == null)
                    return this;
                else
                    return Parent.RootNode;
            }
        }

        /// <summary>
        /// Get node's level (topeset is 0)
        /// </summary>
        public int NodeLevel
        {
            get
            {
                if (Parent == null)
                    return 0;
                return Parent.NodeLevel + 1;
            }
        }

        /// <summary>
        /// Get this node's siblings.
        /// </summary>
        public IEnumerable<TreeNode> Siblings
        {
            get
            {
                if (Parent == null)
                    return new TreeNode[0];
                return Parent.Children.Except(new TreeNode[] { this });
            }
        }

        /// <summary>
        /// Get an object's corresponding tree node instance in Children.
        /// </summary>		
        public TreeNode GetDirectChildTreeNode(object tag)
        {
            return Children.FirstOrDefault(vm => vm.DataObject == tag);
        }

        /// <summary>
        /// Get an object's corresponding Tree node instances in Children.
        /// </summary>
        /// <param name="tag"></param>
        /// <returns></returns>
        public IEnumerable<TreeNode> GetDirectChildTreeNodes(object tag)
        {
            return Children.Where(vm => vm.DataObject == tag);
        }

        public TreeNode GetTreeNode(object tag)
        {
            if (tag == null) throw new ArgumentNullException(/*MSG0*/"tag");

            if (tag == this.DataObject)
            {
                return this;
            }
            else
            {
                // Use depth-first to find tag's corresponding view model, it's faster than
                // using AllContainingChildren.FirstOrDefault(b => b.DataObject.Equals(tag))
                foreach (var nodeVM in Children)
                {
                    var retVM = nodeVM.GetTreeNode(tag);
                    if (retVM != null)
                        return retVM;
                }
                return null;
            }
        }

        public IEnumerable<TreeNode> GetTreeNodes(object tag)
        {
            if (tag == null) throw new ArgumentNullException(/*MSG0*/"tag");

            var nodeVMs = new List<TreeNode>();
            if (tag == this.DataObject)
            {
                nodeVMs.Add(this);
            }
            nodeVMs.AddRange(AllContainingNodes.Where(vm => vm.DataObject != null && vm.DataObject.Equals(tag)));

            return nodeVMs;
        }

        public IEnumerable<TreeNode> AllContainingNodes
        {
            get
            {
                // add directly children
                var allChildren = new List<TreeNode>(Children.Cast<TreeNode>());

                // iterate the sub nodes
                foreach (var child in Children)
                    _GetSubNodeChildren(child, allChildren);

                return allChildren;
            }
        }

        static void _GetSubNodeChildren(TreeNode subNode, List<TreeNode> result)
        {
            result.AddRange(subNode.Children.Cast<TreeNode>());

            foreach (var child in subNode.Children)
                _GetSubNodeChildren(child, result);
        }

        public IEnumerable<TreeNode> GetAllLeafNodes()
        {
            var result = new List<TreeNode>();
            if (!Children.Any())
            {
                result.Add(this);
            }
            else
            {
                foreach (TreeNode child in Children)
                {
                    var leafs = child.GetAllLeafNodes();
                    result.AddRange(leafs);
                }
            }
            return result;
        }

        #region IDisposable pattern
        // http://msdn.microsoft.com/en-us/library/system.idisposable.aspx
        // Track whether Dispose has been called. 
        private bool m_disposed = false;

        /// <summary>
        /// Implement IDisposable.
        /// Do not make this method virtual - a derived class should not be able to override this method.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);

            // This object will be cleaned up by the Dispose method. Therefore, you should call
            // GC.SuppressFinalize to take this object off the finalization queue and prevent 
            // finalization code for this object from executing a second time.
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Dispose all nodes on the tree.
        /// </summary>
        public void DisposeRecursively()
        {
            if (Children != null)
            {
                foreach (TreeNode child in Children)
                {
                    child.DisposeRecursively();
                }
            }
            Dispose();
        }

        /// <summary>
        /// Use C# destructor syntax for finalization code, this destructor will run only if
        /// the Dispose mehtod does not get called. It gives your base class the opportunity to
        /// finalize. 
        /// Do not provide destructors in types derived from this class.
        /// </summary>
        ~TreeNode()
        {
            Dispose(false);
        }

        /// <summary>
        /// Dispose(bool disposing) executes in two distinct scenarios: If disposing equals true, 
        /// the method has been called directly or indirectly by a user's code, managed and unmanaged
        /// resources can be disposed; If disposing equals false, the method has been called by the 
        /// runtime from inside the finalizer and you should not reference other objects, only unmanaged
        /// resources can be disposed.
        /// </summary>
        /// <param name="disposing"></param>
        protected virtual void Dispose(bool disposing)
        {
            if (!this.m_disposed)
            {
                // Call itself's Dispose(bool disposing)
                if (disposing)
                {
                    // Dispose managed resources.
                }
                this.m_disposed = true;
            }
        }

        #endregion
    }
}
