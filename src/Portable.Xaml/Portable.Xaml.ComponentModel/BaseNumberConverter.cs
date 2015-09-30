#if PCL
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Portable.Xaml.ComponentModel;
using System.Runtime.Serialization;
using System.Reflection;

namespace Portable.Xaml.ComponentModel
{

	abstract class BaseNumberConverter : TypeConverter
	{
		internal virtual bool AllowHex { get { return true; } }

		internal abstract Type NumberType { get; }

		internal abstract object FromString(string value, int fromBase);

		internal abstract object FromString(string value, NumberFormatInfo formatInfo);

		internal virtual string ToString(object value, NumberFormatInfo formatInfo)
		{
			return Convert.ToString (value, formatInfo);
		}

		public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
		{
			return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
		}

		public override bool CanConvertTo (ITypeDescriptorContext context, Type destinationType)
		{
			return destinationType == typeof(string) || base.CanConvertTo (context, destinationType);
		}

		public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
		{
			var str = value as string;
			if (str != null)
			{
				string text = str.Trim();
				try
				{
					object result;
					if (AllowHex && text[0] == '#')
					{
						result = FromString(text.Substring(1), 16);
						return result;
					}
					if ((AllowHex && text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) || text.StartsWith("&h", StringComparison.OrdinalIgnoreCase))
					{
						result = FromString(text.Substring(2), 16);
						return result;
					}
					culture = culture ?? CultureInfo.CurrentCulture;
					var formatInfo = (NumberFormatInfo)culture.GetFormat(typeof(NumberFormatInfo));
					result = FromString(text, formatInfo);
					return result;
				}
				catch (InvalidOperationException innerException)
				{
					throw new InvalidOperationException(text, innerException);
				}
				catch (ArgumentNullException innerException)
				{
					throw new ArgumentNullException(text, innerException);
				}
				catch (ArgumentOutOfRangeException innerException)
				{
					throw new ArgumentOutOfRangeException(text, innerException);
				}
				catch (ArgumentException innerException)
				{
					throw new ArgumentException(text, innerException);
				}
				catch (Exception innerException)
				{
					throw new Exception(text, innerException);
				}
			}
			return base.ConvertFrom(context, culture, value);
		}

		public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
		{
			if (destinationType == null)
				throw new ArgumentNullException("destinationType");

			if (destinationType == typeof(string) && value != null && NumberType.GetTypeInfo().IsAssignableFrom(value.GetType().GetTypeInfo()))
			{
				culture = culture ?? CultureInfo.CurrentCulture;
				var formatInfo = (NumberFormatInfo)culture.GetFormat(typeof(NumberFormatInfo));
				return ToString(value, formatInfo);
			}
			return base.ConvertTo(context, culture, value, destinationType);
		}
	}
	
}
#endif