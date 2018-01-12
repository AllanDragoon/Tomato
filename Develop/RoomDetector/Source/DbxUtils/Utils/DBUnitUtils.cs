using System;
using System.Diagnostics;
using Autodesk.AutoCAD.DatabaseServices;
using DbxUtils.Properties;
using DbxUtils.Units;
using UnitType = DbxUtils.Units.UnitType;

namespace DbxUtils.Utils
{
    public static class DbUnitUtils
    {
        public static string GetInsUnitLong(Database database)
        {
            switch (database.Insunits)
            {
                case UnitsValue.Inches:
                    return Resources.Units_Inches_Long;
                case UnitsValue.Miles:
                    return Resources.Units_Miles_Long;
                case UnitsValue.Millimeters:
                    return Resources.Units_Millimeters_Long;
                case UnitsValue.Centimeters:
                    return Resources.Units_Centimeters_Long;
                case UnitsValue.Meters:
                    return Resources.Units_Meters_Long;
                case UnitsValue.Kilometers:
                    return Resources.Units_Kilometers_Long;

                default:
                    return Resources.Units_Feet_Long;
            }
        }

        public static string GetInsUnitShort(Database database)
        {
            switch (database.Insunits)
            {
                case UnitsValue.Inches:
                    return Resources.Units_Inches_Short;
                case UnitsValue.Miles:
                    return Resources.Units_Miles_Short;
                case UnitsValue.Millimeters:
                    return Resources.Units_Millimeters_Short;
                case UnitsValue.Centimeters:
                    return Resources.Units_Centimeters_Short;
                case UnitsValue.Meters:
                    return Resources.Units_Meters_Short;
                case UnitsValue.Kilometers:
                    return Resources.Units_Kilometers_Short;

                default:
                    return Resources.Units_Feet_Short;
            }
        }

        /// <summary>
        /// Get the unit of Asset Library based on input database unit
        /// </summary>
        public static UnitType GetUnitType(Database database)
        {
            if (database == null) throw new ArgumentNullException(/*MSG0*/"database");

            switch (database.Insunits)
            {
                //Imperial units
                case UnitsValue.Inches:
                case UnitsValue.Feet:
                case UnitsValue.Miles:
                case UnitsValue.Yards:
                case UnitsValue.MicroInches:
					return UnitType.Imperial;

                //Metric units
                case UnitsValue.Millimeters:
                case UnitsValue.Centimeters:
                case UnitsValue.Meters:
                case UnitsValue.Kilometers:
                case UnitsValue.Microns:
                case UnitsValue.Decimeters:
                case UnitsValue.Dekameters:
                case UnitsValue.Gigameters:
                case UnitsValue.Hectometers:
                case UnitsValue.Nanometers:
                case UnitsValue.Angstroms:
                case UnitsValue.Mils:
					return UnitType.Metric;

                //For others non-Metric/Imperial units, like Unitless, Astronomical etc.
                default: //db is unitless, regard Imperial as the default unit
					return UnitType.Imperial;
            }
        }

        /// <summary>
        /// Get the length unit type based on input database unit
        /// </summary>
        public static LengthUnit GetLengthUnitType(Database database)
        {
            if (database == null) throw new ArgumentNullException(/*MSG0*/"database");

            switch (database.Insunits)
            {
                //Imperial units
                case UnitsValue.Inches:
                    return LengthUnit.Inches;
                case UnitsValue.Feet:
                    return LengthUnit.Feet;
                case UnitsValue.Miles:
                    return LengthUnit.Miles;
                case UnitsValue.Yards:
                    return LengthUnit.Yards;

                //Metric units
                case UnitsValue.Millimeters:
                    return LengthUnit.Millimeters;
                case UnitsValue.Centimeters:
                    return LengthUnit.Centimeters;
                case UnitsValue.Meters:
                    return LengthUnit.Meters;
                case UnitsValue.Kilometers:
                    return LengthUnit.Kilometers;
                case UnitsValue.Decimeters:
                    return LengthUnit.Decimeters;

                default: //db is unitless
                    Debug.Fail("Shouldn't get here with unit = " + database.Insunits);
                    return LengthUnit.Unknown;
            }
        }

        /// <summary>
        /// Get the angularity unit type based on input database unit
        /// </summary>
        public static AngularityUnit GetAngularityUnitType(Database database)
        {
            if (database == null) throw new ArgumentNullException(/*MSG0*/"database");

            switch (database.Aunits)
            {             
                case 0:		//0  Degrees 
                    return AngularityUnit.Degree;
       
                case 3:		//3  Radians 
                    return AngularityUnit.Radians;
                
				//As inventor doesn't support below kinds of angularity type, so regard "Degree" as its default value  
                case 1:		//1  Degrees/minutes/seconds                 
                case 2:		//2  Grads 
                case 4:		//4  Surveyor's units 
                    return AngularityUnit.Degree;

                default:
                    Debug.Fail("Shouldn't get here with unit = " + database.Insunits);
                    return AngularityUnit.Unknown;
            }
        }
    }
}
