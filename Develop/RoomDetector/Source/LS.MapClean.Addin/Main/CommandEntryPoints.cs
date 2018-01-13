﻿using System.Diagnostics;
using System.Windows;
using System.Windows.Documents;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using System;
using System.Collections.Generic;
using LS.MapClean.Addin.Algorithms;
using LS.MapClean.Addin.MapClean;
using LS.MapClean.Addin.SpreadPoints;
using LS.MapClean.Addin.Utils;
using TopologyTools.ConvexHull;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace LS.MapClean.Addin.Main
{
    public static class CommandEntryPoints
    {
        const string GroupName = "MC";
        #region Test Commands
        /// <summary>
        /// AutoCAD .NET: Command Group and Command
        /// http://spiderinnet1.typepad.com/blog/2013/01/autocad-net-command-group-and-command.html
        /// </summary>
        [Autodesk.AutoCAD.Runtime.CommandMethod(GroupName, "MCTest", CommandFlags.Modal)]
        public static void TestCommand()
        {
            Editor editor = Application.DocumentManager.MdiActiveDocument.Editor;
            editor.WriteMessage("\nMapClean test command!");
        }
        #endregion

        [CommandMethod(GroupName, "MapClean", CommandFlags.Modal)]
        public static void MapClean()
        {
            var currentDoc = Application.DocumentManager.MdiActiveDocument;
            MapCleanService.Instance.StartMapClean(currentDoc);
        }

        /// <summary>
        /// 图形清理命令
        /// </summary>
        [CommandMethod(GroupName, "TXQL", CommandFlags.Modal)]
        public static void MapCleanActionSequence()
        {
            var currentDoc = Application.DocumentManager.MdiActiveDocument;
            var service = MapCleanService.Instance;
            if (service.Document != currentDoc || !service.ActionAgents.ContainsKey(ActionType.BreakCrossing))
            {
                var ret = MessageBox.Show("'图形清理'命令主要用在'造封闭地块'之前，如果此DWG中封闭地块已经创建，请使用'多边形拓扑检查'命令检查拓扑错误，是否继续此命令？", "提示", 
                    MessageBoxButton.YesNo);
                if (ret == MessageBoxResult.No)
                    return;

                service.StartMapCleanSequence(currentDoc);
            }
            else
            {
                service.ShowActionPalette(show: true, recordState: true);
            }
        }

        /// <summary>
        /// 多边形拓扑检查
        /// </summary>
        [CommandMethod(GroupName, "DBXJC", CommandFlags.Modal)]
        public static void PolygonCheckActionSequence()
        {
            var currentDoc = Application.DocumentManager.MdiActiveDocument;
            var service = MapCleanService.Instance;
            if (service.Document != currentDoc || !service.ActionAgents.ContainsKey(ActionType.UnclosedPolygon))
            {
                var ret = MessageBox.Show("'多边形拓扑检查'命令主要用在'造封闭地块'之后，如果此DWG中封闭地块还没创建，请使用'图形清理'命令清理图形错误，是否继续此命令？", "提示",
                    MessageBoxButton.YesNo);
                if (ret == MessageBoxResult.No)
                    return;
                service.StartPolygonCheckSequence(currentDoc);
            }
            else
            {
                service.ShowActionPalette(show: true, recordState: true);
            }
        }

        /// <summary>
        /// 高程不为0检查
        /// </summary>
        [CommandMethod(GroupName, "GCBWL", CommandFlags.Modal)]
        public static void ZeroElevation()
        {
            if (!IsCurrentDocValid())
                return;

            MapCleanService.Instance.OnlyCheckPolyline = false;
            MapCleanService.Instance.SetExecutingActions(new ActionType[] { ActionType.NoneZeroElevation });
            MapCleanService.Instance.CheckAndFix();
        }

        /// <summary>
        /// 直接高程不为0检查
        /// </summary>
        [CommandMethod(GroupName, "GCBWLC", CommandFlags.Modal)]
        public static void ZeroElevationConsole()
        {
            var document = Application.DocumentManager.MdiActiveDocument;
            MapCleanService.Instance.StartPolygonCheckConsole(document, ActionType.NoneZeroElevation);

            MapCleanService.Instance.OnlyCheckPolyline = false;
            MapCleanService.Instance.SetExecutingActions(new ActionType[] { ActionType.NoneZeroElevation });
            MapCleanService.Instance.CheckAndFix();
        }

        /// <summary>
        /// 清理多段线顶点
        /// </summary>
        [CommandMethod(GroupName, "DDXCDC", CommandFlags.Modal)]
        public static void FindDuplicateVerticesConsole()
        {
            var document = Application.DocumentManager.MdiActiveDocument;
            MapCleanService.Instance.StartPolygonCheckConsole(document, ActionType.DuplicateVertexPline);

            if (!SetToleranceByEditor(ActionType.DuplicateVertexPline, "设置重复点容差"))
                return;

            MapCleanService.Instance.OnlyCheckPolyline = true;
            MapCleanService.Instance.SetExecutingActions(new[] { ActionType.DuplicateVertexPline });
            MapCleanService.Instance.CheckAndFix();
        }

        /// <summary>
        /// 打断交叉线命令
        /// </summary>
        [CommandMethod(GroupName, "DDJCX", CommandFlags.Modal)]
        public static void BreakAllCurves()
        {
            if (!IsCurrentDocValid())
                return;

            MapCleanService.Instance.OnlyCheckPolyline = false;
            MapCleanService.Instance.SetExecutingActions(new ActionType[] { ActionType.BreakCrossing });
            MapCleanService.Instance.CheckAndFix();
        }

        /// <summary>
        /// 删除重复线命令
        /// </summary>
        [CommandMethod(GroupName, "SCCFX", CommandFlags.Modal)]
        public static void EraseDuplicateCurves()
        {
            if (!IsCurrentDocValid())
                return;

            if (!SetToleranceByEditor(ActionType.DeleteDuplicates, "设置容差"))
                return;

            var service = MapCleanService.Instance;
            service.OnlyCheckPolyline = false;
            service.SetExecutingActions(new ActionType[] { ActionType.DeleteDuplicates });
            service.Check();
        }

        /// <summary>
        /// 延伸未及点命令
        /// </summary>
        [CommandMethod(GroupName, "YSWJD", CommandFlags.Modal)]
        public static void ExtendUndershoots()
        {
            if (!IsCurrentDocValid())
                return;

            if (!SetToleranceByEditor(ActionType.ExtendUndershoots, "设置容差"))
                return;

            var service = MapCleanService.Instance;
            service.OnlyCheckPolyline = false;
            service.SetExecutingActions(new ActionType[] { ActionType.ExtendUndershoots });
            service.Check();
        }

        /// <summary>
        /// 外观交点
        /// </summary>
        [CommandMethod(GroupName, "XFWGJD", CommandFlags.Modal)]
        public static void ApparentIntersections()
        {
            if (!IsCurrentDocValid())
                return;

            if (!SetToleranceByEditor(ActionType.ApparentIntersection, "设置容差"))
                return;

            MapCleanService.Instance.OnlyCheckPolyline = false;
            MapCleanService.Instance.SetExecutingActions(new ActionType[] { ActionType.ApparentIntersection });
            MapCleanService.Instance.Check();
        }

        /// <summary>
        /// 捕捉节点簇
        /// </summary>
        [CommandMethod(GroupName, "BZJDC", CommandFlags.Modal)]
        public static void SnapClustered()
        {
            if (!IsCurrentDocValid())
                return;

            if (!SetToleranceByEditor(ActionType.SnapClustered, "设置容差"))
                return;

            MapCleanService.Instance.OnlyCheckPolyline = false;
            MapCleanService.Instance.SetExecutingActions(new ActionType[] { ActionType.SnapClustered });
            MapCleanService.Instance.Check();
        }

        /// <summary>
        /// 删除悬挂线
        /// </summary>
        [CommandMethod(GroupName, "SCXGX", CommandFlags.Modal)]
        public static void EraseDangling()
        {
            if (!IsCurrentDocValid())
                return;

            if (!SetToleranceByEditor(ActionType.EraseDangling, "设置最大悬挂线长度"))
                return;

            MapCleanService.Instance.OnlyCheckPolyline = false;
            MapCleanService.Instance.SetExecutingActions(new ActionType[] { ActionType.EraseDangling });
            MapCleanService.Instance.Check();
        }

        /// <summary>
        /// 零面积闭合线
        /// </summary>
        [CommandMethod(GroupName, "LMJBHX", CommandFlags.Modal)]
        public static void EraseZeroAreaLoop()
        {
            if (!IsCurrentDocValid())
                return;
            MapCleanService.Instance.OnlyCheckPolyline = true;
            MapCleanService.Instance.SetExecutingActions(new ActionType[] { ActionType.ZeroAreaLoop });
            MapCleanService.Instance.Check();
        }

        /// <summary>
        /// 直接零面积闭合线
        /// </summary>
        [CommandMethod(GroupName, "LMJBHXC", CommandFlags.Modal)]
        public static void EraseZeroAreaLoopConsole()
        {
            var document = Application.DocumentManager.MdiActiveDocument;
            MapCleanService.Instance.StartPolygonCheckConsole(document, ActionType.ZeroAreaLoop);

            MapCleanService.Instance.OnlyCheckPolyline = true;
            MapCleanService.Instance.SetExecutingActions(new ActionType[] { ActionType.ZeroAreaLoop });
            MapCleanService.Instance.Check();
        }

        /// <summary>
        /// 零长度对象
        /// </summary>
        [CommandMethod(GroupName, "SCLCDDX", CommandFlags.Modal)]
        public static void ZeroLength()
        {
            if (!IsCurrentDocValid())
                return;

            MapCleanService.Instance.OnlyCheckPolyline = false;
            MapCleanService.Instance.SetExecutingActions(new ActionType[] { ActionType.ZeroLength });
            MapCleanService.Instance.Check();
        }

        /// <summary>
        /// 直接零长度对象
        /// </summary>
        [CommandMethod(GroupName, "SCLCDDXC", CommandFlags.Modal)]
        public static void ZeroLengthConsole()
        {
            var document = Application.DocumentManager.MdiActiveDocument;
            MapCleanService.Instance.StartPolygonCheckConsole(document, ActionType.ZeroLength);

            MapCleanService.Instance.OnlyCheckPolyline = false;
            MapCleanService.Instance.SetExecutingActions(new ActionType[] { ActionType.ZeroLength });
            MapCleanService.Instance.Check();
        }

        /// <summary>
        /// 微短线检查
        /// </summary>
        [CommandMethod(GroupName, "SCDDX", CommandFlags.Modal)]
        public static void EraseShort()
        {
            if (!IsCurrentDocValid())
                return;

            if (!SetToleranceByEditor(ActionType.EraseShort, "设置短线最大长度"))
                return;

            MapCleanService.Instance.OnlyCheckPolyline = false;
            MapCleanService.Instance.SetExecutingActions(new ActionType[] { ActionType.EraseShort });
            MapCleanService.Instance.Check();
        }

        /// <summary>
        /// 直接微短线检查
        /// </summary>
        [CommandMethod(GroupName, "SCDDXC", CommandFlags.Modal)]
        public static void EraseShortConsole()
        {
            var document = Application.DocumentManager.MdiActiveDocument;
            MapCleanService.Instance.StartPolygonCheckConsole(document, ActionType.EraseShort);

            if (!SetToleranceByEditor(ActionType.EraseShort, "设置短线最大长度"))
                return;

            MapCleanService.Instance.OnlyCheckPolyline = false;
            MapCleanService.Instance.SetExecutingActions(new ActionType[] { ActionType.EraseShort });
            MapCleanService.Instance.Check();
        }

        /// <summary>
        /// 小面积多边形
        /// </summary>
        [CommandMethod(GroupName, "XMJDBX", CommandFlags.Modal)]
        public static void SmallPolygon()
        {
            if (!IsCurrentDocValid())
                return;

            if (!SetToleranceByEditor(ActionType.SmallPolygon, "设置最大面积"))
                return;

            MapCleanService.Instance.OnlyCheckPolyline = true;
            MapCleanService.Instance.SetExecutingActions(new ActionType[]{ ActionType.SmallPolygon });
            MapCleanService.Instance.Check();
        }

        /// <summary>
        /// 直接检查小面积多边形
        /// </summary>
        [CommandMethod(GroupName, "XMJDBXC", CommandFlags.Modal)]
        public static void SmallPolygonConsole()
        {
            var document = Application.DocumentManager.MdiActiveDocument;
            MapCleanService.Instance.StartPolygonCheckConsole(document, ActionType.SmallPolygon);

            if (!SetToleranceByEditor(ActionType.SmallPolygon, "设置最大面积"))
                return;

            MapCleanService.Instance.OnlyCheckPolyline = true;
            MapCleanService.Instance.SetExecutingActions(new ActionType[] { ActionType.SmallPolygon });
            MapCleanService.Instance.Check();
        }

        /// <summary>
        /// 直接检查逆时针多边形
        /// </summary>
        [CommandMethod(GroupName, "NSZDBXC", CommandFlags.Modal)]
        public static void AntiClockwisePolygonConsole()
        {
            var document = Application.DocumentManager.MdiActiveDocument;
            MapCleanService.Instance.StartPolygonCheckConsole(document, ActionType.AntiClockwisePolygon);

            MapCleanService.Instance.OnlyCheckPolyline = true;
            MapCleanService.Instance.SetExecutingActions(new ActionType[] { ActionType.AntiClockwisePolygon });
            MapCleanService.Instance.Check();
        }

        /// <summary>
        /// 检查弧段
        /// </summary>
        [CommandMethod(GroupName, "JCHD", CommandFlags.Modal)]
        public static void ArcSegment()
        {
            if (!IsCurrentDocValid())
                return;

            MapCleanService.Instance.OnlyCheckPolyline = false;
            MapCleanService.Instance.SetExecutingActions(new ActionType[] { ActionType.ArcSegment });
            MapCleanService.Instance.Check();
        }

        /// <summary>
        /// 直接检查弧段
        /// </summary>
        [CommandMethod(GroupName, "JCHDC", CommandFlags.Modal)]
        public static void ArcSegmentConsole()
        {
            var document = Application.DocumentManager.MdiActiveDocument;
            MapCleanService.Instance.StartPolygonCheckConsole(document, ActionType.ArcSegment);

            MapCleanService.Instance.OnlyCheckPolyline = true;
            MapCleanService.Instance.SetExecutingActions(new ActionType[] { ActionType.ArcSegment });
            MapCleanService.Instance.Check();
        }

        /// <summary>
        /// 纠合极近点
        /// </summary>
        [CommandMethod(GroupName, "JHJJD", CommandFlags.Modal)]
        public static void RectifyNearVerticesConsole()
        {
            if (!IsCurrentDocValid())
                return;

            if (!SetToleranceByEditor(ActionType.RectifyPointDeviation, "设置顶点偏离误差"))
                return;

            MapCleanService.Instance.OnlyCheckPolyline = false;
            MapCleanService.Instance.SetExecutingActions(new ActionType[] { ActionType.RectifyPointDeviation });
            MapCleanService.Instance.Check();
        }

        /// <summary>
        /// 直接纠合极近点
        /// </summary>
        [CommandMethod(GroupName, "JHJJDC", CommandFlags.Modal)]
        public static void RectifyNearVertices()
        {
            var document = Application.DocumentManager.MdiActiveDocument;
            MapCleanService.Instance.StartPolygonCheckConsole(document, ActionType.RectifyPointDeviation);

            if (!SetToleranceByEditor(ActionType.RectifyPointDeviation, "设置顶点偏离误差"))
                return;

            MapCleanService.Instance.OnlyCheckPolyline = true;
            MapCleanService.Instance.SetExecutingActions(new ActionType[] { ActionType.RectifyPointDeviation });
            MapCleanService.Instance.Check();
        }

        /// <summary>
        /// 直接检查狭长角多边形
        /// </summary>
        [CommandMethod(GroupName, "XCJDBXC", CommandFlags.Modal)]
        public static void SharpCornerPolygons()
        {
            var document = Application.DocumentManager.MdiActiveDocument;
            MapCleanService.Instance.StartPolygonCheckConsole(document, ActionType.SharpCornerPolygon);

            if (!SetToleranceByEditor(ActionType.SharpCornerPolygon, "设置多边形内部最小夹角值"))
                return;

            MapCleanService.Instance.OnlyCheckPolyline = true;
            MapCleanService.Instance.SetExecutingActions(new ActionType[] { ActionType.SharpCornerPolygon });
            MapCleanService.Instance.Check();
        }

        /// <summary>
        /// 非闭合多边形
        /// </summary>
        [CommandMethod(GroupName, "FBHDBX", CommandFlags.Modal)]
        public static void UnclosedPolygon()
        {
            if (!IsCurrentDocValid())
                return;

            MapCleanService.Instance.OnlyCheckPolyline = true;
            MapCleanService.Instance.SetExecutingActions(new ActionType[] { ActionType.UnclosedPolygon });
            MapCleanService.Instance.Check();
        }

        /// <summary>
        /// 直接检查非闭合多边形
        /// </summary>
        [CommandMethod(GroupName, "FBHDBXC", CommandFlags.Modal)]
        public static void UnclosedPolygonConsole()
        {
            var document = Application.DocumentManager.MdiActiveDocument;
            MapCleanService.Instance.StartPolygonCheckConsole(document, ActionType.UnclosedPolygon);

            MapCleanService.Instance.OnlyCheckPolyline = true;
            MapCleanService.Instance.SetExecutingActions(new ActionType[] { ActionType.UnclosedPolygon });
            MapCleanService.Instance.Check();
        }

        /// <summary>
        /// 交叉多边形
        /// </summary>
        [CommandMethod(GroupName, "JCDBX", CommandFlags.Modal)]
        public static void IntersectPolygon()
        {
            if (!IsCurrentDocValid())
                return;

            MapCleanService.Instance.OnlyCheckPolyline = true;
            MapCleanService.Instance.SetExecutingActions(new ActionType[] { ActionType.IntersectPolygon });
            MapCleanService.Instance.Check();
        }

        /// <summary>
        /// 直接检查交叉多边形
        /// </summary>
        [CommandMethod(GroupName, "JCDBXC", CommandFlags.Modal)]
        public static void IntersectPolygonConsole()
        {
            var document = Application.DocumentManager.MdiActiveDocument;
            MapCleanService.Instance.StartPolygonCheckConsole(document, ActionType.IntersectPolygon);

            MapCleanService.Instance.OnlyCheckPolyline = true;
            MapCleanService.Instance.SetExecutingActions(new ActionType[] { ActionType.IntersectPolygon });
            MapCleanService.Instance.Check();
        }

        /// <summary>
        /// 地块之间缝隙检查
        /// </summary>
        [CommandMethod(GroupName, "DKJFX", CommandFlags.Modal)]
        public static void SmallPolygonGap()
        {
            if (!IsCurrentDocValid())
                return;

            if (!SetToleranceByEditor(ActionType.SmallPolygonGap, "设置缝隙最大宽度"))
                return;

            MapCleanService.Instance.OnlyCheckPolyline = true;
            MapCleanService.Instance.SetExecutingActions(new ActionType[]{ ActionType.SmallPolygonGap});
            MapCleanService.Instance.Check();
        }

        /// <summary>
        /// 直接检查地块之间缝隙检查
        /// </summary>
        [CommandMethod(GroupName, "DKJFXC", CommandFlags.Modal)]
        public static void SmallPolygonGapConsole()
        {
            var document = Application.DocumentManager.MdiActiveDocument;
            MapCleanService.Instance.StartPolygonCheckConsole(document, ActionType.SmallPolygonGap);

            if (!SetToleranceByEditor(ActionType.SmallPolygonGap, "设置缝隙最大宽度"))
                return;

            MapCleanService.Instance.OnlyCheckPolyline = true;
            MapCleanService.Instance.SetExecutingActions(new ActionType[] { ActionType.SmallPolygonGap });
            MapCleanService.Instance.Check();
        }

        [CommandMethod(GroupName, "QLCFDBXC", CommandFlags.Modal)]
        public static void DuplicatePolygonConsole()
        {
            var document = Application.DocumentManager.MdiActiveDocument;
            MapCleanService.Instance.StartPolygonCheckConsole(document, ActionType.DuplicatePolygon);

            MapCleanService.Instance.OnlyCheckPolyline = true;
            MapCleanService.Instance.SetExecutingActions(new ActionType[] { ActionType.DuplicatePolygon });
            MapCleanService.Instance.CheckAndFix();
        }

        /// <summary>
        /// 直接检查封闭空地
        /// </summary>
        [CommandMethod(GroupName, "DKJKDC", CommandFlags.Modal)]
        public static void PolygonHoleConsole()
        {
            var document = Application.DocumentManager.MdiActiveDocument;
            MapCleanService.Instance.StartPolygonCheckConsole(document, ActionType.PolygonHole);

            MapCleanService.Instance.OnlyCheckPolyline = true;
            MapCleanService.Instance.SetExecutingActions(new ActionType[] { ActionType.PolygonHole });
            MapCleanService.Instance.Check();
        }

        /// <summary>
        /// 自交多段线
        /// </summary>
        [CommandMethod(GroupName, "ZJDDX", CommandFlags.Modal)]
        public static void SelfIntersect()
        {
            if (!IsCurrentDocValid())
                return;

            MapCleanService.Instance.OnlyCheckPolyline = true;
            MapCleanService.Instance.SetExecutingActions(new ActionType[]{ ActionType.SelfIntersect});
            MapCleanService.Instance.Check();
        }

        /// <summary>
        /// 直接自交多段线
        /// </summary>
        [CommandMethod(GroupName, "ZJDDXC", CommandFlags.Modal)]
        public static void SelfIntersectConsole()
        {
            var document = Application.DocumentManager.MdiActiveDocument;
            MapCleanService.Instance.StartPolygonCheckConsole(document, ActionType.SelfIntersect);

            MapCleanService.Instance.OnlyCheckPolyline = true;
            MapCleanService.Instance.SetExecutingActions(new ActionType[] { ActionType.SelfIntersect });
            MapCleanService.Instance.Check();
        }

        /// <summary>
        /// 三岔口无顶点
        /// </summary>
        [CommandMethod(GroupName, "SCKWDD", CommandFlags.Modal)]
        public static void MissingVertexInPolygonConsole()
        {
            var document = Application.DocumentManager.MdiActiveDocument;
            MapCleanService.Instance.StartPolygonCheckConsole(document, ActionType.MissingVertexInPolygon);

            MapCleanService.Instance.OnlyCheckPolyline = true;
            MapCleanService.Instance.SetExecutingActions(new ActionType[] { ActionType.MissingVertexInPolygon });
            MapCleanService.Instance.Check();
        }

        /// <summary>
        /// 自交多边形
        /// </summary>
        [CommandMethod(GroupName, "ZJDBM", CommandFlags.Modal)]
        public static void SelfIntersectInPolygonConsole()
        {
            var document = Application.DocumentManager.MdiActiveDocument;
            MapCleanService.Instance.StartPolygonCheckConsole(document, ActionType.SelfIntersect2);

            MapCleanService.Instance.OnlyCheckPolyline = true;
            MapCleanService.Instance.SetExecutingActions(new ActionType[] { ActionType.SelfIntersect2 });
            MapCleanService.Instance.Check();
        }

        /// <summary>
        /// 查找悬挂线
        /// </summary>
        [CommandMethod(GroupName, "CZXGX", CommandFlags.Modal)]
        public static void FindDanglingConsole()
        {
            var document = Application.DocumentManager.MdiActiveDocument;
            MapCleanService.Instance.StartPolygonCheckConsole(document, ActionType.FindDangling);

            MapCleanService.Instance.OnlyCheckPolyline = true;
            MapCleanService.Instance.SetExecutingActions(new ActionType[] { ActionType.FindDangling });
            MapCleanService.Instance.Check();
        }

        /// <summary>
        /// 查找重叠多边形
        /// </summary>
        [CommandMethod(GroupName, "CZCDDBX2", CommandFlags.Modal)]
        public static void FindOverlapPolygonConsole()
        {
            var document = Application.DocumentManager.MdiActiveDocument;
            MapCleanService.Instance.StartPolygonCheckConsole(document, ActionType.OverlapPolygon);

            MapCleanService.Instance.OnlyCheckPolyline = true;
            MapCleanService.Instance.SetExecutingActions(new ActionType[] { ActionType.OverlapPolygon });
            MapCleanService.Instance.Check();
        }

        /// <summary>
        /// 查找没有扣除的孔洞
        /// </summary>
        [CommandMethod(GroupName, "CZWCLKD", CommandFlags.Modal)]
        public static void FindIslandPolygonConsole()
        {
            var document = Application.DocumentManager.MdiActiveDocument;
            MapCleanService.Instance.StartPolygonCheckConsole(document, ActionType.FindIslandPolygon);

            MapCleanService.Instance.OnlyCheckPolyline = true;
            MapCleanService.Instance.SetExecutingActions(new [] { ActionType.FindIslandPolygon });
            MapCleanService.Instance.Check();
        }

        /// <summary>
        /// 检查图形清理所有项
        /// </summary>
        [CommandMethod(GroupName, "JCSYX", CommandFlags.Modal)]
        public static void CheckAll()
        {
            if (!IsCurrentDocValid())
                return;

            var service = MapCleanService.Instance;
            if (service.ActionsSettingViewModel != null)
            {
                service.ActionsSettingViewModel.ActionSelectVM.BreakCrossingObjects = true;
            }

            bool ret = service.NewMapCleanCheckWithDialog();
            if (ret)
            {
                service.ShowMapCleanPanel(true, true);
            }
        }

        private static bool IsCurrentDocValid()
        {
            var currentDoc = Application.DocumentManager.MdiActiveDocument;
            var service = MapCleanService.Instance;
            if (service.Document != currentDoc)
            {
                currentDoc.Editor.WriteMessage("\n此命令只能从图形清理面板上执行\n");
                return false;
            }
            return true;
        }

        private static bool SetToleranceByEditor(ActionType actionType, string message)
        {
            var service = MapCleanService.Instance;
            var action = service.ActionAgents[actionType].Action;

            var tolerance = UserInputTolerance(message, action.Tolerance);
            if (tolerance == null)
                return false;

            action.Tolerance = tolerance.Value;
            return true;
        }

        private static double? UserInputTolerance(string message, double defaultVal)
        {
            var editor = Application.DocumentManager.MdiActiveDocument.Editor;
            var tolerance = defaultVal;
            var msg = String.Format("\n{0}:", message);
            var option = new PromptDoubleOptions(msg);
            option.AllowNone = true;
            option.DefaultValue = defaultVal;
            var result = editor.GetDouble(option);
            if (result.Status == PromptStatus.Cancel)
                return null;

            if (result.Status == PromptStatus.OK)
                tolerance = result.Value;
            return tolerance;
        }

        //[CommandMethod(GroupName, "JCDXJC", CommandFlags.Modal)]
        //public static void CheckCrossingObject()
        //{
        //    var currentDoc = Application.DocumentManager.MdiActiveDocument;
        //    var editor = currentDoc.Editor;
        //    var database = currentDoc.Database;
        //    // Only select polyline and polyline 2d.
        //    ObjectId polylineId = ObjectId.Null;
        //    while (true)
        //    {
        //        var options = new PromptEntityOptions("\n选择一条曲线：");
        //        var result = editor.GetEntity(options);
        //        if (result.Status == PromptStatus.OK)
        //        {
        //            polylineId = result.ObjectId;
        //            break;
        //        }

        //        if (result.Status == PromptStatus.Cancel)
        //            break;

        //        editor.WriteMessage("\n选择无效");
        //    }

        //    if (polylineId.IsNull)
        //        return;

        //    if (!MapCleanService.Instance.CheckResultGroups.ContainsKey(ActionType.BreakCrossing))
        //        return;

        //    var group = MapCleanService.Instance.CheckResultGroups[ActionType.BreakCrossing];
        //    var checkResults = group.CheckResults.Where(it => it.SourceIds.Contains(polylineId));
        //    var objIds = new HashSet<ObjectId>();
        //    foreach (var checkResult in checkResults)
        //    {
        //        foreach( var objId in checkResult.SourceIds)
        //        {
        //            objIds.Add(objId);
        //        }
        //    }
        //    editor.WriteMessage("\n{0}个交叉结果", checkResults.Count());
        //    using (var transaction = currentDoc.Database.TransactionManager.StartTransaction())
        //    {
        //        foreach (var objectId in objIds)
        //        {
        //            var entity = transaction.GetObject(objectId, OpenMode.ForRead) as Entity;
        //            if (entity == null)
        //                continue;
        //            entity.Highlight();
        //        }
        //        transaction.Commit();
        //    }
        //}

        [CommandMethod("TestCH")]
        public static void TestConvexHull()
        {
            var currentDoc = Application.DocumentManager.MdiActiveDocument;
            var editor = currentDoc.Editor;
            var database = currentDoc.Database;
            // Only select polyline and polyline 2d.
            ObjectId polylineId = ObjectId.Null;
            while (true)
            {
                var options = new PromptEntityOptions("\n选择一条曲线：");
                var result = editor.GetEntity(options);
                if (result.Status == PromptStatus.OK)
                {
                    polylineId = result.ObjectId;
                    break;
                }

                if (result.Status == PromptStatus.Cancel)
                    break;

                editor.WriteMessage("\n选择无效");
            }

            if (polylineId.IsNull)
                return;

            using (var transaction = database.TransactionManager.StartTransaction())
            {
                IEnumerable<Point3d> points = CurveUtils.GetDistinctVertices(polylineId, transaction);
                var convex = new ConvexHull<Point3d>(points, it => it.X, it => it.Y,
                    (x, y) => new Point3d(x, y, 0), (a, b) => a == b);
                convex.CalcConvexHull();

                var hullPoints = convex.GetResultsAsArrayOfPoint();
                var polyline = new Autodesk.AutoCAD.DatabaseServices.Polyline();
                for (int i = 0; i < hullPoints.Length; i++)
                {
                    polyline.AddVertexAt(i, new Point2d(hullPoints[i].X, hullPoints[i].Y), 0, 1, 1 );
                }
                polyline.ColorIndex = 2;

                var modelspaceId = SymbolUtilityServices.GetBlockModelSpaceId(database);
                var modelspace = transaction.GetObject(modelspaceId, OpenMode.ForWrite) as BlockTableRecord;
                modelspace.AppendEntity(polyline);
                transaction.AddNewlyCreatedDBObject(polyline, true);
                transaction.Commit();
            }
        }

        [CommandMethod("TestSB")]
        public static void TestMinimumSurroundingBox()
        {
            var currentDoc = Application.DocumentManager.MdiActiveDocument;
            var editor = currentDoc.Editor;
            var database = currentDoc.Database;
            // Only select polyline and polyline 2d.
            ObjectId polylineId = ObjectId.Null;
            while (true)
            {
                var options = new PromptEntityOptions("\n选择一条曲线：");
                var result = editor.GetEntity(options);
                if (result.Status == PromptStatus.OK)
                {
                    polylineId = result.ObjectId;
                    break;
                }

                if (result.Status == PromptStatus.Cancel)
                    break;

                editor.WriteMessage("\n选择无效");
            }

            if (polylineId.IsNull)
                return;

            using (var transaction = database.TransactionManager.StartTransaction())
            {
                IEnumerable<Point3d> points = CurveUtils.GetDistinctVertices(polylineId, transaction);
                var boundingBox = CurveUtils.CalcMinimumSurroundingBox(points);
                var polyline = new Autodesk.AutoCAD.DatabaseServices.Polyline();
                for (int i = 0; i < boundingBox.Length; i++)
                {
                    polyline.AddVertexAt(i, new Point2d(boundingBox[i].X, boundingBox[i].Y), 0, 1, 1);
                }
                polyline.ColorIndex = 2;

                var modelspaceId = SymbolUtilityServices.GetBlockModelSpaceId(database);
                var modelspace = transaction.GetObject(modelspaceId, OpenMode.ForWrite) as BlockTableRecord;
                modelspace.AppendEntity(polyline);
                transaction.AddNewlyCreatedDBObject(polyline, true);
                transaction.Commit();
            }
        }

        [CommandMethod(GroupName, "TestSMP", CommandFlags.Modal)]
        public static void TestSearchMinimumPolygons()
        {
            var drawingDb = Application.DocumentManager.MdiActiveDocument.Database;
            var editor = Application.DocumentManager.MdiActiveDocument.Editor;

            editor.WriteMessage("\n选择一条或几条闭合曲线：\n");
            var result = editor.GetSelection();
            if (result.Status != PromptStatus.OK)
                return;
            var polylineids = result.Value.GetObjectIds();
            using(var tolerance = new SafeToleranceOverride(null, null))
            using (var transaction = drawingDb.TransactionManager.StartTransaction())
            {
                var polylines = new List<Polyline>();
                foreach (var polylineid in polylineids)
                {
                    var polyline = transaction.GetObject(polylineid, OpenMode.ForRead) as Polyline;
                    polylines.Add(polyline.Clone() as Polyline);
                }
                Dictionary<Curve, List<Curve>> replaced = null;
                var polygons = MinimalLoopSearcher2.SearchMinimalPolygons(polylines, out replaced);
                var modelspaceId = SymbolUtilityServices.GetBlockModelSpaceId(drawingDb);
                var modelspace = transaction.GetObject(modelspaceId, OpenMode.ForWrite) as BlockTableRecord;
                foreach (var polyline in polygons)
                {
                    polyline.Layer = "dk";
                    polyline.Color = Color.FromColorIndex(ColorMethod.ByAci, 3);
                    modelspace.AppendEntity(polyline);
                    transaction.AddNewlyCreatedDBObject(polyline, true);
                }
                transaction.Commit();
            }
            
        }

        [CommandMethod(GroupName, "TestLoop", CommandFlags.Modal)]
        public static void TestSearchAllLoops()
        {
            var drawingDb = Application.DocumentManager.MdiActiveDocument.Database;
            var editor = Application.DocumentManager.MdiActiveDocument.Editor;

            editor.WriteMessage("\n选择一条或几条曲线：\n");
            var result = editor.GetSelection();
            if (result.Status != PromptStatus.OK)
                return;

            var polylineids = result.Value.GetObjectIds();
            var polygons = MinimalLoopSearcher2.GetAllLoopPolylines(polylineids, editor.Document);
            using (var transaction = drawingDb.TransactionManager.StartTransaction())
            {
                var modelspaceId = SymbolUtilityServices.GetBlockModelSpaceId(drawingDb);
                var modelspace = transaction.GetObject(modelspaceId, OpenMode.ForWrite) as BlockTableRecord;
                foreach (var polyline in polygons)
                {
                    polyline.Color = Color.FromColorIndex(ColorMethod.ByAci, 3);
                    modelspace.AppendEntity(polyline);
                    transaction.AddNewlyCreatedDBObject(polyline, true);
                }
                transaction.Commit();
            }
        }

        [CommandMethod(GroupName, "TestHatch", CommandFlags.Modal)]
        public static void CheckHatch()
        {
            var drawingDb = Application.DocumentManager.MdiActiveDocument.Database;
            var editor = Application.DocumentManager.MdiActiveDocument.Editor;

            editor.WriteMessage("\n选择一条或几条闭合曲线：\n");
            var result = editor.GetSelection();
            if (result.Status != PromptStatus.OK)
                return;
            var hatchloopIds = result.Value.GetObjectIds();
            using (var transaction = drawingDb.TransactionManager.StartTransaction())
            {
                var modelspaceId = SymbolUtilityServices.GetBlockModelSpaceId(drawingDb);

                Hatch hat = new Hatch();
                hat.SetDatabaseDefaults(drawingDb);
                hat.SetHatchPattern(HatchPatternType.PreDefined, "ANSI31");
                hat.HatchObjectType = HatchObjectType.HatchObject;

                // Add the hatch to the model space
                // and the transaction
                var modelspace = (BlockTableRecord)transaction.GetObject(modelspaceId, OpenMode.ForWrite);
                ObjectId hatId = modelspace.AppendEntity(hat);
                transaction.AddNewlyCreatedDBObject(hat, true);

                // Add the hatch loop and complete the hatch
                hat.Associative = false;
                hat.PatternScale = 20;
                hat.HatchStyle = HatchStyle.Normal;
                foreach (ObjectId id in hatchloopIds)
                {
                    var loopType = HatchLoopTypes.Outermost;
                    var entity = transaction.GetObject(id, OpenMode.ForRead);
                    if (entity is DBText)
                        loopType = HatchLoopTypes.Textbox;

                    var ids = new ObjectIdCollection();
                    ids.Add(id);
                    hat.AppendLoop(loopType, ids); // error here....
                }

                hat.EvaluateHatch(true);
                transaction.Commit();
            }
        }

        /// <summary>
        /// 测试命令
        /// </summary>
        [CommandMethod(GroupName, "CSCS", CommandFlags.Modal)]
        public static void CheckCrossingObject()
        {
            var currentDoc = Application.DocumentManager.MdiActiveDocument;
            var editor = currentDoc.Editor;
            var database = currentDoc.Database;
            // Only select polyline and polyline 2d.
            ObjectId polylineId = ObjectId.Null;
            while (true)
            {
                var options = new PromptEntityOptions("\n选择一条曲线：");
                var result = editor.GetEntity(options);
                if (result.Status == PromptStatus.OK)
                {
                    polylineId = result.ObjectId;
                    break;
                }

                if (result.Status == PromptStatus.Cancel)
                    break;

                editor.WriteMessage("\n选择无效");
            }

            if (polylineId.IsNull)
                return;

            var pointResult = editor.GetPoint("选择线上一点");
            if (pointResult.Status != PromptStatus.OK)
                return;
            var point = pointResult.Value;
            using (var transaction = currentDoc.Database.TransactionManager.StartTransaction())
            {
                var curve = (Curve)transaction.GetObject(polylineId, OpenMode.ForRead);
                var param = GeometryUtils.GetPointParameter(curve, point);
                if(param != null)
                    editor.WriteMessage("\n参数值:{0}", param.Value);

                param = GeometryUtils.GetPointParameter(curve, curve.EndPoint);
                editor.WriteMessage("\n末点参数值:{0}", param.Value);
                editor.WriteMessage("\n末尾参数值:{0}", curve.EndParam);
            }
            
        }

        [CommandMethod(GroupName, "UNPL", CommandFlags.Modal)]
        public static void UnionPolygons()
        {
            var stopWatch = new Stopwatch();
            stopWatch.Start();

            var currentDoc = Application.DocumentManager.MdiActiveDocument;
            var polylineIds = MapCleanService.GetAllVisiblePolylineObjectIds(currentDoc);

            var database = currentDoc.Database;
            var partitioner = new DrawingPartitioner(database);
            partitioner.Check(polylineIds);
            using (var transaction = database.TransactionManager.StartTransaction())
            {
                var modelspaceId = SymbolUtilityServices.GetBlockModelSpaceId(database);
                var modelspace = (BlockTableRecord)transaction.GetObject(modelspaceId, OpenMode.ForWrite);
                foreach (var region in partitioner.IsolatedRegions)
                {
                    var polyline = new Polyline(region.Contour.Count);
                    int i = 0;
                    foreach (var point in region.Contour)
                    {
                        polyline.AddVertexAt(i++, new Point2d(point.X, point.Y), 0,0,0);
                    }
                    polyline.Closed = true;
                    polyline.ColorIndex = 3;
                    modelspace.AppendEntity(polyline);
                    transaction.AddNewlyCreatedDBObject(polyline, add: true);
                }
                transaction.Commit();
            }

            stopWatch.Stop();
            currentDoc.Editor.WriteMessage("\n图形分区花费{0}毫秒\n", stopWatch.ElapsedMilliseconds);
        }


        #region Spread Points Utils
        /// <summary>
        /// 展野外测点点号
        /// </summary>
        [CommandMethod(GroupName, "ZCDDH", CommandFlags.Modal)]
        public static void InsertSpreadPointsWithId()
        {
            var currentDoc = Application.DocumentManager.MdiActiveDocument;
            SpreadPointCommands.ImportSpreadPoints(currentDoc, insertId: true, insertCode:false);
        }

        /// <summary>
        /// 展野外测点点码
        /// </summary>
        [CommandMethod(GroupName, "ZCDDM", CommandFlags.Modal)]
        public static void InsertSpreadPointWithCode()
        {
            var currentDoc = Application.DocumentManager.MdiActiveDocument;
            SpreadPointCommands.ImportSpreadPoints(currentDoc, insertId:false, insertCode: true);
        }

        /// <summary>
        /// 展野外测点点位
        /// </summary>
        [CommandMethod(GroupName, "ZCDDW", CommandFlags.Modal)]
        public static void InsertSpreadPoint()
        {
            var currentDoc = Application.DocumentManager.MdiActiveDocument;
            SpreadPointCommands.ImportSpreadPoints(currentDoc, insertId:false, insertCode: false);
        }

        /// <summary>
        /// 切换展点注记
        /// </summary>
        [CommandMethod(GroupName, "QHZDZJ", CommandFlags.Modal)]
        public static void SwitchSpreadPointAnnotation()
        {
            var currentDoc = Application.DocumentManager.MdiActiveDocument;
            SpreadPointCommands.UpdateSpreadPoints(currentDoc);
        }
        #endregion

        [CommandMethod(GroupName, "FXSB", CommandFlags.Modal)]
        public static void RecognizeRoomAndWalls()
        {
            var currentDoc = Application.DocumentManager.MdiActiveDocument;
            var editor = currentDoc.Editor;
            editor.WriteMessage("\nLet's begin to recognize the walls and rooms!\n");
            RoomWallRecognizer.Recognize();
        }
    }
}
