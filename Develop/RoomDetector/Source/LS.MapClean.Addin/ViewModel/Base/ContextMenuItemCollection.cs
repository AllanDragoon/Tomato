using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows.Input;

namespace LS.MapClean.Addin.ViewModel.Base
{
    public sealed class ContextMenuItemCollection : ObservableCollection<ContextMenuViewModel>
    {
        public ContextMenuItemCollection()
        {
        }

        /// <summary>
        /// Append a separator at the end of the menu
        /// </summary>
        /// <param name="beforeViewModel"></param>
        public void AppendSeparator()
        {
            //use a null object to represent a Separator
            //Reference from http://www.japf.fr/2008/12/how-insert-separator-in-a-databound-combobox/
            Insert(this.Count, null);
        }

        public void Add(string displayName, ICommand command, string toolTip = "", bool toolTipShowOnDisable = false)
        {
            Add(new ContextMenuViewModel(displayName, command, false, false, toolTipShowOnDisable, toolTip));
        }

        public void AddRange(IEnumerable<ContextMenuViewModel> source)
        {
            foreach (var item in source)
                Add(item);
        }

        public void Insert(int aIndex, string displayName, ICommand command, string toolTip = "")
        {
            Insert(aIndex, new ContextMenuViewModel(displayName, command, false, false, false, toolTip));
        }
    }
}
