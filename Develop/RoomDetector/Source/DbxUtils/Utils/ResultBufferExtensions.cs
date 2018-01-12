using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;

namespace DbxUtils.Utils
{
	public static class ResultBufferExtensions
	{
		/// <summary>
		/// Provide AddRange() which List<> has
		/// </summary>
		public static void AddRange(this ResultBuffer resultBuffer, IEnumerable<TypedValue> collection)
		{
			if (resultBuffer == null) throw new ArgumentNullException(/*MSG0*/"resultBuffer");
			if (collection == null) throw new ArgumentNullException(/*MSG0*/"collection");

			foreach (var item in collection)
				resultBuffer.Add(item);
		}

		/// <summary>
		/// Add the proper TypedValue to the ResultBuffer; this saves the trouble
		/// of casting DfxCode.
		/// 
		/// This is called AddValue() rather than Add() to avoid confusion with ResultBuffer.Add(object) 
		/// </summary>
		public static void AddValue(this ResultBuffer resultBuffer, ObjectId objectId)
		{
			if (resultBuffer == null) throw new ArgumentNullException(/*MSG0*/"resultBuffer");

			resultBuffer.Add(new TypedValue((int)DxfCode.SoftPointerId, objectId));
		}

		/// <summary>
		/// Add the proper TypedValue to the ResultBuffer; this saves the trouble
		/// of casting DfxCode.
		/// 
		/// This is called AddValue() rather than Add() to avoid confusion with ResultBuffer.Add(object) 
		/// </summary>
		public static void AddValue(this ResultBuffer resultBuffer, double value)
		{
			if (resultBuffer == null) throw new ArgumentNullException(/*MSG0*/"resultBuffer");

			resultBuffer.Add(new TypedValue((int)DxfCode.Real, value));
		}

		/// <summary>
		/// Add the proper TypedValue to the ResultBuffer; this saves of the trouble of creating a TypedValue and casting DxfCode.
		/// 
		/// This is called AddValue() rather than Add() to avoid confusion with ResultBuffer.Add(object) 
		/// </summary>
		public static void AddValue(this ResultBuffer resultBuffer, string value, DxfCode code)
		{
			if (resultBuffer == null) throw new ArgumentNullException(/*MSG0*/"resultBuffer");
			switch (code)
			{
				case DxfCode.Text:
				case DxfCode.ExtendedDataRegAppName:
				case DxfCode.ExtendedDataAsciiString: break;
				default: throw new ArgumentOutOfRangeException(/*MSG0*/"code");
			}

			resultBuffer.Add(new TypedValue((int)code, value));
		}

		/// <summary>
		/// Add a string as type DxfCode.Text to a result buffer
		/// 
		/// This is called AddValue() rather than Add() to avoid confusion with ResultBuffer.Add(object) 
		/// </summary>
		public static void AddValue(this ResultBuffer resultBuffer, string value)
		{
			resultBuffer.AddValue(value, DxfCode.Text);
		}

		/// <summary>
		/// Add a System.Guid (as a string) to a result buffer
		/// 
		/// This is called AddValue() rather than Add() to avoid confusion with ResultBuffer.Add(object) 
		/// </summary>
		public static void AddValue(this ResultBuffer resultBuffer, Guid value)
		{
			resultBuffer.AddValue(value.ToString());
		}

		/// <summary>
		/// Call the ResultBuffer.AddValue() extension for each item in the enumeration.
		/// 
		/// This saves of the trouble of creating a TypedValue and casting DxfCode.
		/// </summary>
		public static void AddValues(this ResultBuffer resultBuffer, IEnumerable<ObjectId> collection)
		{
			if (resultBuffer == null) throw new ArgumentNullException(/*MSG0*/"resultBuffer");
			if (collection == null) throw new ArgumentNullException(/*MSG0*/"collection");

			foreach (var item in collection)
				resultBuffer.AddValue(item);
		}
	}
}
