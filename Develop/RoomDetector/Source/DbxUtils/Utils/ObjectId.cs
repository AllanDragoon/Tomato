using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Diagnostics.Contracts;

using Autodesk.AutoCAD.DatabaseServices;

namespace DbxUtils.Utils
{
	/// <summary>
	/// Provide a type-safe wrapper around ObjectId; somewhat similar to the C++ "smart pointer" classes provided
	/// by ObjectARX.
	///
	/// This makes is easier to call ObjectId.GetObject() as it's no longer necessary to cast the DBObject to the desired type.
	/// For compatibility with the (untyped) ObjectId, there are several pass-through methods.
	/// </summary>
	public struct ObjectId<TDBObject> : IComparable<ObjectId<TDBObject>>, IEquatable<ObjectId<TDBObject>> where TDBObject : DBObject
	{
		readonly ObjectId m_id;
		public ObjectId(ObjectId id)
		{
			Contract.Requires(!id.IsNull);
			m_id = id;
		}

		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1000:DoNotDeclareStaticMembersOnGenericTypes", Justification="Compatibility with AutoCAD's ObjectId")]
		public static readonly ObjectId<TDBObject> Null; //new ObjectId<TDBObject>(true /*okToUseNullObjectId*/);

		/// <summary>
		/// Create a strongly-typed ObjectId; unless id.IsNull is "true" in which case "null" is returned.
		/// </summary>
		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1000:DoNotDeclareStaticMembersOnGenericTypes",
			Justification="encourage the use of nullable objects rather than ObjectId.Null")]
		public static ObjectId<TDBObject>? Create(ObjectId id)
		{
			if (id.IsNull) return null;
			return new ObjectId<TDBObject>(id);
		}

		public ObjectId Id
		{
			get { return m_id; }
		}

		public static implicit operator ObjectId(ObjectId<TDBObject> id)
		{
			return id.Id;
		}

		// C++/CLI doesn't like default arguments: 
		//	warning C4564: method '...' of class '...' defines unsupported default parameter '...'
		//	Specify value '...' explicitly when calling the method
		public TDBObject GetObject(OpenMode mode)
		{
			return GetObject(mode, openErased: false);
		}
		public TDBObject GetObject(OpenMode mode, bool openErased)
		{
			return GetObject(mode, openErased, forceOpenOnLockedLayer: false);
		}
		public TDBObject GetObject(OpenMode mode, bool openErased, bool forceOpenOnLockedLayer)
		{
			return (TDBObject)Id.GetObject(mode, openErased, forceOpenOnLockedLayer);
		}

		/// <summary>
		/// Convenient access to GetObject(mode)
		/// </summary>
		public TDBObject this[OpenMode mode]
		{
			get { return GetObject(mode); }
		}

		/// <summary>
		/// Even more convenient access to GetObject(mode) at a slight readability loss.
		/// Instead of id[OpenMode.ForRead], you can use id['r'].
		/// </summary>
		/// <param name="openMode">'r', 'w' or 'n' for the corresponding OpenMode of ForRead, ForWrite or ForNotify</param>
		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1043:UseIntegralOrStringArgumentForIndexers", Justification="0, 1, 2 would be too confusing")]
		public TDBObject this[char openMode]
		{
			get
			{
				switch (openMode)
				{
					case 'r': case 'R': return this[OpenMode.ForRead];
					case 'w': case 'W': return this[OpenMode.ForWrite];
					case 'n': case 'N': return this[OpenMode.ForNotify];

					// Maybe do something clever with other symbols like '>' or '<' or various Unicode characters?
					default: throw new ArgumentOutOfRangeException(/*MSG0*/"openMode");
				}
			}
		}

		public Database Database
		{
			get { return Id.Database; }
		}

		public bool IsErased
		{
			get { return Id.IsErased; }
		}

		public bool IsNull
		{
			get { return Id.IsNull; }
		}

		public bool IsValid
		{
			get { return Id.IsValid; }
		}

		public Handle Handle
		{
			get { return Id.Handle; }
		}

		public Autodesk.AutoCAD.Runtime.RXClass ObjectClass
		{
			get { return Id.ObjectClass; }
		}

		public static bool operator !=(ObjectId<TDBObject> a, ObjectId<TDBObject> b)
		{
			return !(a == b);
		}
		public static bool operator ==(ObjectId<TDBObject> a, ObjectId<TDBObject> b)
		{
			return a.Id == b.Id;
		}

		// These overloads are needed so that we get called when someone writes 
		// "if (id == null)". No, this is not correct (it should be "if (id.IsNull)"), but the compiler 
		// now accepts the code because of an implicit conversion to a nullable type;
		// see http://connect.microsoft.com/VisualStudio/feedback/details/648332/equality-operator-overloading-on-value-type-and-comparing-to-null
		//
		// Without these, no code is generated and we get unexpected results; that is
		// a crash: http://jira.autodesk.com/browse/FDS-3560
		public static bool operator ==(ObjectId<TDBObject> a, ObjectId<TDBObject>? b)
		{
			// normally, we'd just return "false" here since "something" can't equal "nothing"
			// but the intention of "if (id == null)" is really "if (id.IsNull)".
			if (b == null)
				return a.IsNull;

			return a == b.Value;
		}
		public static bool operator !=(ObjectId<TDBObject> a, ObjectId<TDBObject>? b)
		{
			// Normally, we'd just return "true" here since "something" is not equal to "nothing".
			// But the intention of "if (id != null)" is really "if (!id.IsNull)".
			if (b == null)
				return !a.IsNull;

			return a != b.Value;
		}

		public static bool operator <(ObjectId<TDBObject> a, ObjectId<TDBObject> b)
		{
			return a.Id < b.Id;
		}

		public static bool operator >(ObjectId<TDBObject> a, ObjectId<TDBObject> b)
		{
			return a.Id > b.Id;
		}

		#region IComparable<ObjectId<TDBObject>> Members

		public int CompareTo(ObjectId<TDBObject> other)
		{
			return Id.CompareTo(other.Id);
		}

		#endregion

		public override bool Equals(object obj)
		{
			if (obj is ObjectId<TDBObject>)
				return Id.Equals(((ObjectId<TDBObject>)obj).Id);

			if (obj is ObjectId)
				return Id.Equals((ObjectId)obj);

			return false; // "obj" is the wrong type
		}

		#region IEquatable<ObjectId<TDBObject>> Members

		public bool Equals(ObjectId<TDBObject> other)
		{
			return Id.Equals(other.Id);
		}

		#endregion

		public override int GetHashCode()
		{
			return Id.GetHashCode();
		}

		public override string ToString()
		{
			return Id.ToString();
		}
	};
}
