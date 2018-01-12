using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows.Data;

namespace LS.MapClean.Addin.ViewModel.Base
{
    public class CheckableObservableCollection<T> : ObservableCollection<CheckWrapper<T>>
    {
        private ListCollectionView _selected;

        public CheckableObservableCollection()
        {
            _selected = new ListCollectionView(this);
            _selected.Filter = delegate(object checkObject)
            {
                return ((CheckWrapper<T>)checkObject).IsChecked;
            };
        }

        public void Add(T item)
        {
            this.Add(new CheckWrapper<T>(this) { Value = item });
        }

        public void Add(T item, bool isChecked)
        {
            this.Add(new CheckWrapper<T>(this) { Value = item, IsChecked = isChecked});
        }

        public ICollectionView CheckedItems
        {
            get { return _selected; }
        }

        internal void Refresh()
        {
            _selected.Refresh();
        }

    }
}
