using System;
using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Collections.Generic;

namespace Portable.Xaml.ComponentModel
{

	public class DateTimeConverter : TypeConverter
	{
		public override bool CanConvertFrom (ITypeDescriptorContext context, Type sourceType)
		{
			return sourceType == typeof(string) || base.CanConvertFrom (context, sourceType);
		}

		public override bool CanConvertTo (ITypeDescriptorContext context, Type destinationType)
		{
			return destinationType == typeof(string) || base.CanConvertTo (context, destinationType);
		}

		public override object ConvertFrom (ITypeDescriptorContext context, CultureInfo culture, object value)
		{
			var text = value as string;
			if (text != null)
				return DateTime.Parse (text, culture ?? CultureInfo.CurrentCulture);
			return base.ConvertFrom (context, culture, value);
		}

		public override object ConvertTo (ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
		{
			if (destinationType == typeof(string) && value is DateTime)
            {
                culture = culture ?? CultureInfo.CurrentCulture;

                var date = (DateTime)value;
                var hasTime = date.TimeOfDay.TotalSeconds > 0;
                if (culture == CultureInfo.InvariantCulture)
                    return hasTime ? date.ToString(culture) : date.ToString("yyyy-MM-dd", culture);

                var dateTimeFormat = culture.DateTimeFormat;
                string format = dateTimeFormat.ShortDatePattern;
                if (hasTime)
                    format += " " + dateTimeFormat.ShortTimePattern;

                return date.ToString(format, culture);
            }
			return base.ConvertTo (context, culture, value, destinationType);
		}
	}

}
