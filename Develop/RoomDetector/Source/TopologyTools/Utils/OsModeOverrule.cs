using System;
using Autodesk.AutoCAD.ApplicationServices;

namespace TopologyTools.Utils
{
    /// <summary>
    /// Temproarily adjust the snap mode to nearest only.
    /// Change this variable using Application.SetSystemVariable("OSMODE", Val) 
    /// the easiest way is to change the obectsnap in the osnap-settings-dialog to what you want to be, 
    /// close the dialog and look to the sysvar OSMODE. 
    /// 
    /// http://docs.autodesk.com/ACD/2011/ENU/filesACR/WS1a9193826455f5ffa23ce210c4a30acaf-4f1d.htm
    /// </summary>
    public class OsModeOverrule : IDisposable
    {
        public static Int16 OsModeENDpoint = 1;
        public static Int16 OsModeNEArest = 512;

        private object _originalSnapMode;
        public OsModeOverrule(object mode)
        {
            // GetsystemVariable and SetSystemVariable of the Application object can be used to access the OSMODE setting. Refer below .NET code
            _originalSnapMode = Application.GetSystemVariable("OSMODE");
            Application.SetSystemVariable("OSMODE", mode);
        }

        public void Dispose()
        {
            Application.SetSystemVariable("OSMODE", _originalSnapMode);
        }
    }

    public class SnapModeOverrule : IDisposable
    {
        private object _originalSnapMode;
        public SnapModeOverrule(bool mode)
        {
            // GetsystemVariable and SetSystemVariable of the Application object can be used to access the OSMODE setting. Refer below .NET code
            _originalSnapMode = Application.GetSystemVariable("SNAPMODE");
            Application.SetSystemVariable("SNAPMODE", mode ? 1 : 0);
        }

        public void Dispose()
        {
            Application.SetSystemVariable("SNAPMODE", _originalSnapMode);
        }
    }

     
}
