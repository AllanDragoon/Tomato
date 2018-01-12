using Autodesk.AutoCAD.DatabaseServices;
using System;

namespace DbxUtils.Utils
{
	/// <summary>
	/// Simple utilty class that can be wrapped in a using() statement
	/// </summary>
	public sealed class RestoreWorkingDatabase : IDisposable
	{
		readonly Database _old;

        public RestoreWorkingDatabase(Database database)
        {
            _old = HostApplicationServices.WorkingDatabase;        
            HostApplicationServices.WorkingDatabase = database;
		}

		public void Dispose()
        {
            HostApplicationServices.WorkingDatabase = _old;
		}
	}
}
