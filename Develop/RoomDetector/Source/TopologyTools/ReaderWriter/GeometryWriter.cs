using GeoAPI.Geometries;

namespace TopologyTools.ReaderWriter
{
    public abstract class GeometryWriter : GeometryReaderWriter
    {
        protected GeometryWriter()
        {
        }

        protected GeometryWriter(IGeometryFactory factory)
            : base(factory)
        {
        }
    }
}