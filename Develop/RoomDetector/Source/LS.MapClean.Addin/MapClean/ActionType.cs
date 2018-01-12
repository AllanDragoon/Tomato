using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LS.MapClean.Addin.MapClean
{
    /// <summary>
    /// http://knowledge.autodesk.com/support/autocad-map-3d/learn-explore/caas/documentation/MAP/2014/ENU/filesMAPUSE/GUID-5A506E7C-B864-4CB7-B132-A543EC4CC888-htm.html
    /// </summary>
    public enum ActionType
    {
        NoneZeroElevation,
        DuplicateVertexPline,
        BreakCrossing,
        DeleteDuplicates,
        ExtendUndershoots,
        ApparentIntersection,
        SnapClustered,
        EraseDangling,
        ZeroAreaLoop,
        ZeroLength,
        EraseShort,
        DissolvePseudo,
        // Polygon toplogy
        SmallPolygon,
        UnclosedPolygon,
        IntersectPolygon,
        SmallPolygonGap,
        SelfIntersect,
        PolygonHole,
        // Annotation overlap
        AnnotationOverlap,

        MissingVertexInPolygon,
        SelfIntersect2,
        FindDangling,
        OverlapPolygon,
        AntiClockwisePolygon
    }

    public static class ActionTypeUtils
    {
        public static string ToChineseName(this ActionType actionType)
        {
            string result = String.Empty;
            switch (actionType)
            {
                case ActionType.NoneZeroElevation:
                    result = "高程不为0对象检查";
                    break;
                case ActionType.DuplicateVertexPline:
                    result = "多段线重点检查";
                    break;
                case ActionType.DeleteDuplicates:
                    result = "检查重复对象";
                    break;
                case ActionType.EraseShort:
                    result = "检查微短线";
                    break;
                case ActionType.BreakCrossing:
                    result = "打断交叉对象";
                    break;
                case ActionType.ExtendUndershoots:
                    result = "延伸未及点";
                    break;
                case ActionType.ApparentIntersection:
                    result = "延伸外观交点";
                    break;
                case ActionType.SnapClustered:
                    result = "捕捉聚合节点";
                    break;
                case ActionType.DissolvePseudo:
                    result = "融合伪节点";
                    break;
                case ActionType.EraseDangling:
                    result = "检查悬挂对象";
                    break;
                case ActionType.ZeroLength:
                    result = "检查零长度对象";
                    break;
                case ActionType.ZeroAreaLoop:
                    result = "检查0面积闭合线";
                    break;
                case ActionType.SmallPolygon:
                    result = "小面积多边形";
                    break;
                case ActionType.UnclosedPolygon:
                    result = "非闭合多段线";
                    break;
                case ActionType.IntersectPolygon:
                    result = "重叠多边形";
                    break;
                case ActionType.SmallPolygonGap:
                    result = "地块边界缝隙";
                    break;
                case ActionType.PolygonHole:
                    result = "地块间孔洞";
                    break;
                case ActionType.SelfIntersect:
                    result = "自相交或回头线 ";
                    break;
                case ActionType.MissingVertexInPolygon:
                    result = "三岔口缺顶点";
                    break;
                case ActionType.SelfIntersect2:
                    result = "自相交";
                    break;
                case ActionType.FindDangling:
                    result = "悬挂线";
					break;
                case ActionType.OverlapPolygon:
                    result = "重复多边形";
					break;
                case ActionType.AntiClockwisePolygon:
                    result = "逆时针多边形";
                    break;
                case ActionType.AnnotationOverlap:
                    result = "地块标注重叠";
                    break;
            }
            return result;
        }
    }
}
