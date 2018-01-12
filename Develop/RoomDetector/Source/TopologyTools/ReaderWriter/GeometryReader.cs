using GeoAPI.Geometries;

namespace TopologyTools.ReaderWriter
{
    public abstract class GeometryReader : GeometryReaderWriter
    {
        public CurveTessellation CurveTessellationMethod { get; set; }

        readonly double _curveTessellationValue;
        public double CurveTessellationValue
        {
            get
            {
                switch (this.CurveTessellationMethod)
                {
                    case CurveTessellation.Linear:
                        if (this._curveTessellationValue > 0.0)
                        {
                            return this._curveTessellationValue;
                        }
                        return 16.0;
                    case CurveTessellation.Scaled:
                        if (this._curveTessellationValue > 0.0)
                        {
                            return this._curveTessellationValue;
                        }
                        return 1.0;
                    default:
                        return 0.0;
                }
            }
            set
            {
                value = this._curveTessellationValue;
            }
        }

        protected GeometryReader()
        {
            this.CurveTessellationMethod = CurveTessellation.Linear;
            this._curveTessellationValue = 15.0;
        }

        protected GeometryReader(IGeometryFactory factory)
            : base(factory)
        {
            this.CurveTessellationMethod = CurveTessellation.Linear;
            this._curveTessellationValue = 15.0;
        }
    }
}