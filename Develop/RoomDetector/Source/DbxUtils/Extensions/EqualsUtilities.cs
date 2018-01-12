using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Autodesk.Factory
{
	/// <summary>
	/// Tools for implementing Equals() operator==(), etc.
	/// </summary>
	public static class EqualsUtilities
	{
		// see http://geekswithblogs.net/podwysocki/archive/2006/06/30/83729.aspx

		/// <summary>
		/// Standard implementation for Equals(object)
		/// </summary>
		public static bool IsEqual<T>(T t, object obj) // IsEqual() to avoid confusion with Object.Equals()
			where T : IEquatable<T>
		{
			if (obj == null) return false; // no operator==() on System.Object

			// It is expected that this is called as IsEqual(this, obj), so no need to check if t is null 
			if (t.GetType() != obj.GetType()) return false; // objects of different types can't be equal

			// Without the constraint, we can't make this call "return t.Equals((T)obj);" because that will call
			// Object.Equals() which gets us back here.  But since T is constrained to IEquatable<T>, it's OK.
			return t.Equals((T)obj); // MUST have IEquatable<T> constraint!
		}

		/// <summary>
		/// Standard implementation for operator==() for objects with generic IEquatable implementations
		/// </summary>
		public static bool OperatorEquals<T>(T left, T right) where T : IEquatable<T>
		{
			bool leftIsNull = (object)left == null; // cast to System.Object to avoid calling <T>.operator==()
			bool rightIsNull = (object)right == null;
			if (leftIsNull && rightIsNull) return true; // null==null
			if (leftIsNull || rightIsNull) return false; // <something> != null, or null != <something>

			// Because <T> implements IEquatable<T>, this calls <T>.Equals() instead of of Object.Equals()
			return left.Equals(right);
		}
	}
}
