using System;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;

namespace DbxUtils.Utils
{
	/// <summary>
	/// Factory-specific extension methods for AutoCAD ObjectIds; these are not intended for wide-spread use
	/// </summary>
	public static class FactoryObjectIdExtensions
	{
		static class TransactionUtilities
		{
			[Conditional("DEBUG")]
			static void DoPossibleDowngradeOpen(DBObject dbObject, OpenMode openMode)
			{
				if ((openMode == OpenMode.ForRead) && dbObject.IsWriteEnabled)
					dbObject.DowngradeOpen();
			}

			[Conditional("DEBUG")]
			static void DoUpgradeOpen(DBObject dbObject)
			{
				if (!dbObject.IsWriteEnabled)
					dbObject.UpgradeOpen();
			}

			// This is a separate method--rather than just calling transaction.GetObject()--because in some situations we might
			// want to try dealing with Dispose()ing transactions; see AutoCAD defect 1381839.
			static TResult GetObject<T, TResult>(ObjectId id, OpenMode openMode, Func<T, TResult> f, Transaction transaction) where T : DBObject
			{
				if (f == null) throw new ArgumentNullException(/*MSG0*/"f");
				if (transaction == null) throw new ArgumentNullException(/*MSG0*/"transaction");

				// We don't expect to open any erased (or null) objects (notice no "openErased" parameter),
				// so if we've got one throw an exception right here rather than waiting until later
				if (id.IsNull) throw new ArgumentNullException(/*MSG0*/"id");
				if (id.IsErased) throw new Autodesk.AutoCAD.Runtime.Exception(Autodesk.AutoCAD.Runtime.ErrorStatus.WasErased);

				var t = (T)transaction.GetObject(id, openMode);

				// The object might already be opened with a less restrictive (ForWrite) mode.
				// If that's the case, adjust the object to match the requested mode and restore
				// afterwards.  Note that this is only done for DEBUG builds.
				bool wasWriteEnabled = t.IsWriteEnabled; // DoPossibleDowngradeOpen() might change, so save for later
				DoPossibleDowngradeOpen(t, openMode); // [Conditional("DEBUG")]
				try // always restore even if f() throws
				{
					return f(t);
				}
				finally
				{
					// The object might have already been opened elsewhere ForWrite;
					// if so, restore previous mode
					if (wasWriteEnabled)
						DoUpgradeOpen(t); // [Conditional("DEBUG")]
				}
			}

			static void EndTransaction(Transaction transaction, OpenMode? openMode)
			{
				// While Commit() can actually be faster than Abort(), it can also result in anomalous behavior.
				// Specifically, if the object has ALREADY been opened ForWrite by a containing transaction,
				// then AutoCAD will just use that same object pointer when this transaction opens ForRead.
				// If we then Commit() as this transaction ends, what was supposed to be a read operation
				// has now changed the object.
				//
				// This is clearly a bug in the calling code: it shouldn't be trying to write while opening the
				// object ForRead.  But it could be hard to track down, as it depends on whether or not
				// there is an outer transaction, and whether the object was previously opened ForWrite.
				// By calling Abort() (actually, not calling Commit() and letting the transaction fall out of the
				// "using" block) the behavior will at least be consistent.
				// NOTE: If we don't commit the transaction, our transient graphics get destroyed. The workflow was
				// getting the BasePoint when drawing our connection lines. BasePoint would call this method and
				// the transaction would not be committed. This was causing the transient data to be rolled back
				// for some reason. Always commit and deal with the issue listed above.

				//Inventor Code Coverage can't recognize C# preprocessing directives, to avoid the errors caused by this limitation, 
				//here call transaction.Commit() twice, one in # if section, and the other is under # else section.
				//More detail, please refer to #2 of http://wiki.autodesk.com/display/INF/Code+Coverage+practices+for+source+code
#if DEBUG
				bool doCommit = (openMode == null) || (openMode.Value == OpenMode.ForWrite);
				if (doCommit) transaction.Commit();  // only Commit() objects opened ForWrite in DEBUG builds
#else
                    // *always* Commit() in non-DEBUG builds
                    transaction.Commit(); // it seems that Commit() is actually faster than Abort()
#endif
			}

			static TResult UseStartTransaction<T, TResult>(ObjectId id, OpenMode openMode, Func<T, TResult> f) where T : DBObject
			{
				using (var transaction = id.Database.TransactionManager.StartTransaction())
				{
					TResult retval = GetObject(id, openMode, f, transaction);
					EndTransaction(transaction, openMode);
					return retval;					
				}
			}

			static TResult UseStartOpenCloseTransaction<T, TResult>(ObjectId id, OpenMode openMode, Func<T, TResult> f) where T : DBObject
			{
				// an OpenCloseTransaction is just a wrapper around ObjectId.Open() and DBObject.Close()/Cancel()
				using (var transaction = id.Database.TransactionManager.StartOpenCloseTransaction())
				{
					TResult retval = GetObject(id, openMode, f, transaction);

					OpenMode? commitTransaction = openMode;
					DBObject dbObj = transaction.GetObject(id, openMode);
					if (dbObj.IsTransactionResident) // open in a real transaction, not the fake Open/Close "transaction" we're in now
					{
						// Always commit (Close()) objects opened in OpenClose transaction; here's what SEEMS to be the situation:
						// 1) a new object is created and added to a regular transaction, 2) it is opened/canceled (aborted) via OpenClose, and then
						// 3) (2) is tried again but the object is now gone!
						commitTransaction = null; // EndTransaction() will always Commit()
					}
					EndTransaction(transaction, commitTransaction);

					return retval;
				}
			}

			static TResult UseIdWithOpenCloseFirst<T, TResult>(ObjectId id, OpenMode openMode, Func<T, TResult> f) where T : DBObject
			{
				// If we've got a transaction, we really should be using that--otherwise, what's the point
				// of having an open transaction?
				//
				// Well, except that doesn't work very well with multi-threading.  If thread A has
				// created the transaction and then thread B tries to use it, AutoCAD will gack.
				// Using Open/Close has it's own set of problems (such asForRead followed by ForWrite),
				// but perhaps they will be easier to work-around.
				try
				{
					return UseStartOpenCloseTransaction(id, openMode, f);
				}
				catch (Autodesk.AutoCAD.Runtime.Exception/* ex*/)
				{
					//if (ex.ErrorStatus != Autodesk.AutoCAD.Runtime.ErrorStatus.PermanentlyErased)
					//    throw;
				}

				// Try to use an already open transaction rather than creating a new one...only to end it moments later.
				//
				// Trying to create a transaction while another one isn't allowed, see http://jira.autodesk.com/browse/FDS-1439
				// Part of the fix is to avoid creating transactions when possible; this reduces opportunities for Commit() (or Abort())
				// which can cause reentrancy-type problems (AutoCAD tries to update graphics which causes us to open
				// objects which creates a transaction...)
				Transaction topTransaction = id.Database.TransactionManager.TopTransaction;
				if ((topTransaction != null) && (!topTransaction.IsDisposed))
					try
					{
						return GetObject(id, openMode, f, topTransaction);
					}
					catch (Autodesk.AutoCAD.Runtime.Exception ex)
					{
						// There is no way to determine whether the transaction is BEING disposed; see AutoCAD defect 1381839
						if (ex.ErrorStatus != Autodesk.AutoCAD.Runtime.ErrorStatus.InvalidContext)
							throw;

						//if (ex.ErrorStatus != Autodesk.AutoCAD.Runtime.ErrorStatus.PermanentlyErased)
						//    throw;
					}

				//
				// Open/Close already failed, no need to try it again
				//
				// Can't open the object directly (unlikely?), so create a new normal transaction
				return UseStartTransaction(id, openMode, f); // it seems we never get here...OpenClose always works?
			}

			static TResult UseIdWithTopFirst<T, TResult>(ObjectId id, OpenMode openMode, Func<T, TResult> f) where T : DBObject
			{
				//// Newly created objects that are in a transaction that hasn't yet been Committed()
				//// can't be opened with an OpenCloseTransaction.  These are "effectively erased" 
				//// because the ownership chain has not yet been fully established.
				//if (!id.IsEffectivelyErased)
				//    return UseStartOpenClose(id, openMode, f);
				//return UseStart(id, openMode, f);

				// If we've got a transaction, we really should be using that--otherwise, what's the point
				// of having an open transaction?
				//
				// Try to use an already open transaction rather than creating a new one...only to end it moments later.
				//
				// Trying to create a transaction while another one isn't allowed, see http://jira.autodesk.com/browse/FDS-1439
				// Part of the fix is to avoid creating transactions when possible; this reduces opportunities for Commit() (or Abort())
				// which can cause reentrancy-type problems (AutoCAD tries to update graphics which causes us to open
				// objects which creates a transaction...)
                //Transaction topTransaction = id.Database.TransactionManager.TopTransaction;
                //if ((topTransaction != null) && (!topTransaction.IsDisposed))
                //    try
                //    {
                //        return GetObject(id, openMode, f, topTransaction);
                //    }
                //    catch (Autodesk.AutoCAD.Runtime.Exception ex)
                //    {
                //        // There is no way to determine whether the transaction is BEING disposed; see AutoCAD defect 1381839
                //        if (ex.ErrorStatus != Autodesk.AutoCAD.Runtime.ErrorStatus.InvalidContext)
                //            throw;

                //        //if (ex.ErrorStatus != Autodesk.AutoCAD.Runtime.ErrorStatus.PermanentlyErased)
                //        //    throw;
                //    }

                try
                {
				    return UseStartOpenCloseTransaction(id, openMode, f);
                }
                catch (Autodesk.AutoCAD.Runtime.Exception/* ex*/)
                {
                    //if (ex.ErrorStatus != Autodesk.AutoCAD.Runtime.ErrorStatus.PermanentlyErased)
                    //    throw;
                }

				// Can't open the object directly (unlikely?), so create a new normal transaction
				return UseStartTransaction(id, openMode, f); // it seems we never get here...OpenClose always works?
			}

			internal static TResult UseId<T, TResult>(ObjectId id, OpenMode openMode, Func<T, TResult> f) where T : DBObject
			{
				// Each of these techniques has various problems in corner cases (see comments above); go with
				// OpenClose first for now because it works better with multi-threading in ProcessSimulation.
                return UseIdWithTopFirst(id, openMode, f);
                //return UseIdWithOpenCloseFirst(id, openMode, f);
			}
		}

		/// <summary>
		/// Open the given objectId ForRead inside of a new transaction and then call the specified function;
		/// the transaction will be Commit()ted.
		/// </summary>
		public static TResult Use<TDBObject, TResult>(this ObjectId<TDBObject> id, Func<TDBObject, TResult> f) where TDBObject : DBObject
		{
            lock (Runtime.DBSyncObject)
            {
                return TransactionUtilities.UseId(id, OpenMode.ForRead, f);
            }
		}

		// convenience wrapper for ObjectId<TDBObject>?; callers should already have checked Nullable<>.HasValue
		public static TResult Use<TDBObject, TResult>(this ObjectId<TDBObject>? id, Func<TDBObject, TResult> f) where TDBObject : DBObject
		{
			Contract.Requires(id.HasValue);
			return Use(id.Value, f);
		}

		/// <summary>
		/// Open the given objectId ForWrite inside of a new transaction and then call the specified function;
		/// the transaction will be Commit()ted.
		/// </summary>
		public static TResult UseForWrite<TDBObject, TResult>(this ObjectId<TDBObject> id, Func<TDBObject, TResult> f) where TDBObject : DBObject
		{
			return TransactionUtilities.UseId(id, OpenMode.ForWrite, f);
		}

		/// <summary>
		/// Open the given objectId ForWrite inside of a new transaction and then call the specified function;
		/// the transaction will be Commit()ted.
		/// </summary>
		public static void UseForWrite<TDBObject>(this ObjectId<TDBObject> id, Action<TDBObject> f) where TDBObject : DBObject
		{
			UseForWrite(id, (TDBObject t) => // turn Action<> into Func<>
			{
				f(t);
				return IntPtr.Zero;
			});
		}

		/// <summary>
		/// Check whether an DBObject with the given object id is of the given type
		/// </summary>
		public static bool IsDerivedFrom(this ObjectId objectId, Type type)
		{
			// Even without opening the DBObject with its object id, we can know its type.
			return objectId.ObjectClass.IsDerivedFrom(RXClass.GetClass(type));
		}
	}
}
