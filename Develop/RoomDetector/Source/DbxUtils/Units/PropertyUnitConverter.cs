using System;
using System.Collections.Generic;
using System.Globalization;
using Autodesk.Factory.Linq;
using DbxUtils.Properties;

namespace DbxUtils.Units
{
	public static class PropertyUnitConverter
	{
		public static double Convert(string originalValue, string originalUnit, string targetUnit, double hoursPerDay = 24.0, bool useCurrentCulture = true)
		{
            double value = StringToDouble(originalValue, useCurrentCulture);

			if (FactoryUnits.IsSpeedUnits(originalUnit, targetUnit))
			{
				SpeedUnit originalUnitEnum = FactoryUnits.GetSpeedUnitEnum(originalUnit);
				SpeedUnit targetUnitEnum = FactoryUnits.GetSpeedUnitEnum(targetUnit);
				return Convert(value, originalUnitEnum, targetUnitEnum);
			}
			else if (FactoryUnits.IsTimeUnits(originalUnit, targetUnit))
			{
				TimeUnit originalUnitEnum = FactoryUnits.GetTimeUnitEnum(originalUnit);
				TimeUnit targetUnitEnum = FactoryUnits.GetTimeUnitEnum(targetUnit);
				return Convert(value, originalUnitEnum, targetUnitEnum, hoursPerDay);
			}
			else if (FactoryUnits.IsCostRateUnits(originalUnit, targetUnit))
			{
				CostRateUnit originalUnitEnum = FactoryUnits.GetCostRateUnitEnum(originalUnit);
				CostRateUnit targetUnitEnum = FactoryUnits.GetCostRateUnitEnum(targetUnit);
				return Convert(value, originalUnitEnum, targetUnitEnum, hoursPerDay);
			}
			else if (FactoryUnits.IsEnergyConsumptionRateUnits(originalUnit, targetUnit))
			{
				EnergyConsumptionRateUnit originalUnitEnum = FactoryUnits.GetEnergyConsumptionRateUnitEnum(originalUnit);
				EnergyConsumptionRateUnit targetUnitEnum = FactoryUnits.GetEnergyConsumptionRateUnitEnum(targetUnit);
				return Convert(value, originalUnitEnum, targetUnitEnum);
			}
			else if (FactoryUnits.IsLengthUnits(originalUnit, targetUnit))
			{
				LengthUnit originalUnitEnum = FactoryUnits.GetLengthUnitEnum(originalUnit);
				LengthUnit targetUnitEnum = FactoryUnits.GetLengthUnitEnum(targetUnit);
				return Convert(value, originalUnitEnum, targetUnitEnum);
			}
            else if (FactoryUnits.IsAngularityUnits(originalUnit, targetUnit))
            {
                AngularityUnit originalUnitEnum = FactoryUnits.GetAngularityUnitEnum(originalUnit);
                AngularityUnit targetUnitEnum = FactoryUnits.GetAngularityUnitEnum(targetUnit);
                return Convert(value, originalUnitEnum, targetUnitEnum);
            }

			throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture, Resources.UnconvertibleUnits, originalUnit, targetUnit));
		}

		internal static double Convert(string originalValue, SpeedUnit originalUnit, SpeedUnit targetUnit)
		{
			double value = StringToDouble(originalValue);
			return ConvertSpeedUnit(value, originalUnit, targetUnit);
		}

		public static double Convert(double originalValue, SpeedUnit originalUnit, SpeedUnit targetUnit)
		{
			return ConvertSpeedUnit(originalValue, originalUnit, targetUnit);
		}

		public static double Convert(Speed original, SpeedUnit targetUnit)
		{
			return ConvertSpeedUnit(original.Value, original.Unit, targetUnit);
		}

		internal static double Convert(string originalValue, TimeUnit originalUnit, TimeUnit targetUnit, double hoursPerDay)
		{
			double value = StringToDouble(originalValue);
			return ConvertTimeUnit(value, originalUnit, targetUnit, hoursPerDay);
		}

		public static double Convert(double originalValue, TimeUnit originalUnit, TimeUnit targetUnit, double hoursPerDay)
		{
			return ConvertTimeUnit(originalValue, originalUnit, targetUnit, hoursPerDay);
		}

		public static double Convert(Time original, TimeUnit targetUnit, double hoursPerDay)
		{
			return ConvertTimeUnit(original.Value, original.Unit, targetUnit, hoursPerDay);
		}

		public static double Convert(double originalValue, CostRateUnit originalUnit, CostRateUnit targetUnit, double hoursPerDay)
		{
			return ConvertCostRateUnit(originalValue, originalUnit, targetUnit, hoursPerDay);
		}
		public static double Convert(CostRate original, CostRateUnit targetUnit, double hoursPerDay)
		{
			return ConvertCostRateUnit(original.Value, original.Unit, targetUnit, hoursPerDay);
		}
		public static double Convert(string originalValue, CostRateUnit originalUnit, CostRateUnit targetUnit, double hoursPerDay)
		{
			return ConvertCostRateUnit(StringToDouble(originalValue), originalUnit, targetUnit, hoursPerDay);
		}

		public static double Convert(double originalValue, EnergyConsumptionRateUnit originalUnit, EnergyConsumptionRateUnit targetUnit)
		{
			return ConvertEnergyConsumptionRateUnit(originalValue, originalUnit, targetUnit);
		}
		public static double Convert(string originalValue, EnergyConsumptionRateUnit originalUnit, EnergyConsumptionRateUnit targetUnit)
		{
			return ConvertEnergyConsumptionRateUnit(StringToDouble(originalValue), originalUnit, targetUnit);
		}

		public static double Convert(double originalValue, LengthUnit originalUnit, LengthUnit targetUnit)
		{
			return ConvertLengthUnit(originalValue, originalUnit, targetUnit);
		}

		public static double Convert(string originalValue, LengthUnit originalUnit, LengthUnit targetUnit)
		{
			return ConvertLengthUnit(StringToDouble(originalValue), originalUnit, targetUnit);
		}

        public static double Convert(double originalValue, AngularityUnit originalUnit, AngularityUnit targetUnit)
        {
            return ConvertAngularityUnit(originalValue, originalUnit, targetUnit);
        }
        public static double Convert(string originalValue, AngularityUnit originalUnit, AngularityUnit targetUnit)
        {
            return ConvertAngularityUnit(StringToDouble(originalValue), originalUnit, targetUnit);
        }

		internal static double ConvertToInternalValue(string originalValue, SpeedUnit originalUnit)
		{
			return Convert(originalValue, originalUnit, Speed.InternalUnit);
		}
		public static double ConvertToInternalValue(double originalValue, SpeedUnit originalUnit)
		{
			return Convert(originalValue, originalUnit, Speed.InternalUnit);
		}

		internal static double ConvertToInternalValue(string originalValue, TimeUnit originalUnit, double hoursPerDay)
		{
			return Convert(originalValue, originalUnit, Time.InternalUnit, hoursPerDay);
		}
		public static double ConvertToInternalValue(double originalValue, TimeUnit originalUnit, double hoursPerDay)
		{
			return Convert(originalValue, originalUnit, Time.InternalUnit, hoursPerDay);
		}

		internal static double ConvertToInternalValue(string originalValue, EnergyConsumptionRateUnit originalUnit)
		{
			return Convert(originalValue, originalUnit, EnergyConsumptionRate.InternalUnit);
		}
		public static double ConvertToInternalValue(double originalValue, EnergyConsumptionRateUnit originalUnit)
		{
			return Convert(originalValue, originalUnit, EnergyConsumptionRate.InternalUnit);
		}
		public static double ConvertToInternalValue(EnergyConsumptionRate original)
		{
			return Convert(original.Value, original.Unit, EnergyConsumptionRate.InternalUnit);
		}

		internal static double ConvertToInternalValue(string originalValue, CostRateUnit originalUnit, double hoursPerDay)
		{
			return Convert(originalValue, originalUnit, CostRate.InternalUnit, hoursPerDay);
		}

		public static double ConvertToInternalValue(double originalValue, CostRateUnit originalUnit, double hoursPerDay)
		{
			return Convert(originalValue, originalUnit, CostRate.InternalUnit, hoursPerDay);
		}

		public static double ConvertToInternalValue(CostRate original, double hoursPerDay)
		{
			return Convert(original.Value, original.Unit, CostRate.InternalUnit, hoursPerDay);
		}

        public static double ConvertToInternalValue(string originalValue, LengthUnit originalUnit)
        {
            return Convert(originalValue, originalUnit, Length.InternalUnit);
        }

        public static double ConvertToInternalValue(double originalValue, LengthUnit originalUnit)
        {
            return Convert(originalValue, originalUnit, Length.InternalUnit);
        }

        public static double ConvertToInternalValue(string originalValue, AngularityUnit originalUnit)
        {
            return Convert(originalValue, originalUnit, Angularity.InternalUnit);
        }

        public static double ConvertToInternalValue(double originalValue, AngularityUnit originalUnit)
        {
            return Convert(originalValue, originalUnit, Angularity.InternalUnit);
        }

        static double StringToDouble(string value, bool useCurrentCulture = true)
		{
            CultureInfo cul = useCurrentCulture ? CultureInfo.CurrentCulture : CultureInfo.InvariantCulture;
			double result = 0.0;
            if (!double.TryParse(value, NumberStyles.Float, cul, out result))
				throw new InvalidOperationException("Unconvertible units");  // TODO: Resource string + show the units value as context
			return result;
		}

		/// <summary>
		/// Used as a key into Converters dictionaries below, which point to the conversion methods for
		/// converting a value from one compatible units to another.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		struct Conversion<T>
		{
			readonly T originalUnit, targetUnit;

			public Conversion(T original, T target)
			{
				originalUnit = original;
				targetUnit = target;
			}
		}

		#region Speed converters

		static Lazy<IDictionary<Conversion<SpeedUnit>, Func<double, double>>> SpeedConverters = new Lazy<IDictionary<Conversion<SpeedUnit>, Func<double, double>>>(InitSpeedConverters);

		static IDictionary<Conversion<SpeedUnit>, Func<double, double>> InitSpeedConverters()
		{
			var speedConverters = new Dictionary<Conversion<SpeedUnit>, Func<double, double>>();

			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.FeetPerMin, SpeedUnit.MeterPerMin)] = FeetToMeter;
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.FeetPerHour, SpeedUnit.MeterPerHour)] = FeetToMeter;
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.FeetPerSecond, SpeedUnit.MeterPerSecond)] = FeetToMeter;
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.FeetPerMin, SpeedUnit.InchPerMin)] = FeetToInch;
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.FeetPerHour, SpeedUnit.InchPerHour)] = FeetToInch;
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.FeetPerSecond, SpeedUnit.InchPerSecond)] = FeetToInch;
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.FeetPerMin, SpeedUnit.CentimeterPerMin)] = FeetToCentimeter;
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.FeetPerHour, SpeedUnit.CentimeterPerHour)] = FeetToCentimeter;
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.FeetPerSecond, SpeedUnit.CentimeterPerSecond)] = FeetToCentimeter;

			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.MeterPerMin, SpeedUnit.FeetPerMin)] = MeterToFeet;
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.MeterPerHour, SpeedUnit.FeetPerHour)] = MeterToFeet;
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.MeterPerSecond, SpeedUnit.FeetPerSecond)] = MeterToFeet;
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.MeterPerMin, SpeedUnit.InchPerMin)] = MeterToInch;
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.MeterPerHour, SpeedUnit.InchPerHour)] = MeterToInch;
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.MeterPerSecond, SpeedUnit.InchPerSecond)] = MeterToInch;
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.MeterPerMin, SpeedUnit.CentimeterPerMin)] = MeterToCentimeter;
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.MeterPerHour, SpeedUnit.CentimeterPerHour)] = MeterToCentimeter;
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.MeterPerSecond, SpeedUnit.CentimeterPerSecond)] = MeterToCentimeter;

			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.InchPerMin, SpeedUnit.FeetPerMin)] = InchToFeet;
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.InchPerHour, SpeedUnit.FeetPerHour)] = InchToFeet;
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.InchPerSecond, SpeedUnit.FeetPerSecond)] = InchToFeet;
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.InchPerMin, SpeedUnit.MeterPerMin)] = InchToMeter;
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.InchPerHour, SpeedUnit.MeterPerHour)] = InchToMeter;
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.InchPerSecond, SpeedUnit.MeterPerSecond)] = InchToMeter;
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.InchPerMin, SpeedUnit.CentimeterPerMin)] = InchToCentimeter;
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.InchPerHour, SpeedUnit.CentimeterPerHour)] = InchToCentimeter;
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.InchPerSecond, SpeedUnit.CentimeterPerSecond)] = InchToCentimeter;

			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.CentimeterPerMin, SpeedUnit.FeetPerMin)] = CentimeterToFeet;
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.CentimeterPerHour, SpeedUnit.FeetPerHour)] = CentimeterToFeet;
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.CentimeterPerSecond, SpeedUnit.FeetPerSecond)] = CentimeterToFeet;
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.CentimeterPerMin, SpeedUnit.MeterPerMin)] = CentimeterToMeter;
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.CentimeterPerHour, SpeedUnit.MeterPerHour)] = CentimeterToMeter;
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.CentimeterPerSecond, SpeedUnit.MeterPerSecond)] = CentimeterToMeter;
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.CentimeterPerMin, SpeedUnit.InchPerMin)] = CentimeterToInch;
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.CentimeterPerHour, SpeedUnit.InchPerHour)] = CentimeterToInch;
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.CentimeterPerSecond, SpeedUnit.InchPerSecond)] = CentimeterToInch;

			SetFeetSpeedConverters(speedConverters);
			SetMeterSpeedConverters(speedConverters);
			SetInchSpeedConverters(speedConverters);
			SetCentimeterSpeedConverters(speedConverters);

			return speedConverters;
		}

		static void SetFeetSpeedConverters(IDictionary<Conversion<SpeedUnit>, Func<double, double>> speedConverters)
		{
			SetFeetPerHourConverter(speedConverters);
			SetFeetPerMinConverter(speedConverters);
			SetFeetPerSecondConverter(speedConverters);
		}

		static void SetFeetPerHourConverter(IDictionary<Conversion<SpeedUnit>, Func<double, double>> speedConverters)
		{
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.FeetPerHour, SpeedUnit.FeetPerMin)] = (double value) => value / HourToMinute(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.FeetPerHour, SpeedUnit.FeetPerSecond)] = (double value) => value / HourToSecond(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.FeetPerHour, SpeedUnit.MeterPerMin)] = (double value) => FeetToMeter(value) / HourToMinute(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.FeetPerHour, SpeedUnit.MeterPerSecond)] = (double value) => FeetToMeter(value) / HourToSecond(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.FeetPerHour, SpeedUnit.InchPerMin)] = (double value) => FeetToInch(value) / HourToMinute(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.FeetPerHour, SpeedUnit.InchPerSecond)] = (double value) => FeetToInch(value) / HourToSecond(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.FeetPerHour, SpeedUnit.CentimeterPerMin)] = (double value) => FeetToCentimeter(value) / HourToMinute(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.FeetPerHour, SpeedUnit.CentimeterPerSecond)] = (double value) => FeetToCentimeter(value) / HourToSecond(1.0);
		}

		static void SetFeetPerMinConverter(IDictionary<Conversion<SpeedUnit>, Func<double, double>> speedConverters)
		{
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.FeetPerMin, SpeedUnit.FeetPerHour)] = (double value) => value / MinuteToHour(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.FeetPerMin, SpeedUnit.FeetPerSecond)] = (double value) => value / MinuteToSecond(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.FeetPerMin, SpeedUnit.MeterPerHour)] = (double value) => FeetToMeter(value) / MinuteToHour(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.FeetPerMin, SpeedUnit.MeterPerSecond)] = (double value) => FeetToMeter(value) / MinuteToSecond(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.FeetPerMin, SpeedUnit.InchPerHour)] = (double value) => FeetToInch(value)/ MinuteToHour(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.FeetPerMin, SpeedUnit.InchPerSecond)] = (double value) => FeetToInch(value) / MinuteToSecond(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.FeetPerMin, SpeedUnit.CentimeterPerHour)] = (double value) => FeetToCentimeter(value) / MinuteToHour(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.FeetPerMin, SpeedUnit.CentimeterPerSecond)] = (double value) => FeetToCentimeter(value) / MinuteToSecond(1.0);
		}
		static void SetFeetPerSecondConverter(IDictionary<Conversion<SpeedUnit>, Func<double, double>> speedConverters)
		{
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.FeetPerSecond, SpeedUnit.FeetPerHour)] = (double value) => value / SecondToHour(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.FeetPerSecond, SpeedUnit.FeetPerMin)] = (double value) => value / SecondToMinute(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.FeetPerSecond, SpeedUnit.MeterPerHour)] = (double value) => FeetToMeter(value) / SecondToHour(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.FeetPerSecond, SpeedUnit.MeterPerMin)] = (double value) => FeetToMeter(value) / SecondToMinute(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.FeetPerSecond, SpeedUnit.InchPerHour)] = (double value) => FeetToInch(value) / SecondToHour(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.FeetPerSecond, SpeedUnit.InchPerMin)] = (double value) => FeetToInch(value) / SecondToMinute(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.FeetPerSecond, SpeedUnit.CentimeterPerHour)] = (double value) => FeetToCentimeter(value) / SecondToHour(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.FeetPerSecond, SpeedUnit.CentimeterPerMin)] = (double value) => FeetToCentimeter(value) / SecondToMinute(1.0);
		}

		static void SetMeterSpeedConverters(IDictionary<Conversion<SpeedUnit>, Func<double, double>> speedConverters)
		{
			SetMeterPerHourConverters(speedConverters);
			SetMeterPerMinConverters(speedConverters);
			SetMeterPerSecondConverters(speedConverters);
		}
		static void SetMeterPerHourConverters(IDictionary<Conversion<SpeedUnit>, Func<double, double>> speedConverters)
		{
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.MeterPerHour, SpeedUnit.MeterPerMin)] = (double value) => value / HourToMinute(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.MeterPerHour, SpeedUnit.MeterPerSecond)] = (double value) => value / HourToSecond(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.MeterPerHour, SpeedUnit.FeetPerMin)] = (double value) => MeterToFeet(value) / HourToMinute(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.MeterPerHour, SpeedUnit.FeetPerSecond)] = (double value) => MeterToFeet(value) / HourToSecond(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.MeterPerHour, SpeedUnit.InchPerMin)] = (double value) => MeterToInch(value) / HourToMinute(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.MeterPerHour, SpeedUnit.InchPerSecond)] = (double value) => MeterToInch(value) / HourToSecond(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.MeterPerHour, SpeedUnit.CentimeterPerMin)] = (double value) => MeterToCentimeter(value) / HourToMinute(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.MeterPerHour, SpeedUnit.CentimeterPerSecond)] = (double value) => MeterToCentimeter(value) / HourToSecond(1.0);
		}
		static void SetMeterPerMinConverters(IDictionary<Conversion<SpeedUnit>, Func<double, double>> speedConverters)
		{
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.MeterPerMin, SpeedUnit.MeterPerHour)] = (double value) => value / MinuteToHour(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.MeterPerMin, SpeedUnit.MeterPerSecond)] = (double value) => value / MinuteToSecond(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.MeterPerMin, SpeedUnit.FeetPerHour)] = (double value) => MeterToFeet(value) / MinuteToHour(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.MeterPerMin, SpeedUnit.FeetPerSecond)] = (double value) => MeterToFeet(value) / MinuteToSecond(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.MeterPerMin, SpeedUnit.InchPerHour)] = (double value) => MeterToInch(value) / MinuteToHour(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.MeterPerMin, SpeedUnit.InchPerSecond)] = (double value) => MeterToInch(value) / MinuteToSecond(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.MeterPerMin, SpeedUnit.CentimeterPerHour)] = (double value) => MeterToCentimeter(value) / MinuteToHour(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.MeterPerMin, SpeedUnit.CentimeterPerSecond)] = (double value) => MeterToCentimeter(value) / MinuteToSecond(1.0);
		}
		static void SetMeterPerSecondConverters(IDictionary<Conversion<SpeedUnit>, Func<double, double>> speedConverters)
		{
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.MeterPerSecond, SpeedUnit.MeterPerHour)] = (double value) => value / SecondToHour(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.MeterPerSecond, SpeedUnit.MeterPerMin)] = (double value) => value / SecondToMinute(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.MeterPerSecond, SpeedUnit.FeetPerHour)] = (double value) => MeterToFeet(value) / SecondToHour(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.MeterPerSecond, SpeedUnit.FeetPerMin)] = (double value) => MeterToFeet(value) / SecondToMinute(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.MeterPerSecond, SpeedUnit.InchPerHour)] = (double value) => MeterToInch(value) / SecondToHour(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.MeterPerSecond, SpeedUnit.InchPerMin)] = (double value) => MeterToInch(value) / SecondToMinute(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.MeterPerSecond, SpeedUnit.CentimeterPerHour)] = (double value) => MeterToCentimeter(value) / SecondToHour(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.MeterPerSecond, SpeedUnit.CentimeterPerMin)] = (double value) => MeterToCentimeter(value) / SecondToMinute(1.0);
		}

		static void SetInchSpeedConverters(IDictionary<Conversion<SpeedUnit>, Func<double, double>> speedConverters)
		{
			SetInchPerHourConverters(speedConverters);
			SetInchPerMinConverters(speedConverters);
			SetInchPerSecondConverters(speedConverters);
		}
		static void SetInchPerHourConverters(IDictionary<Conversion<SpeedUnit>, Func<double, double>> speedConverters)
		{
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.InchPerHour, SpeedUnit.MeterPerMin)] = (double value) => InchToMeter(value) / HourToMinute(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.InchPerHour, SpeedUnit.MeterPerSecond)] = (double value) => InchToMeter(value) / HourToSecond(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.InchPerHour, SpeedUnit.FeetPerMin)] = (double value) => InchToFeet(value) / HourToMinute(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.InchPerHour, SpeedUnit.FeetPerSecond)] = (double value) => InchToFeet(value) / HourToSecond(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.InchPerHour, SpeedUnit.InchPerMin)] = (double value) => value / HourToMinute(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.InchPerHour, SpeedUnit.InchPerSecond)] = (double value) => value / HourToSecond(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.InchPerHour, SpeedUnit.CentimeterPerMin)] = (double value) => InchToCentimeter(value) / HourToMinute(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.InchPerHour, SpeedUnit.CentimeterPerSecond)] = (double value) => InchToCentimeter(value) / HourToSecond(1.0);
		}
		static void SetInchPerMinConverters(IDictionary<Conversion<SpeedUnit>, Func<double, double>> speedConverters)
		{
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.InchPerMin, SpeedUnit.MeterPerHour)] = (double value) => InchToMeter(value) / MinuteToHour(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.InchPerMin, SpeedUnit.MeterPerSecond)] = (double value) => InchToMeter(value) / MinuteToSecond(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.InchPerMin, SpeedUnit.FeetPerHour)] = (double value) => InchToFeet(value) / MinuteToHour(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.InchPerMin, SpeedUnit.FeetPerSecond)] = (double value) => InchToFeet(value) / MinuteToSecond(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.InchPerMin, SpeedUnit.InchPerHour)] = (double value) => value / MinuteToHour(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.InchPerMin, SpeedUnit.InchPerSecond)] = (double value) => value / MinuteToSecond(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.InchPerMin, SpeedUnit.CentimeterPerHour)] = (double value) => InchToCentimeter(value) / MinuteToHour(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.InchPerMin, SpeedUnit.CentimeterPerSecond)] = (double value) => InchToCentimeter(value) / MinuteToSecond(1.0);
		}
		static void SetInchPerSecondConverters(IDictionary<Conversion<SpeedUnit>, Func<double, double>> speedConverters)
		{
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.InchPerSecond, SpeedUnit.MeterPerHour)] = (double value) => InchToMeter(value) / SecondToHour(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.InchPerSecond, SpeedUnit.MeterPerMin)] = (double value) => InchToMeter(value) / SecondToMinute(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.InchPerSecond, SpeedUnit.FeetPerHour)] = (double value) => InchToFeet(value) / SecondToHour(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.InchPerSecond, SpeedUnit.FeetPerMin)] = (double value) => InchToFeet(value) / SecondToMinute(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.InchPerSecond, SpeedUnit.InchPerHour)] = (double value) => value / SecondToHour(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.InchPerSecond, SpeedUnit.InchPerMin)] = (double value) => value / SecondToMinute(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.InchPerSecond, SpeedUnit.CentimeterPerHour)] = (double value) => InchToCentimeter(value) / SecondToHour(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.InchPerSecond, SpeedUnit.CentimeterPerMin)] = (double value) => InchToCentimeter(value) / SecondToMinute(1.0);
		}

		static void SetCentimeterSpeedConverters(IDictionary<Conversion<SpeedUnit>, Func<double, double>> speedConverters)
		{
			SetCentimeterPerHourConverters(speedConverters);
			SetCentimeterPerMinConverters(speedConverters);
			SetCentimeterPerSecondConverters(speedConverters);
		}
		static void SetCentimeterPerHourConverters(IDictionary<Conversion<SpeedUnit>, Func<double, double>> speedConverters)
		{
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.CentimeterPerHour, SpeedUnit.MeterPerMin)] = (double value) => CentimeterToMeter(value) / HourToMinute(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.CentimeterPerHour, SpeedUnit.MeterPerSecond)] = (double value) => CentimeterToMeter(value) / HourToSecond(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.CentimeterPerHour, SpeedUnit.FeetPerMin)] = (double value) => CentimeterToFeet(value) / HourToMinute(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.CentimeterPerHour, SpeedUnit.FeetPerSecond)] = (double value) => CentimeterToFeet(value) / HourToSecond(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.CentimeterPerHour, SpeedUnit.CentimeterPerMin)] = (double value) => value / HourToMinute(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.CentimeterPerHour, SpeedUnit.CentimeterPerSecond)] = (double value) => value / HourToSecond(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.CentimeterPerHour, SpeedUnit.InchPerMin)] = (double value) => CentimeterToInch(value) / HourToMinute(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.CentimeterPerHour, SpeedUnit.InchPerSecond)] = (double value) => CentimeterToInch(value) / HourToSecond(1.0);
		}
		static void SetCentimeterPerMinConverters(IDictionary<Conversion<SpeedUnit>, Func<double, double>> speedConverters)
		{
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.CentimeterPerMin, SpeedUnit.MeterPerHour)] = (double value) => CentimeterToMeter(value) / MinuteToHour(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.CentimeterPerMin, SpeedUnit.MeterPerSecond)] = (double value) => CentimeterToMeter(value) / MinuteToSecond(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.CentimeterPerMin, SpeedUnit.FeetPerHour)] = (double value) => CentimeterToFeet(value) / MinuteToHour(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.CentimeterPerMin, SpeedUnit.FeetPerSecond)] = (double value) => CentimeterToFeet(value) / MinuteToSecond(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.CentimeterPerMin, SpeedUnit.CentimeterPerHour)] = (double value) => value / MinuteToHour(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.CentimeterPerMin, SpeedUnit.CentimeterPerSecond)] = (double value) => value / MinuteToSecond(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.CentimeterPerMin, SpeedUnit.InchPerHour)] = (double value) => CentimeterToInch(value) / MinuteToHour(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.CentimeterPerMin, SpeedUnit.InchPerSecond)] = (double value) => CentimeterToInch(value) / MinuteToSecond(1.0);
		}
		static void SetCentimeterPerSecondConverters(IDictionary<Conversion<SpeedUnit>, Func<double, double>> speedConverters)
		{
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.CentimeterPerSecond, SpeedUnit.MeterPerHour)] = (double value) => CentimeterToMeter(value) / SecondToHour(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.CentimeterPerSecond, SpeedUnit.MeterPerMin)] = (double value) => CentimeterToMeter(value) / SecondToMinute(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.CentimeterPerSecond, SpeedUnit.FeetPerHour)] = (double value) => CentimeterToFeet(value) / SecondToHour(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.CentimeterPerSecond, SpeedUnit.FeetPerMin)] = (double value) => CentimeterToFeet(value) / SecondToMinute(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.CentimeterPerSecond, SpeedUnit.CentimeterPerHour)] = (double value) => value / SecondToHour(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.CentimeterPerSecond, SpeedUnit.CentimeterPerMin)] = (double value) => value / SecondToMinute(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.CentimeterPerSecond, SpeedUnit.InchPerHour)] = (double value) => CentimeterToInch(value) / SecondToHour(1.0);
			speedConverters[new Conversion<SpeedUnit>(SpeedUnit.CentimeterPerSecond, SpeedUnit.InchPerMin)] = (double value) => CentimeterToInch(value) / SecondToMinute(1.0);
		}

		#endregion

		[System.Diagnostics.CodeAnalysis.SuppressMessage/*CAOK*/("Microsoft.Usage", "CA2208:InstantiateArgumentExceptionsCorrectly", Justification = "the lambda confuses CA")]
		private static double ConvertSpeedUnit(double originalValue, SpeedUnit originalUnit, SpeedUnit targetUnit)
		{
			if (originalUnit == targetUnit)
				return originalValue;

			var conversion = new Conversion<SpeedUnit>(originalUnit, targetUnit);
			var converter = SpeedConverters.Value.SingleOrDefault(conversion, () => { throw new ArgumentOutOfRangeException(/*MSG0*/"originalUnit"); });
			return converter(originalValue);
		}

		#region Time converters

		static Lazy<IDictionary<Conversion<TimeUnit>, Func<double, double, double>>> TimeConverters = new Lazy<IDictionary<Conversion<TimeUnit>, Func<double, double, double>>>(InitTimeConverters);

		static IDictionary<Conversion<TimeUnit>, Func<double, double, double>> InitTimeConverters()
		{
			var timeConverters = new Dictionary<Conversion<TimeUnit>, Func<double, double, double>>();

			timeConverters[new Conversion<TimeUnit>(TimeUnit.Day, TimeUnit.Hour)] = DayToHour;
			timeConverters[new Conversion<TimeUnit>(TimeUnit.Day, TimeUnit.Minute)] = DayToMinute;
			timeConverters[new Conversion<TimeUnit>(TimeUnit.Day, TimeUnit.Second)] = DayToSecond;

			timeConverters[new Conversion<TimeUnit>(TimeUnit.Hour, TimeUnit.Day)] = HourToDay;
			timeConverters[new Conversion<TimeUnit>(TimeUnit.Hour, TimeUnit.Minute)] = HourToMinute;
			timeConverters[new Conversion<TimeUnit>(TimeUnit.Hour, TimeUnit.Second)] = HourToSecond;

			timeConverters[new Conversion<TimeUnit>(TimeUnit.Minute, TimeUnit.Day)] = MinuteToDay;
			timeConverters[new Conversion<TimeUnit>(TimeUnit.Minute, TimeUnit.Hour)] = MinuteToHour;
			timeConverters[new Conversion<TimeUnit>(TimeUnit.Minute, TimeUnit.Second)] = MinuteToSecond;

			timeConverters[new Conversion<TimeUnit>(TimeUnit.Second, TimeUnit.Day)] = SecondToDay;
			timeConverters[new Conversion<TimeUnit>(TimeUnit.Second, TimeUnit.Hour)] = SecondToHour;
			timeConverters[new Conversion<TimeUnit>(TimeUnit.Second, TimeUnit.Minute)] = SecondToMinute;

			return timeConverters;
		}

		#endregion

		[System.Diagnostics.CodeAnalysis.SuppressMessage/*CAOK*/("Microsoft.Usage", "CA2208:InstantiateArgumentExceptionsCorrectly", Justification = "the lambda confuses CA")]
		private static double ConvertTimeUnit(double originalValue, TimeUnit originalUnit, TimeUnit targetUnit, double hoursPerDay)
		{
			if (originalUnit == targetUnit)
				return originalValue;

			var conversion = new Conversion<TimeUnit>(originalUnit, targetUnit);
			var converter = TimeConverters.Value.SingleOrDefault(conversion, () => { throw new ArgumentOutOfRangeException(/*MSG0*/"originalUnit"); });
			return converter(originalValue, hoursPerDay);
		}

		#region CostRate converters

		static Lazy<IDictionary<Conversion<CostRateUnit>, Func<double, double, double>>> CostRateConverters = new Lazy<IDictionary<Conversion<CostRateUnit>, Func<double, double, double>>>(InitCostRateConverters);

		static IDictionary<Conversion<CostRateUnit>, Func<double, double, double>> InitCostRateConverters()
		{
			var costRateConverters = new Dictionary<Conversion<CostRateUnit>, Func<double, double, double>>();

			costRateConverters[new Conversion<CostRateUnit>(CostRateUnit.PerDay, CostRateUnit.PerHour)] = HourToDay;
			costRateConverters[new Conversion<CostRateUnit>(CostRateUnit.PerDay, CostRateUnit.PerMinute)] = MinuteToDay;
			costRateConverters[new Conversion<CostRateUnit>(CostRateUnit.PerDay, CostRateUnit.PerSecond)] = SecondToDay;

			costRateConverters[new Conversion<CostRateUnit>(CostRateUnit.PerHour, CostRateUnit.PerDay)] = DayToHour;
			costRateConverters[new Conversion<CostRateUnit>(CostRateUnit.PerHour, CostRateUnit.PerMinute)] = MinuteToHour;
			costRateConverters[new Conversion<CostRateUnit>(CostRateUnit.PerHour, CostRateUnit.PerSecond)] = SecondToHour;

			costRateConverters[new Conversion<CostRateUnit>(CostRateUnit.PerMinute, CostRateUnit.PerDay)] = DayToMinute;
			costRateConverters[new Conversion<CostRateUnit>(CostRateUnit.PerMinute, CostRateUnit.PerHour)] = HourToMinute;
			costRateConverters[new Conversion<CostRateUnit>(CostRateUnit.PerMinute, CostRateUnit.PerSecond)] = SecondToMinute;

			costRateConverters[new Conversion<CostRateUnit>(CostRateUnit.PerSecond, CostRateUnit.PerDay)] = DayToSecond;
			costRateConverters[new Conversion<CostRateUnit>(CostRateUnit.PerSecond, CostRateUnit.PerHour)] = HourToSecond;
			costRateConverters[new Conversion<CostRateUnit>(CostRateUnit.PerSecond, CostRateUnit.PerMinute)] = MinuteToSecond;

			return costRateConverters;
		}

		#endregion

		[System.Diagnostics.CodeAnalysis.SuppressMessage/*CAOK*/("Microsoft.Usage", "CA2208:InstantiateArgumentExceptionsCorrectly", Justification="the lambda confuses CA")]
		private static double ConvertCostRateUnit(double originalValue, CostRateUnit originalUnit, CostRateUnit targetUnit, double hoursPerDay)
		{
			if (originalUnit == targetUnit)
				return originalValue;

			var conversion = new Conversion<CostRateUnit>(originalUnit, targetUnit);
			var converter = CostRateConverters.Value.SingleOrDefault(conversion, () => { throw new ArgumentOutOfRangeException(/*MSG0*/"originalUnit"); });
			return converter(originalValue, hoursPerDay);
		}

		#region Energy consumption rate converters

		static IDictionary<Conversion<EnergyConsumptionRateUnit>, Func<double, double>> EnergyConsumptionRateConverters;

		static void InitEnergyConsumptionRateConverters()
		{
			if (EnergyConsumptionRateConverters == null)
			{
				EnergyConsumptionRateConverters = new Dictionary<Conversion<EnergyConsumptionRateUnit>, Func<double, double>>();

				EnergyConsumptionRateConverters[new Conversion<EnergyConsumptionRateUnit>(EnergyConsumptionRateUnit.kW, EnergyConsumptionRateUnit.JoulePerHour)] =
					KWToJoulePerHour;
				EnergyConsumptionRateConverters[new Conversion<EnergyConsumptionRateUnit>(EnergyConsumptionRateUnit.kW, EnergyConsumptionRateUnit.JoulePerMin)] = 
					(double value) => KWToJoulePerHour(value) / HourToMinute(1.0);
				EnergyConsumptionRateConverters[new Conversion<EnergyConsumptionRateUnit>(EnergyConsumptionRateUnit.kW, EnergyConsumptionRateUnit.JoulePerSecond)] =
					(double value) => KWToJoulePerHour(value) / HourToSecond(1.0);

				EnergyConsumptionRateConverters[new Conversion<EnergyConsumptionRateUnit>(EnergyConsumptionRateUnit.JoulePerHour, EnergyConsumptionRateUnit.kW)] =
					JoulePerHourToKW;
				EnergyConsumptionRateConverters[new Conversion<EnergyConsumptionRateUnit>(EnergyConsumptionRateUnit.JoulePerHour, EnergyConsumptionRateUnit.JoulePerMin)] =
					(double value) => value / HourToMinute(1.0);
				EnergyConsumptionRateConverters[new Conversion<EnergyConsumptionRateUnit>(EnergyConsumptionRateUnit.JoulePerHour, EnergyConsumptionRateUnit.JoulePerSecond)] = 
					(double value) => value / HourToSecond(1.0);

				EnergyConsumptionRateConverters[new Conversion<EnergyConsumptionRateUnit>(EnergyConsumptionRateUnit.JoulePerMin, EnergyConsumptionRateUnit.kW)] =
					(double value) => JoulePerHourToKW(value * HourToMinute(1.0));
				EnergyConsumptionRateConverters[new Conversion<EnergyConsumptionRateUnit>(EnergyConsumptionRateUnit.JoulePerMin, EnergyConsumptionRateUnit.JoulePerHour)] = 
					(double value) => value / MinuteToHour(1.0);
				EnergyConsumptionRateConverters[new Conversion<EnergyConsumptionRateUnit>(EnergyConsumptionRateUnit.JoulePerMin, EnergyConsumptionRateUnit.JoulePerSecond)] = 
					(double value) => value / MinuteToSecond(1.0);

				EnergyConsumptionRateConverters[new Conversion<EnergyConsumptionRateUnit>(EnergyConsumptionRateUnit.JoulePerSecond, EnergyConsumptionRateUnit.kW)] =
					(double value) => JoulePerHourToKW(value * HourToSecond(1.0));
				EnergyConsumptionRateConverters[new Conversion<EnergyConsumptionRateUnit>(EnergyConsumptionRateUnit.JoulePerSecond, EnergyConsumptionRateUnit.JoulePerHour)] =
					(double value) => value / SecondToHour(1.0);
				EnergyConsumptionRateConverters[new Conversion<EnergyConsumptionRateUnit>(EnergyConsumptionRateUnit.JoulePerSecond, EnergyConsumptionRateUnit.JoulePerMin)] =
					(double value) => value / SecondToMinute(1.0);
			}
		}

		#endregion

		[System.Diagnostics.CodeAnalysis.SuppressMessage/*CAOK*/("Microsoft.Usage", "CA2208:InstantiateArgumentExceptionsCorrectly", Justification = "the lambda confuses CA")]
		private static double ConvertEnergyConsumptionRateUnit(double originalValue, EnergyConsumptionRateUnit originalUnit, EnergyConsumptionRateUnit targetUnit)
		{
			if (originalUnit == targetUnit)
				return originalValue;

			InitEnergyConsumptionRateConverters();
			var conversion = new Conversion<EnergyConsumptionRateUnit>(originalUnit, targetUnit);
			var converter = EnergyConsumptionRateConverters.SingleOrDefault(conversion, () => { throw new ArgumentOutOfRangeException(/*MSG0*/"originalUnit"); });
			return converter(originalValue);
		}

		#region Length converters

		static Lazy<IDictionary<Conversion<LengthUnit>, Func<double, double>>> LengthConverters = new Lazy<IDictionary<Conversion<LengthUnit>, Func<double, double>>>(InitLengthConverters);

		static IDictionary<Conversion<LengthUnit>, Func<double, double>> InitLengthConverters()
		{
			var lengthConverters = new Dictionary<Conversion<LengthUnit>, Func<double, double>>();

			lengthConverters[new Conversion<LengthUnit>(LengthUnit.Millimeters, LengthUnit.Centimeters)] = MillimeterToCentimeter;
            lengthConverters[new Conversion<LengthUnit>(LengthUnit.Millimeters, LengthUnit.Decimeters)] = MillimeterToDecimeter;
			lengthConverters[new Conversion<LengthUnit>(LengthUnit.Millimeters, LengthUnit.Meters)] = MillimeterToMeter;
			lengthConverters[new Conversion<LengthUnit>(LengthUnit.Millimeters, LengthUnit.Kilometers)] = MillimeterToKilometer;
			lengthConverters[new Conversion<LengthUnit>(LengthUnit.Millimeters, LengthUnit.Inches)] = MillimeterToInch;
			lengthConverters[new Conversion<LengthUnit>(LengthUnit.Millimeters, LengthUnit.Feet)] = MillimeterToFeet;
			lengthConverters[new Conversion<LengthUnit>(LengthUnit.Millimeters, LengthUnit.Yards)] = MillimeterToYard;
			lengthConverters[new Conversion<LengthUnit>(LengthUnit.Millimeters, LengthUnit.Miles)] = MillimeterToMile;
            lengthConverters[new Conversion<LengthUnit>(LengthUnit.Millimeters, LengthUnit.Micron)] = MillimeterToMicron;

            lengthConverters[new Conversion<LengthUnit>(LengthUnit.Centimeters, LengthUnit.Millimeters)] = CentimeterToMillimeter;
            lengthConverters[new Conversion<LengthUnit>(LengthUnit.Centimeters, LengthUnit.Decimeters)] = CentimeterToDecimeter;
            lengthConverters[new Conversion<LengthUnit>(LengthUnit.Centimeters, LengthUnit.Meters)] = CentimeterToMeter;
            lengthConverters[new Conversion<LengthUnit>(LengthUnit.Centimeters, LengthUnit.Kilometers)] = CentimeterToKilometer;
            lengthConverters[new Conversion<LengthUnit>(LengthUnit.Centimeters, LengthUnit.Inches)] = CentimeterToInch;
            lengthConverters[new Conversion<LengthUnit>(LengthUnit.Centimeters, LengthUnit.Feet)] = CentimeterToFeet;
            lengthConverters[new Conversion<LengthUnit>(LengthUnit.Centimeters, LengthUnit.Yards)] = CentimeterToYard;
            lengthConverters[new Conversion<LengthUnit>(LengthUnit.Centimeters, LengthUnit.Miles)] = CentimeterToMile;
            lengthConverters[new Conversion<LengthUnit>(LengthUnit.Centimeters, LengthUnit.Micron)] = CentimeterToMicron;

            lengthConverters[new Conversion<LengthUnit>(LengthUnit.Decimeters, LengthUnit.Millimeters)] = DecimeterToMilimeter;
            lengthConverters[new Conversion<LengthUnit>(LengthUnit.Decimeters, LengthUnit.Centimeters)] = DecimeterToCentimeter;
            lengthConverters[new Conversion<LengthUnit>(LengthUnit.Decimeters, LengthUnit.Meters)] = DecimeterToMeter;
            lengthConverters[new Conversion<LengthUnit>(LengthUnit.Decimeters, LengthUnit.Kilometers)] = DecimeterToKilometer;
            lengthConverters[new Conversion<LengthUnit>(LengthUnit.Decimeters, LengthUnit.Inches)] = DecimeterToInch;
            lengthConverters[new Conversion<LengthUnit>(LengthUnit.Decimeters, LengthUnit.Feet)] = DecimeterToFeet;
            lengthConverters[new Conversion<LengthUnit>(LengthUnit.Decimeters, LengthUnit.Yards)] = DecimeterToYard;
            lengthConverters[new Conversion<LengthUnit>(LengthUnit.Decimeters, LengthUnit.Miles)] = DecimeterToMile;
            lengthConverters[new Conversion<LengthUnit>(LengthUnit.Decimeters, LengthUnit.Micron)] = DecimeterToMicron;

            lengthConverters[new Conversion<LengthUnit>(LengthUnit.Meters, LengthUnit.Millimeters)] = MeterToMillimeter;
			lengthConverters[new Conversion<LengthUnit>(LengthUnit.Meters, LengthUnit.Centimeters)] = MeterToCentimeter;
            lengthConverters[new Conversion<LengthUnit>(LengthUnit.Meters, LengthUnit.Decimeters)] = MeterToDecimeter;
			lengthConverters[new Conversion<LengthUnit>(LengthUnit.Meters, LengthUnit.Kilometers)] = MeterToKilometer;
			lengthConverters[new Conversion<LengthUnit>(LengthUnit.Meters, LengthUnit.Inches)] = MeterToInch;
			lengthConverters[new Conversion<LengthUnit>(LengthUnit.Meters, LengthUnit.Feet)] = MeterToFeet;
			lengthConverters[new Conversion<LengthUnit>(LengthUnit.Meters, LengthUnit.Yards)] = MeterToYard;
			lengthConverters[new Conversion<LengthUnit>(LengthUnit.Meters, LengthUnit.Miles)] = MeterToMile;
            lengthConverters[new Conversion<LengthUnit>(LengthUnit.Meters, LengthUnit.Micron)] = MeterToMicron;

			lengthConverters[new Conversion<LengthUnit>(LengthUnit.Kilometers, LengthUnit.Millimeters)] = KilometerToMillimeter;
            lengthConverters[new Conversion<LengthUnit>(LengthUnit.Kilometers, LengthUnit.Centimeters)] = KilometerToCentimeter;
            lengthConverters[new Conversion<LengthUnit>(LengthUnit.Kilometers, LengthUnit.Decimeters)] = KilometersToDecimeter;
			lengthConverters[new Conversion<LengthUnit>(LengthUnit.Kilometers, LengthUnit.Meters)] = KilometerToMeter;
			lengthConverters[new Conversion<LengthUnit>(LengthUnit.Kilometers, LengthUnit.Inches)] = KilometerToInch;
			lengthConverters[new Conversion<LengthUnit>(LengthUnit.Kilometers, LengthUnit.Feet)] = KilometerToFeet;
			lengthConverters[new Conversion<LengthUnit>(LengthUnit.Kilometers, LengthUnit.Yards)] = KilometerToYard;
			lengthConverters[new Conversion<LengthUnit>(LengthUnit.Kilometers, LengthUnit.Miles)] = KilometerToMile;
            lengthConverters[new Conversion<LengthUnit>(LengthUnit.Kilometers, LengthUnit.Micron)] = KilometerToMicron;

            lengthConverters[new Conversion<LengthUnit>(LengthUnit.Inches, LengthUnit.Millimeters)] = InchToMillimeter;
            lengthConverters[new Conversion<LengthUnit>(LengthUnit.Inches, LengthUnit.Decimeters)] = InchesToDecimeter;
			lengthConverters[new Conversion<LengthUnit>(LengthUnit.Inches, LengthUnit.Centimeters)] = InchToCentimeter;
			lengthConverters[new Conversion<LengthUnit>(LengthUnit.Inches, LengthUnit.Meters)] = InchToMeter;
			lengthConverters[new Conversion<LengthUnit>(LengthUnit.Inches, LengthUnit.Kilometers)] = InchToKilometer;
			lengthConverters[new Conversion<LengthUnit>(LengthUnit.Inches, LengthUnit.Feet)] = InchToFeet;
			lengthConverters[new Conversion<LengthUnit>(LengthUnit.Inches, LengthUnit.Yards)] = InchToYard;
			lengthConverters[new Conversion<LengthUnit>(LengthUnit.Inches, LengthUnit.Miles)] = InchToMile;
            lengthConverters[new Conversion<LengthUnit>(LengthUnit.Inches, LengthUnit.Micron)] = InchToMicron;

            lengthConverters[new Conversion<LengthUnit>(LengthUnit.Feet, LengthUnit.Millimeters)] = FeetToMillimeter;
            lengthConverters[new Conversion<LengthUnit>(LengthUnit.Feet, LengthUnit.Centimeters)] = FeetToCentimeter;
            lengthConverters[new Conversion<LengthUnit>(LengthUnit.Feet, LengthUnit.Decimeters)] = FeetToDecimeter;
			lengthConverters[new Conversion<LengthUnit>(LengthUnit.Feet, LengthUnit.Meters)] = FeetToMeter;
			lengthConverters[new Conversion<LengthUnit>(LengthUnit.Feet, LengthUnit.Kilometers)] = FeetToKilometer;
			lengthConverters[new Conversion<LengthUnit>(LengthUnit.Feet, LengthUnit.Inches)] = FeetToInch;
			lengthConverters[new Conversion<LengthUnit>(LengthUnit.Feet, LengthUnit.Yards)] = FeetToYard;
			lengthConverters[new Conversion<LengthUnit>(LengthUnit.Feet, LengthUnit.Miles)] = FeetToMile;
            lengthConverters[new Conversion<LengthUnit>(LengthUnit.Feet, LengthUnit.Micron)] = FeetToMicron;

            lengthConverters[new Conversion<LengthUnit>(LengthUnit.Yards, LengthUnit.Millimeters)] = YardToMillimeter;
			lengthConverters[new Conversion<LengthUnit>(LengthUnit.Yards, LengthUnit.Centimeters)] = YardToCentimeter;
            lengthConverters[new Conversion<LengthUnit>(LengthUnit.Yards, LengthUnit.Decimeters)] = YardToDecimeter;
			lengthConverters[new Conversion<LengthUnit>(LengthUnit.Yards, LengthUnit.Meters)] = YardToMeter;
			lengthConverters[new Conversion<LengthUnit>(LengthUnit.Yards, LengthUnit.Kilometers)] = YardToKilometer;
			lengthConverters[new Conversion<LengthUnit>(LengthUnit.Yards, LengthUnit.Inches)] = YardToInch;
			lengthConverters[new Conversion<LengthUnit>(LengthUnit.Yards, LengthUnit.Feet)] = YardToFeet;
			lengthConverters[new Conversion<LengthUnit>(LengthUnit.Yards, LengthUnit.Miles)] = YardToMile;
            lengthConverters[new Conversion<LengthUnit>(LengthUnit.Yards, LengthUnit.Micron)] = YardToMicron;

            lengthConverters[new Conversion<LengthUnit>(LengthUnit.Miles, LengthUnit.Millimeters)] = MileToMillimeter;
			lengthConverters[new Conversion<LengthUnit>(LengthUnit.Miles, LengthUnit.Centimeters)] = MileToCentimeter;
            lengthConverters[new Conversion<LengthUnit>(LengthUnit.Miles, LengthUnit.Decimeters)] = MileToDecimeter;
			lengthConverters[new Conversion<LengthUnit>(LengthUnit.Miles, LengthUnit.Meters)] = MileToMeter;
			lengthConverters[new Conversion<LengthUnit>(LengthUnit.Miles, LengthUnit.Kilometers)] = MileToKilometer;
			lengthConverters[new Conversion<LengthUnit>(LengthUnit.Miles, LengthUnit.Inches)] = MileToInch;
			lengthConverters[new Conversion<LengthUnit>(LengthUnit.Miles, LengthUnit.Feet)] = MileToFeet;
			lengthConverters[new Conversion<LengthUnit>(LengthUnit.Miles, LengthUnit.Yards)] = MileToYard;
            lengthConverters[new Conversion<LengthUnit>(LengthUnit.Miles, LengthUnit.Micron)] = MileToMicron;

            lengthConverters[new Conversion<LengthUnit>(LengthUnit.Micron, LengthUnit.Millimeters)] = MicronToMillimeter;
            lengthConverters[new Conversion<LengthUnit>(LengthUnit.Micron, LengthUnit.Centimeters)] = MicronToCentimeter;
            lengthConverters[new Conversion<LengthUnit>(LengthUnit.Micron, LengthUnit.Decimeters)] = MicronToDecimeter;
            lengthConverters[new Conversion<LengthUnit>(LengthUnit.Micron, LengthUnit.Meters)] = MicronToMeter;
            lengthConverters[new Conversion<LengthUnit>(LengthUnit.Micron, LengthUnit.Kilometers)] = MicronToKilometer;
            lengthConverters[new Conversion<LengthUnit>(LengthUnit.Micron, LengthUnit.Inches)] = MicronToInch;
            lengthConverters[new Conversion<LengthUnit>(LengthUnit.Micron, LengthUnit.Feet)] = MicronToFeet;
            lengthConverters[new Conversion<LengthUnit>(LengthUnit.Micron, LengthUnit.Yards)] = MicronToYard;
            lengthConverters[new Conversion<LengthUnit>(LengthUnit.Micron, LengthUnit.Miles)] = MicronToMile;

			return lengthConverters;
		}

		#endregion

		private static double ConvertLengthUnit(double originalValue,LengthUnit originalUnit,LengthUnit targetUnit)
		{
			if (originalUnit == targetUnit)
				return originalValue;

			var conversion = new Conversion<LengthUnit>(originalUnit, targetUnit);
			var converter = LengthConverters.Value.SingleOrDefault(conversion, () => { throw new ArgumentOutOfRangeException(/*MSG0*/"originalUnit"); });
			return converter(originalValue);
		}

        #region Angularity

        // Represents 1 degree equals to how many radians
        private const double DegreeToRadiansRatio = Math.PI / 180;

        public static double ConvertAngularityToInternalUnit(double originalValue, AngularityUnit angularityUnit)
        {
            switch (angularityUnit)
            {
                case AngularityUnit.Radians:
                    return originalValue;
                case AngularityUnit.Degree:
                    return originalValue * DegreeToRadiansRatio;
                case AngularityUnit.Grad:
                case AngularityUnit.Steradian:
                    throw new NotImplementedException();
                default:
                    throw new InvalidOperationException();
            }
        }

        public static double ConvertAngularityFromInternalUnit(double originalValue, AngularityUnit angularityUnit)
        {
            switch (angularityUnit)
            {
                case AngularityUnit.Radians:
                    return originalValue;
                case AngularityUnit.Degree:
                    return originalValue / DegreeToRadiansRatio;
                case AngularityUnit.Grad:
                case AngularityUnit.Steradian:
                    throw new NotImplementedException();
                default:
                    throw new InvalidOperationException();
            }
        }

        public static double ConvertAngularityUnit(double originalValue, AngularityUnit originalUnit, AngularityUnit targetUnit)
        {
            if (originalUnit == targetUnit)
                return originalValue;

            double newValue = ConvertAngularityToInternalUnit(originalValue, originalUnit);
            newValue = ConvertAngularityFromInternalUnit(newValue, targetUnit);

            return newValue;
        }

        #endregion

        #region Energy conversion methods

        private static double KWToJoulePerHour(double value)
		{
			return value * 3600000;
		}

		private static double JoulePerHourToKW(double value)
		{
			return value / 3600000;
		}

		#endregion

		#region Length conversion methods

		private static double FeetToInch(double value)
		{
			return value * 12;
		}

		private static double FeetToYard(double value)
		{
			return value / 3;
		}

		private static double FeetToMile(double value)
		{
			return value / 5280;
		}

        private static double FeetToMicron(double value)
        {
            return value * 304800;
        }

		private static double FeetToMillimeter(double value)
		{
			return CentimeterToMillimeter(FeetToCentimeter(value));
		}

		private static double FeetToCentimeter(double value)
		{
			return MeterToCentimeter(FeetToMeter(value));
		}

        private static double FeetToDecimeter(double value)
        {
            return MeterToDecimeter(FeetToMeter(value));
        }

		private static double FeetToMeter(double value)
		{
            return value / 3.2808398950131;
		}

		private static double FeetToKilometer(double value)
		{
			return MeterToKilometer(FeetToMeter(value));
		}

		private static double InchToFeet(double value)
		{
			return value / 12;
		}

		private static double InchToYard(double value)
		{
			return FeetToYard(InchToFeet(value));
		}

		private static double InchToMile(double value)
		{
			return FeetToMile(InchToFeet(value));
		}

        private static double InchToMicron(double value)
        {
            return FeetToMicron(InchToFeet(value));
        }

		private static double InchToMillimeter(double value)
		{
			return FeetToMillimeter(InchToFeet(value));
		}

		private static double InchToCentimeter(double value)
		{
			return FeetToCentimeter(InchToFeet(value));
		}

        private static double InchesToDecimeter(double value)
        {
            return FeetToDecimeter(InchToFeet(value));
        }

		private static double InchToMeter(double value)
		{
			return FeetToMeter(InchToFeet(value));
		}

		private static double InchToKilometer(double value)
		{
			return FeetToKilometer(InchToFeet(value));
		}

		private static double YardToInch(double value)
		{
			return FeetToInch(YardToFeet(value));
		}

		private static double YardToFeet(double value)
		{
			return value * 3;
		}

		private static double YardToMile(double value)
		{
			return FeetToMile(YardToFeet(value));
		}

        private static double YardToMicron(double value)
        {
            return FeetToMicron(YardToFeet(value));
        }

		private static double YardToMillimeter(double value)
		{
			return FeetToMillimeter(YardToFeet(value));
		}

		private static double YardToCentimeter(double value)
		{
			return FeetToCentimeter(YardToFeet(value));
		}

        private static double YardToDecimeter(double value)
        {
            return FeetToDecimeter(YardToFeet(value));
        }

		private static double YardToMeter(double value)
		{
			return FeetToMillimeter(YardToFeet(value));
		}

		private static double YardToKilometer(double value)
		{
			return FeetToKilometer(YardToFeet(value));
		}

		public static double MileToInch(double value)
		{
			return FeetToInch(MileToFeet(value));
		}

		public static double MileToFeet(double value)
		{
			return value * 5280;
		}

		public static double MileToYard(double value)
		{
			return FeetToYard(MileToFeet(value));
		}

        public static double MileToMicron(double value)
        {
            return FeetToMicron(MileToFeet(value));
        }

		public static double MileToMillimeter(double value)
		{
			return FeetToMillimeter(MileToFeet(value));
		}

		public static double MileToCentimeter(double value)
		{
			return FeetToCentimeter(MileToFeet(value));
		}

        public static double MileToDecimeter(double value)
        {
            return FeetToDecimeter(MileToFeet(value));
        }

		public static double MileToMeter(double value)
		{
			return FeetToMeter(MileToFeet(value));
		}

		public static double MileToKilometer(double value)
		{
			return FeetToKilometer(MileToFeet(value));
		}

        private static double MillimeterToDecimeter(double value)
        {
            return value / 100;
        }

		private static double MillimeterToCentimeter(double value)
		{
			return value / 10;
		}

		private static double MillimeterToMeter(double value)
		{
			return value / 1000;
		}

		private static double MillimeterToKilometer(double value)
		{
			return MeterToKilometer(MillimeterToMeter(value));
		}

		private static double MillimeterToInch(double value)
		{
			return MeterToInch(MillimeterToMeter(value));
		}

		private static double MillimeterToFeet(double value)
		{
			return MeterToFeet(MillimeterToMeter(value));
		}

		private static double MillimeterToYard(double value)
		{
			return MeterToYard(MillimeterToMeter(value));
		}

		private static double MillimeterToMile(double value)
		{
			return FeetToMile(MillimeterToFeet(value));
		}

        private static double MillimeterToMicron(double value)
        {
            return FeetToMicron(MillimeterToFeet(value));
        }

		private static double MeterToMillimeter(double value)
		{
			return value * 1000;
		}

		private static double MeterToCentimeter(double value)
		{
			return value * 100;
		}

        private static double MeterToDecimeter(double value)
        {
            return value * 10;
        }

		private static double MeterToKilometer(double value)
		{
			return value / 1000;
		}

		private static double MeterToInch(double value)
		{
			return FeetToInch(MeterToFeet(value));
		}

		private static double MeterToFeet(double value)
		{
            //1 meter = 3.2808398950131 feet
            return value * 3.2808398950131;
		}

		private static double KilometerToMillimeter(double value)
		{
			return MeterToMillimeter(KilometerToMeter(value));
		}

		private static double KilometerToCentimeter(double value)
		{
			return MeterToCentimeter(KilometerToMeter(value));
		}

        private static double KilometersToDecimeter(double value)
        {
            return MeterToDecimeter(KilometerToMeter(value));
        }

		private static double KilometerToMeter(double value)
		{
			return value * 1000;
		}

		private static double KilometerToInch(double value)
		{
			return MeterToInch(KilometerToMeter(value));
		}

		private static double KilometerToFeet(double value)
		{
			return MeterToFeet(KilometerToMeter(value));
		}

		private static double KilometerToYard(double value)
		{
			return MeterToYard(KilometerToMeter(value));
		}

		private static double KilometerToMile(double value)
		{
			return MeterToMile(KilometerToMeter(value));
		}

        private static double KilometerToMicron(double value)
        {
            return MeterToMicron(KilometerToMeter(value));
        }

		private static double MeterToYard(double value)
		{
			return FeetToYard(MeterToFeet(value));
		}

		private static double MeterToMile(double value)
		{
			return FeetToMile(MeterToFeet(value));
		}

        private static double MeterToMicron(double value)
        {
            return FeetToMicron(MeterToFeet(value));
        }

		private static double CentimeterToMeter(double value)
		{
			return value / 100;
		}

		private static double CentimeterToMillimeter(double value)
		{
			return value * 10;
		}

        private static double CentimeterToDecimeter(double value)
        {
            return value / 10;
        }

		private static double CentimeterToKilometer(double value)
		{
			return MeterToKilometer(CentimeterToMeter(value));
		}

		private static double CentimeterToFeet(double value)
		{
			return MeterToFeet(CentimeterToMeter(value));
		}

		private static double CentimeterToInch(double value)
		{
			return FeetToInch(CentimeterToFeet(value));
		}

		private static double CentimeterToYard(double value)
		{
			return FeetToYard(CentimeterToFeet(value));
		}

		private static double CentimeterToMile(double value)
		{
			return FeetToMile(CentimeterToFeet(value));
		}

        private static double CentimeterToMicron(double value)
        {
            return FeetToMicron(CentimeterToFeet(value));
        }

        private static double DecimeterToMilimeter(double value)
        {
            return value * 100;
        }

        private static double DecimeterToCentimeter(double value)
        {
            return value * 10;
        }

        private static double DecimeterToMeter(double value)
        {
            return value / 10;
        }

        private static double DecimeterToKilometer(double vlaue)
        {
            return vlaue / 1000;
        }

        private static double DecimeterToInch(double value)
        {
            return FeetToInch(DecimeterToFeet(value));
        }

        private static double DecimeterToFeet(double value)
        {
            return MeterToFeet(DecimeterToMeter(value));
        }

        private static double DecimeterToYard(double value)
        {
            return FeetToYard(DecimeterToFeet(value));
        }

        private static double DecimeterToMile(double value)
        {
            return FeetToMile(DecimeterToFeet(value));
        }

        private static double DecimeterToMicron(double value)
        {
            return FeetToMicron(DecimeterToFeet(value));
        }


        private static double MicronToCentimeter(double value)
        {
            return FeetToCentimeter(MicronToFeet(value));
        }

        private static double MicronToDecimeter(double value)
        {
            return FeetToDecimeter(MicronToFeet(value));
        }

        private static double MicronToFeet(double value)
        {
            //1 micron = 3.280839e-06 feet
            return value / 3280839; 
        }

        private static double MicronToInch(double value)
        {
            return FeetToInch(MicronToFeet(value));
        }

        private static double MicronToKilometer(double value)
        {
            return FeetToKilometer(MicronToFeet(value));
        }

        private static double MicronToMeter(double value)
        {
            return FeetToMeter(MicronToFeet(value));
        }

        private static double MicronToMile(double value)
        {
            return FeetToMile(MicronToFeet(value));
        }

        private static double MicronToMillimeter(double value)
        {
            return FeetToMillimeter(MicronToFeet(value));
        }

        private static double MicronToYard(double value)
        {
            return FeetToYard(MicronToFeet(value));
        }

		#endregion

		#region Time conversion methods

		private static double HourToMinute(double value, double hoursPayDay = 24.0)
		{
			return value * 60;
		}

		private static double MinuteToHour(double value, double hoursPayDay = 24.0)
		{
			return value / 60;
		}

		private static double HourToDay(double value, double hoursPayDay = 24.0)
		{
			return value / hoursPayDay;
		}

		private static double DayToHour(double value, double hoursPayDay = 24.0)
		{
			return value * hoursPayDay;
		}

		private static double DayToMinute(double value, double hoursPayDay = 24.0)
		{
			return HourToMinute(DayToHour(value, hoursPayDay));
		}

		private static double MinuteToDay(double value, double hoursPayDay = 24.0)
		{
			return HourToDay(MinuteToHour(value), hoursPayDay);
		}

		private static double SecondToMinute(double value, double hoursPayDay = 24.0)
		{
			return value / 60;
		}

		private static double MinuteToSecond(double value, double hoursPayDay = 24.0)
		{
			return value * 60;
		}

		private static double SecondToHour(double value, double hoursPayDay = 24.0)
		{
			return MinuteToHour(SecondToMinute(value));
		}

		private static double HourToSecond(double value, double hoursPayDay = 24.0)
		{
			return MinuteToSecond(HourToMinute(value));
		}

		private static double SecondToDay(double value, double hoursPayDay = 24.0)
		{
			return MinuteToDay(SecondToMinute(value), hoursPayDay);
		}

		private static double DayToSecond(double value, double hoursPayDay = 24.0)
		{
			return MinuteToSecond(DayToMinute(value, hoursPayDay));
		}

		#endregion

        public static double ConvertCubicCentimetersToCubicFeet(double value)
        {
            return value / ((2.54 * 12.0) * (2.54 * 12.0) * (2.54 * 12.0));
        }

        public static double ConvertCubicCentimetersToCubicInches(double value)
        {
            return value / (2.54 * 2.54 * 2.54);
        }


        public static double ConvertCubicCentimetersToCubicMeters(double value)
        {
            return value * 0.000001;
        }

	}
}
