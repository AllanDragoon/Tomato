using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using System.Diagnostics;

namespace DbxUtils.Utils
{
    /// <summary>
    /// Enum used to identify the reserved string to fetch from RealDWG.
    /// </summary>
    public enum ReservedStringType
    {
        /// <summary>
        /// ByBlock
        /// </summary>
        ByBlock,

        /// <summary>
        /// ByColor
        /// </summary>
        ByColor,

        /// <summary>
        /// ByLayer
        /// </summary>
        ByLayer,

        /// <summary>
        /// Continuous
        /// </summary>
        Continuous,

        /// <summary>
        /// Data
        /// </summary>
        Data,

        /// <summary>
        /// Default
        /// </summary>
        Default,

        /// <summary>
        /// DefPoints
        /// </summary>
        DefPoints,

        /// <summary>
        /// Global
        /// </summary>
        Global,

        /// <summary>
        /// Header
        /// </summary>
        Header,

        /// <summary>
        /// Missing
        /// </summary>
        Missing,

        /// <summary>
        /// Model
        /// </summary>
        Model,
        
        /// <summary>
        /// None
        /// </summary>
        None,

        /// <summary>
        /// Normal
        /// </summary>
        Normal,

        /// <summary>
        /// Standard
        /// </summary>
        Standard,

        /// <summary>
        /// Title
        /// </summary>
        Title
    }

    /// <summary>
    /// Runtime class contains various initialization and global runtime methods
    /// not associated with any given object type.
    /// </summary>
    public static class Runtime
    {
		// Used as a synchronization object to ensure thread safety in Use, ForRead, etc.
		// This is intentionally not database-specific, and therefore coordinate all RealDwg activities in which it's used.
		internal static object DBSyncObject = new object();
        static bool isInitialized = false;

        /// <summary>
        /// Initialize DWG usage by creating and setting up the global app
        /// services object.
        /// </summary>
        public static void Initialize()
        {
            // Call into the initiliaze method on the application services
            // class. Note that calling this method is preferable to calling
            // the method below directly as this method allows external modules
            // to avoid having to add AcdbMgd as a reference.
            //
            if (!isInitialized)
            { 
                ApplicationServices.Initialize();
                isInitialized = true;
            }
        }

        /// <summary>
        /// Terminate DWG
        /// </summary>
        public static void Terminate()
        {
            // Call into the Terminate method on the application services
            // class. Note that calling this method is preferable to calling
            // the method below directly as this method allows external modules
            // to avoid having to add AcdbMgd as a reference.
            //
            ApplicationServices.Terminate();
        }

        /// <summary>
        /// Fetch a localized version of a RealDWG reserved string.
        /// </summary>
        /// <param name="inputType">The reserved string to look for.</param>
        /// <returns>The resulting localized string.</returns>
        public static string GetReservedString(ReservedStringType inputType)
        {
            var outputType = ReservedStringEnumType.None;

            switch (inputType)
            {
                case ReservedStringType.ByBlock:
                    outputType = ReservedStringEnumType.ByBlock;
                    break;
                case ReservedStringType.ByColor:
                    outputType = ReservedStringEnumType.ByColor;
                    break;
                case ReservedStringType.ByLayer:
                    outputType = ReservedStringEnumType.ByLayer;
                    break;
                case ReservedStringType.Continuous:
                    outputType = ReservedStringEnumType.Continuous;
                    break;
                case ReservedStringType.Data:
                    outputType = ReservedStringEnumType.Data;
                    break;
                case ReservedStringType.Default:
                    outputType = ReservedStringEnumType.Default;
                    break;
                case ReservedStringType.DefPoints:
                    outputType = ReservedStringEnumType.DefPoints;
                    break;
                case ReservedStringType.Global:
                    outputType = ReservedStringEnumType.Global;
                    break;
                case ReservedStringType.Header:
                    outputType = ReservedStringEnumType.Header;
                    break;
                case ReservedStringType.Missing:
                    outputType = ReservedStringEnumType.Missing;
                    break;
                case ReservedStringType.Model:
                    outputType = ReservedStringEnumType.Model;
                    break;
                case ReservedStringType.None:
                    outputType = ReservedStringEnumType.None;
                    break;
                case ReservedStringType.Normal:
                    outputType = ReservedStringEnumType.Normal;
                    break;
                case ReservedStringType.Standard:
                    outputType = ReservedStringEnumType.Standard;
                    break;
                case ReservedStringType.Title:
                    outputType = ReservedStringEnumType.Title;
                    break;
            }

            return Utilities.GetReservedString(outputType, true);
        }
    }

    /// <summary>
    /// Runtime extension methods for unit testing purpose
    /// </summary>
    public static class RuntimeUnitTestExtensions
    {
        static Database _sDatabase;

        public static Database DBDatabase
        {
            get
            {
                return _sDatabase;
            }
        }

        /// <summary>
        /// Initialize RealDWG Environent for testing purpose.
        /// If the test code is executed in AutoCAD application, we don't need to initialize the customized ApplicationService,
        /// Otherwise, we create one.
        /// </summary>
        public static void InitializeRealDwgTestEnvironent()
        {
            if (Process.GetCurrentProcess().ProcessName != "acad")
            {
                Runtime.Initialize();
                _sDatabase = DatabaseUtilities.CreateDwg();
            }
        }

        /// <summary>
        /// Initialize RealDWG Environent for testing purpose.
        /// </summary>
        public static void DeInitializeRealDwgTestEnvironent()
        {
            if (Process.GetCurrentProcess().ProcessName != "acad")
            {
                if (_sDatabase != null)
                {
                    _sDatabase.Dispose();
                }
                Runtime.Terminate();
            }
        }
    }
}
