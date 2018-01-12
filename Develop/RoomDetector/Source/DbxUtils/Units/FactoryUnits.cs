using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Serialization;
using System.Xml.XPath;
using DbxUtils.Properties;

namespace DbxUtils.Units
{
	/// <summary>
	/// Simple metric/english units specifier.
	/// </summary>
	public enum UnitType
	{
		Imperial,
		Metric,
		Unknown
	}

	/// <summary>
	/// Store a value and unit together in a single class.
	/// 
	/// Ideally, this might be an immutable class (at least TUnit), but that makes it more difficult
	/// to use directly with XML Serialization.  Also, this class is mostly used by UI code where
	/// it makes sense to change the value and unit separately.
	/// </summary>
	[Serializable]
	public abstract class ValueWithUnit<TValue, TUnit> where TValue: struct where TUnit : struct
	{
		protected ValueWithUnit()
		{
			// for XML Serialization
		}

		protected ValueWithUnit(TValue value, TUnit unit)
		{
			Value = value;
			Unit = unit;
		}

		[XmlText] // "<SetupTime Unit="Minute">10</SetupTime>" instead of "...<Value>10</Value>..."
		public TValue Value { get; set; }

		[XmlAttribute]
		public TUnit Unit { get; set; }

        #region unit test support
        public override bool Equals(object other)
        {
            ValueWithUnit<TValue, TUnit> otherThis = (ValueWithUnit<TValue, TUnit>)other;
            if (otherThis == null)
                return false;

            if (!this.Value.Equals(otherThis.Value))
                return false;
            if (!this.Unit.Equals(otherThis.Unit))
                return false;

            return true;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
        #endregion
    }

	/// <summary>
	/// Identifies the various unit sets supported
	/// </summary>
	[Obfuscation]
	public enum UnitsType
	{
        Unknown,

		CostRate,
		EnergyConsumptionRate,
		Time,
		Speed,
		Length,
        Angularity,
        Unitless,
        Volume
	}

	[Obfuscation]
	public enum CostRateUnit
	{
		PerDay,
		PerHour,
		PerMinute,
		PerSecond
	};

	[Serializable]
	public class CostRate : ValueWithUnit<double, CostRateUnit>
	{
		public CostRate()
		{
			// for XML Serialization
		}

		public CostRate(double value, CostRateUnit unit)
			: base(value, unit)
		{
		}

		public static CostRateUnit InternalUnit
		{
			get { return (CostRateUnit)FactoryUnits.GetInternalUnitsValue(UnitsType.CostRate); }
		}
    }


	public enum EnergyConsumptionRateUnit
	{
		kW,
		JoulePerHour,
		JoulePerMin,
		JoulePerSecond,
	}

	[Serializable]
	public class EnergyConsumptionRate : ValueWithUnit<double, EnergyConsumptionRateUnit>
	{
		public EnergyConsumptionRate()
		{
			// for XML Serialization
		}

		public EnergyConsumptionRate(double value, EnergyConsumptionRateUnit unit)
			: base(value, unit)
		{
		}

		public static EnergyConsumptionRateUnit InternalUnit
		{
			get { return (EnergyConsumptionRateUnit)FactoryUnits.GetInternalUnitsValue(UnitsType.EnergyConsumptionRate); }
		}
    }

	public enum TimeUnit
	{
		Minute,
		Hour,
		Day,
		Second
	};

	[Serializable]
	public class Time : ValueWithUnit<double, TimeUnit>
	{
		public Time()
		{
			// for XML Serialization
		}

		public Time(double value, TimeUnit unit)
			: base(value, unit)
		{
		}

		public static TimeUnit InternalUnit
		{
			get { return (TimeUnit)FactoryUnits.GetInternalUnitsValue(UnitsType.Time); }
		}
    }

	public enum SpeedUnit
	{
		FeetPerHour,
		FeetPerMin,
		FeetPerSecond,

		InchPerHour,
		InchPerMin,
		InchPerSecond,

		CentimeterPerHour,
		CentimeterPerMin,
		CentimeterPerSecond,

		MeterPerHour,
		MeterPerMin,
		MeterPerSecond
	};

	[Serializable]
	public class Speed : ValueWithUnit<double, SpeedUnit>
	{
		public Speed()
		{
			// for XML Serialization
		}

		public Speed(double value, SpeedUnit unit)
			: base(value, unit)
		{
		}

		public static SpeedUnit InternalUnit
		{
			get { return (SpeedUnit)FactoryUnits.GetInternalUnitsValue(UnitsType.Speed); }
		}
	}

	public enum LengthUnit
	{
		Millimeters,
		Centimeters,
		Meters,
		Kilometers,
		
		Inches,
		Feet,
		Yards,
		Miles,
        Micron,
        Decimeters,
        Unknown,
	};

	[Serializable]
	public class Length : ValueWithUnit<double, LengthUnit>
	{
		public Length()
		{
			// for XML Serialization
		}

		public Length(double value, LengthUnit unit)
			: base(value, unit)
		{
		}

		public static LengthUnit InternalUnit
		{
			get { return (LengthUnit)FactoryUnits.GetInternalUnitsValue(UnitsType.Length); }
		}
	}

    public enum AngularityUnit
    {
        Radians,
        Degree,
        Grad,
        Steradian,
        Unknown
    };

    [Serializable]
    public class Angularity : ValueWithUnit<double, AngularityUnit>
    {
        public Angularity()
        {
            // for XML Serialization
        }

        public Angularity(double value, AngularityUnit unit)
            : base(value, unit)
        {
        }

        public static AngularityUnit InternalUnit
        {
            get { return (AngularityUnit)FactoryUnits.GetInternalUnitsValue(UnitsType.Angularity); }
        }
    }

    public enum UnitlessUnit
    {
        Unitless
    }

    [Serializable]
    public class Unitless : ValueWithUnit<double, UnitlessUnit>
    {
        public Unitless()
        {
            // for XML Serialization
        }

        public Unitless(double value, UnitlessUnit unit)
            : base(value, unit)
        {
        }

        public static UnitlessUnit InternalUnit
        {
            get { return (UnitlessUnit)FactoryUnits.GetInternalUnitsValue(UnitsType.Unitless); }
        }
    }

    public enum VolumeUnit
    {
        CubicInches,
        CubicFeet,
        CubicYards,
        CubicMeters,
        CubicDecimeters,
        CubicCentimeters,
        CubicMillimeters
     }

    [Serializable]
    public class Volume : ValueWithUnit<double, VolumeUnit>
    {
        public Volume()
        {
            // for XML Serialization
        }

        public Volume(double value, VolumeUnit unit)
            : base(value, unit)
        {
        }

        public static VolumeUnit InternalUnit
        {
            get { return (VolumeUnit)FactoryUnits.GetInternalUnitsValue(UnitsType.Volume); }
        }
    }

	public static class FactoryUnits
	{
		/// <summary>
		/// Instances of this type are held in sUnitsData below,
		/// and they define the value (really an enum) and the relevant long and/or short display strings,
		/// as read from resources.
		/// </summary>
		class UnitsData
		{
			public int Value { get; internal set; }              // This will correspond to a xxxUnit enum value
			public string DisplayString { get; internal set; }
			public string ShortDisplayString { get; internal set; }
		}

		/// <summary>
		/// Provides information about the units data for a particular units type (e.g., length)
		/// </summary>
		class UnitsTypeData
		{
			UnitsType m_unitsType; // Not really needed -- held for debugging
			internal UnitsTypeData(UnitsType unitsType)
			{
				m_unitsType = unitsType;
				UnitsData = new List<UnitsData>();
			}

			public IList<UnitsData> UnitsData { get; private set; }
			public int InternalUnits { get; internal set; }
		}

		static Dictionary<UnitsType, UnitsTypeData> sUnitsTypeData;

		static Dictionary<UnitsType, UnitsTypeData> GetUnitsData()
		{
			if (sUnitsTypeData == null)
			{
				InitializeUnitsData();
			}
			return sUnitsTypeData;
		}

		/// <summary>
		/// We define the various unit strings (long and short), as well as the default (internal) units
		/// for each unit type in a resource-based XML file (FactoryUnits.xml).
		/// This method loads the above lists from that file, for use in the methods that follow.
		/// </summary>
		static void InitializeUnitsData()
		{
            const string FactoryUnitsXmlResource = "DbxUtils.Units.FactoryUnits.xml";

			sUnitsTypeData = new Dictionary<UnitsType, UnitsTypeData>();
	
			Assembly thisAssembly = Assembly.GetExecutingAssembly();
			using (Stream unitsDataStream = thisAssembly.GetManifestResourceStream(FactoryUnitsXmlResource))
			{
				XPathDocument unitsDoc = new XPathDocument(unitsDataStream);
				XPathNavigator nav = unitsDoc.CreateNavigator();

				// grab data for each known unit type (i.e., UnitsType enum values)
				foreach (UnitsType unitType in Enum.GetValues(typeof(UnitsType)))
				{
					// Skip Unknown
					if (unitType == UnitsType.Unknown) continue;

					string unitTypeName = Enum.GetName(typeof(UnitsType), unitType);
					Type unitTypeEnum = thisAssembly.GetType("Autodesk.Factory.Core.Utilities." + unitTypeName + "Unit");

					var subNav = nav.SelectSingleNode("/FactoryUnits/UnitGroup[@type='" + unitTypeName + "']");
					var unitsTypeData = new UnitsTypeData(unitType);

					string internalUnit = (string)subNav.Evaluate("string(@default)");
					object internalUnitValue = Enum.Parse(unitTypeEnum, internalUnit);
					unitsTypeData.InternalUnits = (int)internalUnitValue;

					// "default" attribute identifies internal unit for the group
					// "Unit" children provide type/display string mappings
					var unitGroupItems = subNav.Select("Unit");
					while (unitGroupItems.MoveNext())
					{
						var unitNode = unitGroupItems.Current;

						// "type" attribute identifies the particular xxxUnit enum value (as a string)
						// "res" attribute identifies the resource string
						string resId = (string)unitNode.Evaluate("string(@res)");
						string shortResId = (string)unitNode.Evaluate("string(@shortRes)");
						string typeName = (string)unitNode.Evaluate("string(@type)");

						string displayString = Resources.ResourceManager.GetString(resId);
						string shortDisplayString = String.IsNullOrEmpty(shortResId) ? String.Empty : Resources.ResourceManager.GetString(shortResId);

						object typeValue = Enum.Parse(unitTypeEnum, typeName);
						unitsTypeData.UnitsData.Add(new UnitsData() { Value = (int)typeValue, DisplayString = displayString, ShortDisplayString = shortDisplayString });
					}

					sUnitsTypeData[unitType] = unitsTypeData;
				}
			}
		}

		/// <summary>
		/// Can be used for unit-testing of units data set up in InitializeUnitsData
		/// </summary>
		[Conditional("DEBUG")]
		static void UnitsDataTest()
		{
			Debug.Print("CostRateUnitStringCollection:");
			foreach (var s in CostRateUnitStringCollection)
				Debug.Print("  " + s);
			Debug.Print("SpeedUnitStringCollection:");
			foreach (var s in SpeedUnitStringCollection)
				Debug.Print("  " + s);
			Debug.Print("TimeUnitStringCollection:");
			foreach (var s in TimeUnitStringCollection)
				Debug.Print("  " + s);
			Debug.Print("EnergyConsumptionRateUnitStringCollection:");
			foreach (var s in EnergyConsumptionRateUnitStringCollection)
				Debug.Print("  " + s);
			Debug.Print("LengthUnitStrings:");
			foreach (var s in LengthUnitStrings)
				Debug.Print("  " + s);
			Debug.Print("LengthUnitCollection:");
			foreach (var s in LengthUnitCollection)
				Debug.Print("  " + s.ToString());
            Debug.Print("AngularityUnitStrings:");
            foreach (var s in AngularityUnitStrings)
                Debug.Print("  " + s);
            Debug.Print("AngularityUnitCollection:");
            foreach (var s in AngularityUnitCollection)
                Debug.Print("  " + s.ToString());
            foreach (var s in UnitlessUnitStrings)
                Debug.Print("  " + s);
            Debug.Print("UnitlessUnitCollection:");
            foreach (var s in UnitlessUnitCollection)
                Debug.Print("  " + s.ToString());
            Debug.Print("VolumeUnitCollection:");
            foreach (var s in VolumeUnitCollection)
                Debug.Print("  " + s.ToString());

			Debug.Print("kW = {0}", GetEnergyConsumptionRateUnitEnum("kW"));
			Debug.Print("/Work Day = {0}", GetCostRateUnitEnum("/Work Day"));
			Debug.Print("cm/sec = {0}", GetSpeedUnitEnum("cm/sec"));
			Debug.Print("hr = {0}", GetTimeUnitEnum("hr"));
			Debug.Print("Feet = {0}", GetLengthUnitEnum("Feet"));
			Debug.Print("ft = {0}", GetLengthUnitByShortName("ft"));
            Debug.Print("rad = {0}", GetAngularityUnitByShortName("rad"));
            Debug.Print("ul = {0}", GetUnitlessUnitByShortName("ul"));
            Debug.Print("cubic inches = {0}", GetVolumeUnitEnum("Cubic Inches"));
            Debug.Print("cu in = {0}", GetVolumeUnitByShortName("cu in"));

			Debug.Print("/sec = {0}", GetUnitsType("/Second"));
			Debug.Print("min = {0}", GetUnitsType("min"));
			Debug.Print("ft/sec = {0}", GetUnitsType("ft/sec"));
            Debug.Print("Cubic Inches = {0}", GetUnitsType("Cubic Inches"));

			Debug.Print("Internal units:");
			foreach (UnitsType unitType in Enum.GetValues(typeof(UnitsType)))
			{
				if (unitType == UnitsType.Unknown) continue; // Skip Unknown
				Debug.Print("  {0} = {1}", unitType, GetInternalUnitsValue(unitType));
			}
		}

		public static IEnumerable<string> GetUnitStrings(UnitsType type)
		{
			return GetUnitsData()[type].UnitsData.Select(data => data.DisplayString);
		}

        public static IEnumerable<string> GetUnitStringsInShort(UnitsType type)
        {
			return GetUnitsData()[type].UnitsData.Select(data => data.ShortDisplayString);
        }

		public static IEnumerable<TUnit> GetUnitValues<TUnit>(UnitsType type)
		{
			return GetUnitsData()[type].UnitsData.Select(data => data.Value).Cast<TUnit>();
		}

		public static int GetUnitStringValue(UnitsType type, string unitString)
		{
			return GetUnitsData()[type].UnitsData.Single(data => data.DisplayString == unitString).Value;
		}

        public static string GetUnitStringInShort(UnitsType type, string unitString)
        {
			return GetUnitsData()[type].UnitsData.Single(data => data.DisplayString == unitString).ShortDisplayString;
        }

		public static int GetShortUnitStringValue(UnitsType type, string unitString)
		{
			return GetUnitsData()[type].UnitsData.Single(data => data.ShortDisplayString == unitString).Value;
		}

		public static IList<string> CostRateUnitStringCollection
		{
			get { return GetUnitStrings(UnitsType.CostRate).ToList(); }
		}

		public static IList<string> SpeedUnitStringCollection
		{
			get { return GetUnitStrings(UnitsType.Speed).ToList(); }
		}

		public static IList<string> TimeUnitStringCollection
		{
			get { return GetUnitStrings(UnitsType.Time).ToList(); }
		}

		public static IList<string> EnergyConsumptionRateUnitStringCollection
		{
			get { return GetUnitStrings(UnitsType.EnergyConsumptionRate).ToList(); }
		}

        public static IList<string> VolumeUnitStringCollection
        {
            get { return GetUnitStrings(UnitsType.Volume).ToList(); }
        }

        public static IList<string> VolumeUnitStringInShortCollection
        {
            get { return GetUnitStringsInShort(UnitsType.Volume).ToList(); }
        }

		public static IEnumerable<string> LengthUnitStrings
		{
			get { return GetUnitStrings(UnitsType.Length); }
		}

		public static IEnumerable<LengthUnit> LengthUnitCollection
		{
			get { return GetUnitValues<LengthUnit>(UnitsType.Length); }
		}

        public static IEnumerable<string> AngularityUnitStrings
        {
            get { return GetUnitStrings(UnitsType.Angularity); }
        }

        public static IEnumerable<AngularityUnit> AngularityUnitCollection
        {
            get { return GetUnitValues<AngularityUnit>(UnitsType.Angularity); }
        }

        public static IEnumerable<string> UnitlessUnitStrings
        {
            get { return GetUnitStrings(UnitsType.Unitless); }
        }

        public static IEnumerable<UnitlessUnit> UnitlessUnitCollection
        {
            get { return GetUnitValues<UnitlessUnit>(UnitsType.Unitless); }
        }

        public static IEnumerable<VolumeUnit> VolumeUnitCollection
        {
            get { return GetUnitValues<VolumeUnit>(UnitsType.Volume); }
        }
        public static IEnumerable<string> VolumeUnitStrings
        {
            get { return GetUnitStrings(UnitsType.Volume); }
        }

		// TODO: Maybe these should be done with lookup dictionaries instead

		public static EnergyConsumptionRateUnit GetEnergyConsumptionRateUnitEnum(string consumptionRateUnit)
		{
			try {
				return (EnergyConsumptionRateUnit)GetUnitStringValue(UnitsType.EnergyConsumptionRate, consumptionRateUnit);
			}
			catch (InvalidOperationException ioe)
			{
				throw new ArgumentOutOfRangeException(/*MSG0*/"consumptionRateUnit", ioe);
			}
		}

		public static CostRateUnit GetCostRateUnitEnum(string costRateUnit)
		{
			try
			{
				return (CostRateUnit)GetUnitStringValue(UnitsType.CostRate, costRateUnit);
			}
			catch (InvalidOperationException ioe)
			{
				throw new ArgumentOutOfRangeException(/*MSG0*/"costRateUnit", ioe);
			}
		}
		
		public static SpeedUnit GetSpeedUnitEnum(string speedUnit)
		{
			try
			{
				return (SpeedUnit)GetUnitStringValue(UnitsType.Speed, speedUnit);
			}
			catch (InvalidOperationException ioe)
			{
				throw new ArgumentOutOfRangeException(/*MSG0*/"speedUnit", ioe);
			}
		}

		public static TimeUnit GetTimeUnitEnum(string timeUnit)
		{
			try
			{
				return (TimeUnit)GetUnitStringValue(UnitsType.Time, timeUnit);
			}
			catch (InvalidOperationException ioe)
			{
				throw new ArgumentOutOfRangeException(/*MSG0*/"timeUnit", ioe);
			}
		}

		public static LengthUnit GetLengthUnitEnum(string lengthUnit)
		{
			try
			{
				return (LengthUnit)GetUnitStringValue(UnitsType.Length, lengthUnit);
			}
			catch (InvalidOperationException ioe)
			{
				throw new ArgumentOutOfRangeException(/*MSG0*/"lengthUnit", ioe);
			}
		}

		public static LengthUnit GetLengthUnitByShortName(string lengthUnitShortName)
		{
			try
			{
				return (LengthUnit)GetShortUnitStringValue(UnitsType.Length, lengthUnitShortName);
			}
			catch (InvalidOperationException ioe)
			{
				throw new ArgumentOutOfRangeException(/*MSG0*/"lengthUnitShortName", ioe);
			}
		}

        public static AngularityUnit GetAngularityUnitEnum(string angularityUnit)
        {
            try
            {
                return (AngularityUnit)GetUnitStringValue(UnitsType.Angularity, angularityUnit);
            }
            catch (InvalidOperationException ioe)
            {
                throw new ArgumentOutOfRangeException(/*MSG0*/"angularityUnit", ioe);
            }
        }

        public static AngularityUnit GetAngularityUnitByShortName(string angularityUnitShortName)
        {
            try
            {
                return (AngularityUnit)GetShortUnitStringValue(UnitsType.Angularity, angularityUnitShortName);
            }
            catch (InvalidOperationException ioe)
            {
                throw new ArgumentOutOfRangeException(/*MSG0*/"angularityUnitShortName", ioe);
            }
        }

        public static UnitlessUnit GetUnitlessUnitEnum(string unitlessUnit)
        {
            try
            {
                return (UnitlessUnit)GetUnitStringValue(UnitsType.Unitless, unitlessUnit);
            }
            catch (InvalidOperationException ioe)
            {
                throw new ArgumentOutOfRangeException(/*MSG0*/"unitlessUnit", ioe);
            }
        }

        public static UnitlessUnit GetUnitlessUnitByShortName(string unitlessUnitShortName)
        {
            try
            {
                return (UnitlessUnit)GetShortUnitStringValue(UnitsType.Unitless, unitlessUnitShortName);
            }
            catch (InvalidOperationException ioe)
            {
                throw new ArgumentOutOfRangeException(/*MSG0*/"unitlessUnitShortName", ioe);
            }
        }

        public static VolumeUnit GetVolumeUnitEnum(string volumeUnit)
        {
            try
            {
                return (VolumeUnit)GetUnitStringValue(UnitsType.Volume, volumeUnit);
            }
            catch (InvalidOperationException ioe)
            {
                throw new ArgumentOutOfRangeException(/*MSG0*/"volumeUnit", ioe);
            }
        }

        public static VolumeUnit GetVolumeUnitByShortName(string volumeUnitShortName)
        {
            try
            {
                return (VolumeUnit)GetShortUnitStringValue(UnitsType.Volume, volumeUnitShortName);
            }
            catch (InvalidOperationException ioe)
            {
                throw new ArgumentOutOfRangeException(/*MSG0*/"volumeUnitShortName", ioe);
            }
        }

		/// <summary>
		/// Checks whether specified string is valid for given unit type
		/// </summary>
		/// <param name="type"></param>
		/// <param name="unitString"></param>
		/// <returns></returns>
		static bool IsUnits(UnitsType type, string unitString)
		{
			return GetUnitsData()[type].UnitsData.Any(data => data.DisplayString == unitString);
		}
		
		/// <summary>
		/// Checks whether both specified strings are valid for given unit type
		/// </summary>
		/// <param name="type"></param>
		/// <param name="unitString1"></param>
		/// <param name="unitString2"></param>
		/// <returns></returns>
		static bool IsUnits(UnitsType type, string unitString1, string unitString2)
		{
			return IsUnits(type, unitString1) && IsUnits(type, unitString2);
		}
		

		public static bool IsSpeedUnits(string unit1)
		{
			return IsUnits(UnitsType.Speed, unit1);
		}

		public static bool IsSpeedUnits(string unit1, string unit2)
		{
			return IsUnits(UnitsType.Speed, unit1, unit2);
		}

		public static bool IsTimeUnits(string unit1)
		{
			return IsUnits(UnitsType.Time, unit1);
		}

		public static bool IsTimeUnits(string unit1, string unit2)
		{
			return IsUnits(UnitsType.Time, unit1, unit2);
		}

		public static bool IsEnergyConsumptionRateUnits(string unit1)
		{
			return IsUnits(UnitsType.EnergyConsumptionRate, unit1);
		}

		public static bool IsEnergyConsumptionRateUnits(string unit1, string unit2)
		{
			return IsUnits(UnitsType.EnergyConsumptionRate, unit1, unit2);
		}

		public static bool IsCostRateUnits(string unit1)
		{
			return IsUnits(UnitsType.CostRate, unit1);
		}

		public static bool IsCostRateUnits(string unit1, string unit2)
		{
			return IsUnits(UnitsType.CostRate, unit1, unit2);
		}

		public static bool IsLengthUnit(string unit1)
		{
			return IsUnits(UnitsType.Length, unit1);
		}

		public static bool IsLengthUnits(string unit1, string unit2)
		{
			return IsUnits(UnitsType.Length, unit1, unit2);
		}

        public static bool IsAngularityUnit(string unit1)
        {
            return IsUnits(UnitsType.Angularity, unit1);
        }

        public static bool IsAngularityUnits(string unit1, string unit2)
        {
            return IsUnits(UnitsType.Angularity, unit1, unit2);
        }

        public static bool IsUnitlessUnit(string unit1)
        {
            return IsUnits(UnitsType.Unitless, unit1);
        }

        public static bool IsVolumeUnit(string unit1)
        {
            return IsUnits(UnitsType.Volume, unit1);

        }
        public static bool IsUnitlessUnits(string unit1, string unit2)
        {
            return IsUnits(UnitsType.Unitless, unit1, unit2);
        }

        public static bool IsVolumeUnits(string unit1, string unit2)
        {
            return IsUnits(UnitsType.Volume, unit1, unit2);
        }

		/// <summary>
		/// Get the UnitsType corresponding to the provided string
		/// </summary>
		/// <param name="unitsString"></param>
		/// <returns></returns>
		public static UnitsType GetUnitsType(string unitsString)
		{
            if (FactoryUnits.IsSpeedUnits(unitsString))
                return UnitsType.Speed;
            else if (FactoryUnits.IsTimeUnits(unitsString))
                return UnitsType.Time;
            else if (FactoryUnits.IsCostRateUnits(unitsString))
                return UnitsType.CostRate;
            else if (FactoryUnits.IsEnergyConsumptionRateUnits(unitsString))
                return UnitsType.EnergyConsumptionRate;
            else if (FactoryUnits.IsLengthUnit(unitsString))
                return UnitsType.Length;
            else if (FactoryUnits.IsAngularityUnit(unitsString))
                return UnitsType.Angularity;
            else if (FactoryUnits.IsUnitlessUnit(unitsString))
                return UnitsType.Unitless;
            else if (FactoryUnits.IsVolumeUnit(unitsString))
                return UnitsType.Volume;

			throw new ArgumentException(String.Format(CultureInfo.CurrentCulture, Resources.UnrecognizedUnits, unitsString));
		}

        /// <summary>
        /// Get a member of UnitsType enum from its string
        /// </summary>
        /// <param name="typeString"></param>
        /// <returns></returns>
        public static UnitsType GetUnitsTypeFromTypeString(string typeString)
        {
            return (UnitsType)Enum.Parse(typeof(UnitsType), typeString, true);
        }

		/// <summary>
		/// Get the internal units string for the provided units string
		/// </summary>
		/// <param name="unitsString"></param>
		/// <returns></returns>
		public static string GetInternalUnits(string unitsString)
		{
			return GetInternalUnits(GetUnitsType(unitsString));
		}

		public static int GetInternalUnitsValue(UnitsType unitType)
		{
			try
			{
				return GetUnitsData()[unitType].InternalUnits;
			}
			catch (InvalidOperationException)
			{
				throw new ArgumentOutOfRangeException("unitType");
			}
		}

        //Now 3 types are supported: Length, Angularity and Unitless
        public static string GetInventorInternalUnitString(UnitsType unitType)
        {
            switch (unitType)
            {
                case UnitsType.Length:
                    return /*MSG0*/"cm";
                case UnitsType.Angularity:
                    return /*MSG0*/"rad";
                case UnitsType.Unitless:
                    return String.Empty;

                default:
                    {
                        Debug.Assert(false);
                        return String.Empty;
                    }
            }
        }

        //Now 3 types are supported: Length, Angularity and Unitless
        public static string GetInventorInternalFullUnitString(UnitsType unitType)
        {
            switch (unitType)
            {
                case UnitsType.Length:
                    return Resources.Units_Centimeters_Long;
                case UnitsType.Angularity:
                    return Resources.Units_Radians_Long;
                case UnitsType.Unitless:
                    return String.Empty;

                default:
                    {
                        Debug.Assert(false);
                        return String.Empty;
                    }
            }
        }

		/// <summary>
		/// Get the internal units string for the provided units type
		/// </summary>
		/// <param name="unitType"></param>
		/// <returns></returns>
		public static string GetInternalUnits(UnitsType unitType)
		{
			try
			{
				UnitsTypeData unitsData = GetUnitsData()[unitType];

				int internalUnitsValue = unitsData.InternalUnits;
				return unitsData.UnitsData.Single(data => data.Value == internalUnitsValue).DisplayString;
			}
			catch (InvalidOperationException)
			{
				throw new ArgumentOutOfRangeException("unitType");
			}
		}

		internal static string GetDisplayString(UnitsType type, int value)
		{
			return GetUnitsData()[type].UnitsData.Single(data => data.Value == value).DisplayString;
		}

		internal static string GetShortDisplayString(UnitsType type, int value)
		{
			return GetUnitsData()[type].UnitsData.Single(data => data.Value == value).ShortDisplayString;
		}
	}

	public static class FactoryUnitExtensions
	{
		public static string ToDisplayString(this EnergyConsumptionRateUnit consumptionRateUnit)
		{
			try
			{
				return FactoryUnits.GetDisplayString(UnitsType.EnergyConsumptionRate, (int)consumptionRateUnit);
			}
			catch (InvalidOperationException)
			{
				throw new ArgumentOutOfRangeException(/*MSG0*/"consumptionRateUnit");
			}
		}

		public static string ToDisplayString(this CostRateUnit costRateUnit)
		{
			try
			{
				return FactoryUnits.GetDisplayString(UnitsType.CostRate, (int)costRateUnit);
			}
			catch (InvalidOperationException)
			{
				throw new ArgumentOutOfRangeException(/*MSG0*/"costRateUnit");
			}
		}

        public static string ToDisplayStringInShort(this CostRateUnit costRateUnit)
        {
            try
            {
                return FactoryUnits.GetShortDisplayString(UnitsType.CostRate, (int)costRateUnit);
            }
            catch (InvalidOperationException)
            {
                throw new ArgumentOutOfRangeException(/*MSG0*/"costRateUnit");
            }
        }

		public static string ToDisplayString(this SpeedUnit speedUnit)
		{
			try
			{
				return FactoryUnits.GetDisplayString(UnitsType.Speed, (int)speedUnit);
			}
			catch (InvalidOperationException)
			{
				throw new ArgumentOutOfRangeException(/*MSG0*/"speedUnit");
			}
		}

		public static string ToDisplayString(this TimeUnit timeUnit)
		{
			try
			{
				return FactoryUnits.GetDisplayString(UnitsType.Time, (int)timeUnit);
			}
			catch (InvalidOperationException)
			{
				throw new ArgumentOutOfRangeException(/*MSG0*/"timeUnit");
			}
		}

		public static string ToDisplayString(this LengthUnit lengthUnit)
		{
			try
			{
				return FactoryUnits.GetDisplayString(UnitsType.Length, (int)lengthUnit);
			}
			catch (InvalidOperationException)
			{
				throw new ArgumentOutOfRangeException(/*MSG0*/"lengthUnit");
			}
		}

		public static string ToDisplayStringInShort(this LengthUnit lengthUnit)
		{
			try
			{
				return FactoryUnits.GetShortDisplayString(UnitsType.Length, (int)lengthUnit);
			}
			catch (InvalidOperationException)
			{
				throw new ArgumentOutOfRangeException(/*MSG0*/"lengthUnit");
			}
		}

        public static string ToDisplayString(this AngularityUnit angularityUnit)
        {
            try
            {
                return FactoryUnits.GetDisplayString(UnitsType.Angularity, (int)angularityUnit);
            }
            catch (InvalidOperationException)
            {
                throw new ArgumentOutOfRangeException(/*MSG0*/"angularityUnit");
            }
        }

        public static string ToDisplayStringInShort(this AngularityUnit angularityUnit)
        {
            try
            {
                return FactoryUnits.GetShortDisplayString(UnitsType.Angularity, (int)angularityUnit);
            }
            catch (InvalidOperationException)
            {
                throw new ArgumentOutOfRangeException(/*MSG0*/"angularityUnit");
            }
        }

        public static string ToDisplayString(this UnitlessUnit unitlessUnit)
        {
            try
            {
                return FactoryUnits.GetDisplayString(UnitsType.Unitless, (int)unitlessUnit);
            }
            catch (InvalidOperationException)
            {
                throw new ArgumentOutOfRangeException(/*MSG0*/"unitlessUnit");
            }
        }

        public static string ToDisplayStringInShort(this UnitlessUnit unitlessUnit)
        {
            try
            {
                return FactoryUnits.GetShortDisplayString(UnitsType.Unitless, (int)unitlessUnit);
            }
            catch (InvalidOperationException)
            {
                throw new ArgumentOutOfRangeException(/*MSG0*/"unitlessUnit");
            }
        }

        public static string ToDisplayString(this VolumeUnit volumeUnit)
        {
            try
            {
                return FactoryUnits.GetDisplayString(UnitsType.Volume, (int)volumeUnit);
            }
            catch (InvalidOperationException)
            {
                throw new ArgumentOutOfRangeException(/*MSG0*/"volumeUnit");
            }
        }

        public static string ToDisplayStringInShort(this VolumeUnit volumeUnit)
        {
            try
            {
                return FactoryUnits.GetShortDisplayString(UnitsType.Volume, (int)volumeUnit);
            }
            catch (InvalidOperationException)
            {
                throw new ArgumentOutOfRangeException(/*MSG0*/"volumeUnit");
            }
        }
	}
}
