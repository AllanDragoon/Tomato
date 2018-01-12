using System;
using GeoAPI.Geometries;
using NetTopologySuite.Geometries;

namespace TopologyTools.ReaderWriter
{
    public abstract class GeometryReaderWriter
    {
        //private PrecisionModel _precisionModel = new PrecisionModel(3d);
        private IGeometryFactory _mGeometryFactory;
        public IGeometryFactory GeometryFactory
        {
            get
            {
                if (_mGeometryFactory == null)
                {
                    if (Math.Abs(PrecisionScale) > 1e-06)
                    {
                        var precisionModel = new PrecisionModel(PrecisionScale);
                        _mGeometryFactory = GeoAPI.GeometryServiceProvider.Instance.CreateGeometryFactory(precisionModel);
                    }
                    else
                        _mGeometryFactory = GeoAPI.GeometryServiceProvider.Instance.CreateGeometryFactory();
                }
                return this._mGeometryFactory;
            }
        }

        public IPrecisionModel PrecisionModel
        {
            get { return this.GeometryFactory.PrecisionModel; }
        }

        public double PrecisionScale { get; set; }

        public bool AllowRepeatedCoordinates { get; set; }

        protected GeometryReaderWriter()
        {
            PrecisionScale = 0.0;
        }

        protected GeometryReaderWriter(IGeometryFactory factory)
        {
            PrecisionScale = 0.0;
            this._mGeometryFactory = factory;
        }
    }
}