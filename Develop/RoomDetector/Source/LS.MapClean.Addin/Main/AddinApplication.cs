using System.IO;
using System.Reflection;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LS.MapClean.Addin.Framework;
using LS.MapClean.Addin.Palettes;

namespace LS.MapClean.Addin.Main
{
    /// <summary>
    /// http://through-the-interface.typepad.com/through_the_interface/2006/09/initialization_.html
    /// </summary>
    public class AddinApplication : IExtensionApplication
    {
        #region Single Instance
        private static AddinApplication _instance;
        public static AddinApplication Instance
        {
            get { return _instance; }
        }
        #endregion

        public AddinApplication()
        { 
        }

        #region IExtensionApplication
        public void Initialize()
        {
            // Set single instance
            _instance = this;

            DialogService.Instance.MainHandle =
                    Autodesk.AutoCAD.ApplicationServices.Application.MainWindow.Handle;
            try
            {
                AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(CurrentDomain_AssemblyResolve);
                AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += new ResolveEventHandler(CurrentDomain_ReflectionOnlyAssemblyResolve);

                // WL: 这里有点问题，会有exception
                // Initialize the state of all palette sets.
                AllPaletteSets.InitPaletteSets();
            }
            catch (System.Exception ex)
            {
            }
        }

        public void Terminate()
        {
            AllPaletteSets.DisposePaletteSets();
        }
        #endregion

        #region Misc
        /// <summary>
        /// I encountered a problem that "LS.Utils.Extend.dll" and "System.Windows.Controls.Input.Toolkit.dll" 
        /// couldn't be loaded in AutoCAD, this is weird, I don't know the reason. 
        /// So use this method to make sure it's loaded before Addin is loaded.
        /// </summary>
        Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {

            try
            {
                var currentDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string pureAsmName = new AssemblyName(args.Name).Name;
                String assemblyName = pureAsmName + /*MSG0*/".dll";
                var dllpath = Path.Combine(currentDir, assemblyName);
                if (File.Exists(dllpath))
                {
                    return Assembly.LoadFile(dllpath);
                }
                else
                {
                    var resourcePath = Path.Combine(currentDir, "zh-CN", assemblyName);
                    if (File.Exists(resourcePath))
                        return Assembly.LoadFile(resourcePath);
                }
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Trace.WriteLine("Cannot load " + ex.Message);
            }

            return null;
        }

        private Assembly CurrentDomain_ReflectionOnlyAssemblyResolve(object sender, ResolveEventArgs args)
        {
            return Assembly.ReflectionOnlyLoad(args.Name);
        }
        #endregion
    }
}
