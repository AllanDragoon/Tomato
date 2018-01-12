using System;
using System.Linq;
using System.Globalization;
using System.IO;
using System.Xml.XPath;
using System.Text.RegularExpressions;

namespace DbxUtils.Units
{
    public static class ParameterUtils
    {
        /// <summary>
        /// Digits to compose numbers. Do not support such formats as "1E-6", "123,456", etc. -Why? -Why not?
        /// </summary>
        private static readonly string numberDigits = /*MSG0*/"0123456789+-.";

        /// <summary>
        /// A weak parser to get value and unit from an expression
        /// </summary>
        /// <param name="expression"></param>
        /// <param name="value"></param>
        /// <param name="unit"></param>
        public static void GetValueAndUnit(string expression, out double value, out string unit)
        {
            string valueText = String.Empty;
            unit = String.Empty;

            for (int i = 0; i < expression.Length; ++i)
            {
                if (numberDigits.Contains(expression[i]))
                {
                    valueText += expression[i];
                }
                else
                {
                    unit = expression.Substring(i);
                    unit = unit.Trim();
                    break;
                }
            }

            //We need use the InvariantCulture in parse to avoid the case that parse "53.1" to 531 in some locale (such as German,which doesn't treat '.' as decimal point)
            value = double.Parse(valueText, NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        /// <summary>
        ///  Evaluate mathematical expression
        ///  Refer to: http://bytes.com/topic/c-sharp/answers/260838-how-evaluate-c-string-expression
        /// </summary>
        public static double Evaluate(string expression)
        {
            Regex regex = new Regex((@"([\+\-\*])"));
            string processedExpression = regex.Replace(expression, " ${1} ");
            processedExpression = processedExpression.Replace("/", " div ");
            processedExpression = processedExpression.Replace("%", " mod ");

            processedExpression = string.Format("number({0})", processedExpression);

            StringReader stringReader = new StringReader("<r/>");
            XPathDocument xPathDoc = new XPathDocument(stringReader);
            XPathNavigator navigator = xPathDoc.CreateNavigator();
            return (double)navigator.Evaluate(processedExpression);
        }
    }
}
