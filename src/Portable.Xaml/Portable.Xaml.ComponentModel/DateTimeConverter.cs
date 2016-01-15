using System;
using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Portable.Xaml.ComponentModel
{

	public class DateTimeConverter : TypeConverter
	{
		private static readonly Regex UtcTidyUpPattern = new Regex(@"0+Z$");

		private static readonly Regex LocalTidyUpPatterns = new Regex(@"0+$");

		public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
		{
			return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
		}

		public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
		{
			return destinationType == typeof(string) || base.CanConvertTo(context, destinationType);
		}

		public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
		{
			var text = value as string;
			if (text != null)
				return DateTime.Parse(text, culture ?? CultureInfo.CurrentCulture);
			return base.ConvertFrom(context, culture, value);
		}

		public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
		{
			if (destinationType == typeof(string) && value is DateTime)
			{
				culture = culture ?? CultureInfo.CurrentCulture;

				var date = (DateTime)value;
				var hasTime = date.TimeOfDay.TotalSeconds > 0;
				if (culture == CultureInfo.InvariantCulture)
				{
					if (hasTime)
					{
						// To exactly match the behaviour of System.Xaml unnecessary zeros must be removed from the end of the time.
						return RemoveUnnecessaryZeros(date.ToString("o", culture), date.Kind);
					}

					return date.ToString("yyyy-MM-dd", culture);
				}

				var dateTimeFormat = culture.DateTimeFormat;
				string format = dateTimeFormat.ShortDatePattern;
				if (hasTime)
					format += " " + dateTimeFormat.ShortTimePattern;

				return date.ToString(format, culture);
			}
			return base.ConvertTo(context, culture, value, destinationType);
		}

		private string RemoveUnnecessaryZeros(string dateFormatted, DateTimeKind kind)
		{
			string conciseDate = dateFormatted.Trim();
			if (kind == DateTimeKind.Utc)
			{
				conciseDate = UtcTidyUpPattern.Replace(conciseDate, "Z");
				if (conciseDate.EndsWith(".Z"))
					conciseDate = conciseDate.Remove(conciseDate.Length - 2, 1);
			}
			else
			{
				conciseDate = LocalTidyUpPatterns.Replace(conciseDate, "");
				if (conciseDate.EndsWith("."))
					conciseDate = conciseDate.Substring(0, conciseDate.Length - 1);
			}

			return conciseDate;
		}
	}
}
