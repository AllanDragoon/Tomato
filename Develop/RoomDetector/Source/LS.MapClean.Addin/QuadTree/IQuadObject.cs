using System;
using System.Windows;

namespace LS.MapClean.Addin.QuadTree
{
    public interface IQuadObject
    {
        Rect Bounds { get; }
        event EventHandler BoundsChanged;
    }
}