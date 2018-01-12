using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using Microsoft.Win32;
using System.Windows.Interop;

namespace DbxUtils.Utils
{
    /// <summary>
    /// Common HostApplicationServices class for the Factory Inventor Addins.
    /// </summary>
    public class ApplicationServices : HostApplicationServices
    {
        /// <summary>
        /// Read-only property contains a reference to the global Factory application services object.
		/// Make internal to make it available to "Test" assembly.
        /// </summary>
        internal static ApplicationServices Global { get; set; }

        /// <summary>
        /// Property override returns the registry key that points to the ObjectDBX section for
        /// a host application so that an attempt can be made to open known DLLsf or AutoCAD based
        /// products (i.e. AutoCAD Architecture).
        /// </summary>
        public string UserRegistryProductRootKey
        {
            get { return OverriddenRegistryProductRootKey; }
        }

        /// <summary>
        /// Property override returns the registry key that points to the ObjectDBX section for
        /// a host application so that an attempt can be made to open known DLLsf or AutoCAD based
        /// products (i.e. AutoCAD Architecture).
        /// </summary>
        public string MachineRegistryProductRootKey
        {
            get { return OverriddenRegistryProductRootKey; }
        }

        /// <summary>
        /// Property indicates the registry key that points to the ObjectDBX section for
        /// a host application so that an attempt can be made to open known DLLsf or AutoCAD based
        /// products (i.e. AutoCAD Architecture).
        /// </summary>
        public string OverriddenRegistryProductRootKey { get;  set; }

        /// <summary>
        /// Initialize RealDWG. Static call will force call of static constructor and then
        /// set the global instance of the app services object as "Current".
        /// </summary>
        public static void Initialize()
        {
			// This has side effect of setting HostApplicationServices.Current to Global
			RuntimeSystem.Initialize(new ApplicationServices(), CultureInfo.CurrentUICulture.LCID);
		}

        /// <summary>
        /// Terminate RealDWG RuntimeSystem. set the global instance of the app services object as null.
        /// </summary>
        public static void Terminate()
        {
			// Note: Though HostApplicationServices is an IDisposable, it seems that actually calling Dispose on it (or allowing it to be GC'd)
			//       conflicts with the call to RuntimeSystem.Terminate. If you do both things, in either order, bad exceptions result.
			RuntimeSystem.Terminate();
			HostApplicationServices.Current = null;
		}

        /// <summary>
        /// Upper coordinate of parent window relative to the desktop (optional).
        /// </summary>
        public IntPtr ParentFrame { get; set; }

        /// <summary>
        /// Called internally by host application services to prompt the user for a password.
        /// </summary>
        /// <param name="dwgName">Name of the drawing file that is requiring a password.</param>
        /// <param name="options">Additional security options.</param>
        /// <returns>The password.</returns>
        public override string GetPassword(string dwgName, PasswordOptions options)
        {
            var passwordDialog = new PasswordDialog(dwgName);

            // If a parent frame has been set on the app services object, then use that
            // frame as the dialog owner.
            //
            if (ParentFrame != null)
            {
                var wih = new WindowInteropHelper(passwordDialog);
                wih.Owner = ParentFrame;
            }

            // If we have a good password, return it making sure to convert the entire
            // password to upper case (required by RealDWG).
            if (passwordDialog.ShowDialog() == true)
                return passwordDialog.Password.ToUpperInvariant();

            // If the user cancelled, throw the "SecErrorDecryptingData" exception. This
            // should be temporary until a fix is made in RealDWG to handle cancel properly.
            throw new Autodesk.AutoCAD.Runtime.Exception(ErrorStatus.SecErrorDecryptingData);
        }

        /// <summary>
        /// This method is called by the database code when it is trying to locate a file.
        /// There is no default implementation. The RealDWG host application must override this method.
        /// The database will sometimes pass you a FindFileHint that you may use to narrow your search.
        /// Refer to ObjectArx document for more information.
        /// </summary>
        /// <param name="fileName">Given name of tghe file to find.</param>
        /// <param name="database">This will give you the path of the DWG file associated with the database.</param>
        /// <param name="hint">Caller may pass a hint used to narrow the search.</param>
        /// <returns></returns>
        public override string FindFile(string fileName, Database database, FindFileHint hint)
        {
            if (String.IsNullOrEmpty(fileName))
                return null;

            var extension = String.Empty;
            // add extension if needed
            if (!fileName.Contains("."))
            {
                switch (hint)
                {
                    case FindFileHint.CompiledShapeFile:
                        extension = /*MSG0*/".shx";
                        break;
                    case FindFileHint.TrueTypeFontFile:
                        extension = /*MSG0*/".ttf";
                        break;
                    case FindFileHint.PatternFile:
                        extension = /*MSG0*/".pat";
                        break;
                    case FindFileHint.ArxApplication:
                        extension = /*MSG0*/".dbx";
                        break;
                    case FindFileHint.FontMapFile:
                        extension = /*MSG0*/".fmp";
                        break;
                    case FindFileHint.XRefDrawing:
                        extension = /*MSG0*/".dwg";
                        break;
                    // Fall through. These could have various extensions
                    case FindFileHint.FontFile:
                    case FindFileHint.EmbeddedImageFile:
                    default:
                        extension = "";
                        break;
                }
                fileName += extension;
            }
            return SearchPath(fileName);
        }

        // Return the full path of the given filename.
        //
        private static string SearchPath(string fileName)
        {
            // If the file is found as is, there is no point in modifying the path.
            if (System.IO.File.Exists(fileName))
                return fileName;

            // Look for the file in some of the other standard places. Start by separating
            // the filename from the rest of the path.
            //
            string localFile = Path.GetFileName(fileName);

            // Check the folder of the executing application.
            string applicationPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (File.Exists(applicationPath + "\\" + localFile))
                return applicationPath + "\\" + localFile;

            // Search the folders in the %PATH% environment variable.
            string[] paths = Environment.GetEnvironmentVariable("Path").Split(new char[] { ';' });
            foreach (string path in paths)
            {
                string validatedPath = Path.GetFullPath(path + "\\" + localFile);
                if (File.Exists(validatedPath))
                    return validatedPath;
            }

            // Check the Fonts folders.
            string systemFonts = Environment.GetEnvironmentVariable("SystemRoot") + "\\Fonts\\";
            if (File.Exists(systemFonts + localFile))
                return systemFonts + localFile;

            // Search in Autocad application folder.
            string acadFonts = GetDefaultACADPath() + "\\Fonts\\";
            if (File.Exists(acadFonts + localFile))
                return acadFonts + localFile;

            return "";
        }

        private static string GetDefaultACADPath()
        {
            string acadExePath;
            if (TryGetDefaultAcadExecutablePath(out acadExePath) && File.Exists(acadExePath))
            {
                var fileInfo = new FileInfo(acadExePath);
                return fileInfo.Directory.FullName;
            }
            return "";
        }

        // TODO: Remove this Copy-paste code in Autodesk.Factory.ApplicationInterop AutoCADAutomation
        // We will consume the code in FactoryCore later.
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        private static bool TryGetDefaultAcadExecutablePath(out string executablePath)
        {
            // Executable: HKEY_CLASSES_ROOT\CLSID\{6D7AE628-FF41-4CD3-91DD-34825BB1A251}\LocalServer32
            executablePath = null;
            try
            {
                // CLSID: HKEY_CLASSES_ROOT\AutoCAD.Application\CLSID
                using (var clsIdRegkey = Registry.ClassesRoot.OpenSubKey(/*MSG0*/"AutoCAD.Application\\CLSID"))
                {
                    var clsId = (string)clsIdRegkey.GetValue(String.Empty);
                    var acadRegPath = "CLSID\\" + clsId + "\\LocalServer32";

                    // Executable: HKEY_CLASSES_ROOT\CLSID\CLSID = {6D7AE628-FF41-4CD3-91DD-34825BB1A251}\LocalServer32
                    using (var acadRegkey = Registry.ClassesRoot.OpenSubKey(acadRegPath))
                    {
                        executablePath = (string)acadRegkey.GetValue(String.Empty);

                        // Remove the commandline parameters
                        executablePath = Regex.Match(executablePath, /*MSG0*/"^.+\\.exe").Value;
                    }
                }
            }
            catch
            {
            }
            return !String.IsNullOrEmpty(executablePath);
        }
    }
}
