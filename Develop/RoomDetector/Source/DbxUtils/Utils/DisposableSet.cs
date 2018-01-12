using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DbxUtils.Utils
{
    /// There is one thing I might be careful of, which is that if one of the methods you call throws an exception, 
    /// the new Polylines would not be disposed, and that could very likely crash AutoCAD.
    /// Here's one way to deal with it, that ensures that all new Polyline objects are 
    /// disposed even if the code fails for some reason:
    /// 
    ///using( var plines = new DisposableSet<Polyline>() )
    ///{
    ///   IEnumerable<Polyline> offsetRight = source.GetOffsetCurves( offsetDist ).Cast<Polyline>();
    ///   plines.AddRange( offsetRight );
    ///   IEnumerable<Polyline> offsetLeft = source.GetOffsetCurves( -offsetDist ).Cast<Polyline>();
    ///   plines.AddRange( offsetLeft );
    ///}
    /// 
    public interface IDisposableCollection<T> : ICollection<T>, IDisposable where T : IDisposable
    {
        void AddRange(IEnumerable<T> items);
        IEnumerable<T> RemoveRange(IEnumerable<T> items);
    }

    public class DisposableSet<T> : HashSet<T>, IDisposableCollection<T>
       where T : IDisposable
    {
        public DisposableSet()
        {
        }

        public DisposableSet(IEnumerable<T> items)
        {
            AddRange(items);
        }

        public void Dispose()
        {
            if (base.Count > 0)
            {
                System.Exception last = null;
                var list = this.ToList();
                this.Clear();

                foreach (T item in list)
                {
                    if (item != null)
                    {
                        try
                        {
                            item.Dispose();
                        }
                        catch (System.Exception ex)
                        {
                            last = last ?? ex;
                        }
                    }
                }

                if (last != null)
                    throw last;
            }
        }

        public void AddRange(IEnumerable<T> items)
        {
            if (items == null)
                throw new ArgumentNullException("items");

            base.UnionWith(items);
        }

        public IEnumerable<T> RemoveRange(IEnumerable<T> items)
        {
            if (items == null)
                throw new ArgumentNullException("items");
            base.ExceptWith(items);
            return items;
        }
    }
}
