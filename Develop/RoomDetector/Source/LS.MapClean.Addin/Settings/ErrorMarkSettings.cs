using System;
using System.Windows.Media;
using LS.MapClean.Addin.MapClean;
using LS.MapClean.Addin.Utils;

namespace LS.MapClean.Addin.Settings
{
    public enum MarkShape
    {
        Circle,
        Triangle,
        Diamond,
        Square,
        Cross
    }

    public class ErrorMarkSettings
    {
        #region Constructors
        public ErrorMarkSettings()
            :this(fromSaved: false)
        {
        }

        public ErrorMarkSettings(bool fromSaved)
        {
            if (fromSaved)
            {
                var tempSettings = DeserializeFromXml();
                CopyFrom(tempSettings);
            }
            else
            {
                SetDefault();
            }
        }
        #endregion

        #region Singleton
        private static volatile ErrorMarkSettings _currentSettings;
        static object _syncRoot = new object();
        public static ErrorMarkSettings CurrentSettings
        {
            get
            {
                if (_currentSettings == null)
                {
                    lock (_syncRoot)
                    {
                        // Double check.
                        if (_currentSettings == null)
                        {
                            _currentSettings = new ErrorMarkSettings(false);
                        }
                    }
                }

                return _currentSettings;
            }
        }
        #endregion

        #region Properties
        /// <summary>
        /// Error marker size.
        /// </summary>
        public double MarkerSize { get; set; }

        /// <summary>
        /// Mark shap settings.
        /// </summary>
        public SerializableDictionary<ActionType, MarkShape> MarkShapes { get; set; }

        /// <summary>
        /// Mark color settings
        /// </summary>
        public SerializableDictionary<ActionType, Color> MarkColors { get; set; }
        #endregion

        #region Initialization
        public void SetDefault()
        {
            MarkerSize = 5.0;
            MarkShapes = new SerializableDictionary<ActionType, MarkShape>();
            // Initialize each mark shape
            MarkShapes.Add(ActionType.DuplicateVertexPline, MarkShape.Circle);
            MarkShapes.Add(ActionType.DeleteDuplicates, MarkShape.Diamond);
            MarkShapes.Add(ActionType.EraseShort, MarkShape.Square);
            MarkShapes.Add(ActionType.NoneZeroElevation, MarkShape.Square);
            MarkShapes.Add(ActionType.BreakCrossing, MarkShape.Square);
            MarkShapes.Add(ActionType.ExtendUndershoots, MarkShape.Circle);
            MarkShapes.Add(ActionType.ApparentIntersection, MarkShape.Circle);
            MarkShapes.Add(ActionType.SnapClustered, MarkShape.Circle);
            MarkShapes.Add(ActionType.DissolvePseudo, MarkShape.Triangle);
            MarkShapes.Add(ActionType.EraseDangling, MarkShape.Triangle);
            MarkShapes.Add(ActionType.ZeroLength, MarkShape.Diamond);
            MarkShapes.Add(ActionType.ZeroAreaLoop, MarkShape.Circle);
            MarkShapes.Add(ActionType.SmallPolygon, MarkShape.Cross);
            MarkShapes.Add(ActionType.AntiClockwisePolygon, MarkShape.Diamond);
            MarkShapes.Add(ActionType.UnclosedPolygon, MarkShape.Diamond);
            MarkShapes.Add(ActionType.IntersectPolygon, MarkShape.Cross);
            MarkShapes.Add(ActionType.SmallPolygonGap, MarkShape.Cross);
            MarkShapes.Add(ActionType.PolygonHole, MarkShape.Circle);
            MarkShapes.Add(ActionType.SelfIntersect, MarkShape.Circle);
            MarkShapes.Add(ActionType.MissingVertexInPolygon, MarkShape.Circle);
            MarkShapes.Add(ActionType.SelfIntersect2, MarkShape.Circle);
            MarkShapes.Add(ActionType.FindDangling, MarkShape.Circle);
            MarkShapes.Add(ActionType.OverlapPolygon, MarkShape.Circle);
            MarkShapes.Add(ActionType.AnnotationOverlap, MarkShape.Square);
            MarkShapes.Add(ActionType.FindIslandPolygon, MarkShape.Square);
            MarkShapes.Add(ActionType.ArcSegment, MarkShape.Circle);
            MarkShapes.Add(ActionType.RectifyPointDeviation, MarkShape.Cross);
            MarkShapes.Add(ActionType.SharpCornerPolygon, MarkShape.Circle);

            MarkColors = new SerializableDictionary<ActionType, Color>();
            MarkColors.Add(ActionType.DuplicateVertexPline, Colors.Red);
            MarkColors.Add(ActionType.DeleteDuplicates, Colors.Cyan);
            MarkColors.Add(ActionType.EraseShort, Colors.Red);
            MarkColors.Add(ActionType.NoneZeroElevation, Colors.BlueViolet);
            MarkColors.Add(ActionType.BreakCrossing, Colors.Green);
            MarkColors.Add(ActionType.ExtendUndershoots, Colors.Magenta);
            MarkColors.Add(ActionType.ApparentIntersection, Colors.Red);
            MarkColors.Add(ActionType.SnapClustered, Colors.Magenta);
            MarkColors.Add(ActionType.DissolvePseudo, Colors.Purple);
            MarkColors.Add(ActionType.EraseDangling, Colors.Red);
            MarkColors.Add(ActionType.ZeroLength, Colors.Red);
            MarkColors.Add(ActionType.ZeroAreaLoop, Colors.Red);
            MarkColors.Add(ActionType.SmallPolygon, Colors.Magenta);
            MarkColors.Add(ActionType.AntiClockwisePolygon, Colors.Gold);
            MarkColors.Add(ActionType.UnclosedPolygon, Colors.Lime);
            MarkColors.Add(ActionType.IntersectPolygon, Colors.DeepPink);
            MarkColors.Add(ActionType.SmallPolygonGap, Colors.Purple);
            MarkColors.Add(ActionType.PolygonHole, Colors.Goldenrod);
            MarkColors.Add(ActionType.SelfIntersect, Colors.DeepPink);
            MarkColors.Add(ActionType.MissingVertexInPolygon, Colors.Blue);
            MarkColors.Add(ActionType.SelfIntersect2, Colors.Brown);
            MarkColors.Add(ActionType.FindDangling, Colors.BlueViolet);
            MarkColors.Add(ActionType.OverlapPolygon, Colors.BlueViolet);
            MarkColors.Add(ActionType.AnnotationOverlap, Colors.Fuchsia);
            MarkColors.Add(ActionType.FindIslandPolygon, Colors.Fuchsia);
            MarkColors.Add(ActionType.ArcSegment, Colors.Chocolate);
            MarkColors.Add(ActionType.RectifyPointDeviation, Colors.Chartreuse);
            MarkColors.Add(ActionType.SharpCornerPolygon, Colors.DeepPink);
        }

        private void CopyFrom(ErrorMarkSettings other)
        {
            this.MarkerSize = other.MarkerSize;
            this.MarkShapes = other.MarkShapes;
            this.MarkColors = other.MarkColors;
        }
        #endregion

        #region Serialization
        public void Save()
        {
            SerializeToXml();
        }

        private static ErrorMarkSettings DeserializeFromXml()
        {
            throw new NotImplementedException();
        }

        private void SerializeToXml()
        {
            throw new NotImplementedException();
        }
        #endregion
    }
}
