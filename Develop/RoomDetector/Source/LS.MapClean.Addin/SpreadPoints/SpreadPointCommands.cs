using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using LS.MapClean.Addin.Algorithms;
using LS.MapClean.Addin.SpreadPoints.View;
using LS.MapClean.Addin.SpreadPoints.ViewModel;
using LS.MapClean.Addin.Utils;
using OpenFileDialog = Autodesk.AutoCAD.Windows.OpenFileDialog;
using AcadApplication = Autodesk.AutoCAD.ApplicationServices.Application;

namespace LS.MapClean.Addin.SpreadPoints
{
    class SpreadPointCommands
    {
        #region APIS
        public static void ImportSpreadPoints(Document document, bool insertId, bool insertCode)
        {
            var mapScale = MapScaleUtils.GetApplicationMapScale();
            if (mapScale == null || mapScale.Value.EqualsWithTolerance(0.0))
            {
                bool success = SetMapScale(document);
                if (!success)
                    return;
                mapScale = MapScaleUtils.GetApplicationMapScale();
            }

            // Open file dialog to select dat file 
            // http://through-the-interface.typepad.com/through_the_interface/2007/08/using-autocads-.html
            var fileDialog = new OpenFileDialog("导入展点文件", defaultName: null,
                extension: "dat;*", dialogName: "SelectFile",
                flags: OpenFileDialog.OpenFileDialogFlags.DoNotTransferRemoteFiles);
            var dialogResult = fileDialog.ShowDialog();
            if (dialogResult != DialogResult.OK)
                return;

            // Create settings.
            var settings = new SpreadPointSettings()
            {
                InsertId = insertId,
                InsertCode = insertCode,
                Scale = mapScale.Value / 1000.0
            };

            var spreadPoints = SpreadPointUtils.ReadSpreadPointsFromFile(fileDialog.Filename);
            var ids = SpreadPointUtils.InsertSpreadPoints(document, spreadPoints, settings);

            if (!ids.Any())
                return;

            Extents3d extents = GeometryUtils.SafeGetGeometricExtents(ids);
            EditorUtils.ZoomToWin(document.Editor, extents, 1.2);
        }

        public static void UpdateSpreadPoints(Document document)
        {
            var vm = new SpreadPointSettingsViewModel();
            var dialog = new SpreadPointSettingsDlg() {DataContext = vm};
            var dlgResult = dialog.ShowDialog();
            if (dlgResult == null || !dlgResult.Value)
                return;

            var mapScale = MapScaleUtils.GetApplicationMapScale();
            if (mapScale == null)
                return;

            var settings = new SpreadPointSettings()
            {
                InsertCode = vm.ShowPointCode,
                InsertId = vm.ShowPointId,
                Scale = mapScale.Value / 1000.0
            };
            SpreadPointUtils.UpdateSpreadPoints(document, settings);
        }
        #endregion

        #region Utils
        private static bool SetMapScale(Document document)
        {
            // http://docs.autodesk.com/ACD/2010/ENU/AutoCAD%202010%20User%20Documentation/index.html?url=WS1a9193826455f5ffa23ce210c4a30acaf-4fff.htm,topicNumber=d0e346024
            var dynmode = AcadApplication.GetSystemVariable("DYNMODE");
            AcadApplication.SetSystemVariable("DYNMODE", 2); // 2 means "Dimensional input on"

            int defaultScale = 500;
            string strPrompt = String.Format("\n设置绘图比例尺为1:<{0}>", defaultScale);
            var pScaleOpts = new PromptIntegerOptions(strPrompt)
            {
                AllowNone = true,
                AllowZero = false
            };

            var pScaleRes = document.Editor.GetInteger(pScaleOpts);
            AcadApplication.SetSystemVariable("DYNMODE", dynmode);
            if (pScaleRes.Status == PromptStatus.OK || pScaleRes.Status == PromptStatus.None)
            {
                double scale = defaultScale;
                if (pScaleRes.Status == PromptStatus.OK)
                    scale = pScaleRes.Value;
                MapScaleUtils.SetApplicationMapScale(scale);

                return true;
            }
            return false;
        }
        #endregion
    }
}
