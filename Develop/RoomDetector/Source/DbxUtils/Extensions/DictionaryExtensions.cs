using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Autodesk.Factory.Linq
{
	/// <summary>
	/// LINQ-like extension methods for IDictionary
	/// </summary>
	public static class DictionaryExtensions
	{
		/// <summary>
		/// Returns the only element of a sequence, or a default value if the sequence
		/// is empty; this method throws an exception if there is more than one element
		/// in the sequence.
		/// 
		/// This is a combination of SingleOrDefault() and DefaultIfEmpty() optimized for an IDictionary
		/// </summary>
		public static TValue SingleOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> source, TKey key, TValue defaultValue)
		{
			return SingleOrDefault(source, key, () => defaultValue);
		}

		/// <summary>
		/// Returns the only element of a sequence, or a default value if the sequence
		/// is empty; this method throws an exception if there is more than one element
		/// in the sequence.
		/// 
		/// This is a combination of SingleOrDefault() and DefaultIfEmpty() optimized for an IDictionary
		/// </summary>
		public static TValue SingleOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> source, TKey key)
		{
			return SingleOrDefault(source, key, default(TValue));
		}

		/// <summary>
		/// Returns the only element of a sequence, or a default value if the sequence
		/// is empty; this method throws an exception if there is more than one element
		/// in the sequence.
		/// 
		/// This is a combination of SingleOrDefault() and DefaultIfEmpty() optimized for an IDictionary
		/// </summary>
		/// <param name="getDefaultValue">Used to generate a "expensive" default value; can also be used to throw an exception</param>
		public static TValue SingleOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> source, TKey key, Func<TValue> getDefaultValue)
		{
			if (source == null) throw new ArgumentNullException(/*MSG0*/"source");
			if (getDefaultValue == null) throw new ArgumentNullException(/*MSG0*/"getDefaultValue)");
	
			TValue result;
			if (source.TryGetValue(key, out result))
				return result;

			return getDefaultValue();
		}
	}
}
