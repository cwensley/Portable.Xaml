using System;
using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Collections.Generic;

namespace Portable.Xaml.ComponentModel
{

	public class BoolConverter : TypeConverter
	{
		public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
		{
			return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
		}

		public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
		{
			var text = value as string;
			if (text != null)
			{
				bool result;
				if (bool.TryParse(text, out result))
					return result;
			}
			return base.ConvertFrom(context, culture, value);
		}
	}

}
