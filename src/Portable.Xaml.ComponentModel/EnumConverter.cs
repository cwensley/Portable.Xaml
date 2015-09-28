using System;
using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Collections.Generic;

namespace Portable.Xaml.ComponentModel
{
	public class EnumConverter : TypeConverter
	{
		public Type Type { get; private set; }

		public EnumConverter(Type type)
		{
			Type = type;
		}

		public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
		{
			return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
		}

		public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
		{
			var text = value as string;
			if (text != null)
			{
				try
				{
					if (text.IndexOf(',') == -1)
						return Enum.Parse(Type, text, true);
					
					long enumValue = 0L;
					var array = text.Split(',');
					for (int i = 0; i < array.Length; i++)
					{
						string value2 = array[i];
						enumValue |= Convert.ToInt64((Enum)Enum.Parse(Type, value2, true), culture);
					}

					return Enum.ToObject(Type, enumValue);
				}
				catch (Exception innerException)
				{
					throw new FormatException("Invalid Primitive", innerException);
				}
			}
			return base.ConvertFrom(context, culture, value);
		}
	}

}

