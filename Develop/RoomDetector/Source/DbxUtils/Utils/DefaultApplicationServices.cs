// ApplicationServices.cs

using Autodesk.AutoCAD.DatabaseServices;

namespace DbxUtils.Utils
{
	/// <summary>
	/// A simple RealDWG host application services class.  This is intended to provide
	/// reasonable behavior to make it easy to get started quickly.  As a result, it's
	/// small and simple; if you need more functionality (which is perfectly OK),
	/// create your own HostApplicationServices instance.
	/// </summary>
	public sealed class DefaultHostApplicationServices : HostApplicationServices
	{
		/// <summary>
		/// This method is called by the database code when it is trying to locate a file.
		/// There is no default implementation. The RealDWG host application must override
		/// this method. The database will sometimes pass a FindFileHint that can be used
		/// to narrow the search. Refer to ObjectArx document for more information.
		/// </summary>
		/// <param name="fileName">Given name of the file to find.</param>
		/// <param name="database">The path of the DWG file associated with the database.</param>
		/// <param name="hint">Caller may pass a hint used to narrow the search.</param>
		/// <returns>The full path to the file.</returns>
		public override string FindFile(string fileName, Database database, FindFileHint hint)
		{
			return "";
		}
	}
}
