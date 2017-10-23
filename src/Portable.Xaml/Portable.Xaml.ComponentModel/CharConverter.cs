#if !NETSTANDARD
using System;
using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Collections.Generic;
using System.ComponentModel;

namespace Portable.Xaml.ComponentModel
{

	public class CharConverter : TypeConverter
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
			if (text != null && text.Length == 1) {
				return text [0];
			}
			return base.ConvertFrom (context, culture, value);
		}

		public override object ConvertTo (ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
		{
			if (destinationType == typeof(string) && value is char)
				return ((char)value).ToString();
			return base.ConvertTo (context, culture, value, destinationType);
		}
	}

}
#endif