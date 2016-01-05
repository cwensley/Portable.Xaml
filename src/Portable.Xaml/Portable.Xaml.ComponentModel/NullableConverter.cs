using System;
using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Collections.Generic;

namespace Portable.Xaml.ComponentModel
{

	public class NullableConverter : TypeConverter
	{
		public Type UnderlyingType { get; private set; }

		public TypeConverter UnderlyingTypeConverter { get; private set; }

		public Type NullableType { get; private set; }

		public NullableConverter(Type type)
		{
			NullableType = type;
			UnderlyingType = Nullable.GetUnderlyingType(type);
			if (UnderlyingType == null)
			{
				throw new ArgumentException("Specified type is not nullable", "type");
			}
			UnderlyingTypeConverter = TypeDescriptor.GetConverter(this.UnderlyingType);
		}

		public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
		{
			if (UnderlyingType.GetTypeInfo().IsAssignableFrom(sourceType.GetTypeInfo()))
				return true;
			
			if (UnderlyingTypeConverter != null)
				return UnderlyingTypeConverter.CanConvertFrom(context, sourceType);
			
			return base.CanConvertFrom(context, sourceType);
		}

		public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
		{
			if (value == null || UnderlyingType.GetTypeInfo().IsAssignableFrom(value.GetType().GetTypeInfo()))
				return value;
			
			if (value is string && string.IsNullOrEmpty(value as string))
				return null;
			
			if (UnderlyingTypeConverter != null)
				return UnderlyingTypeConverter.ConvertFrom(context, culture, value);
			return base.ConvertFrom(context, culture, value);
		}

		public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
		{
			if (destinationType == null) throw new ArgumentNullException(nameof(destinationType));

			if (destinationType == typeof (string) && UnderlyingTypeConverter != null && value != null)
			{
				return UnderlyingTypeConverter.ConvertTo(context, culture, value, destinationType);
			}
			return GetConvertToException(value, destinationType);
		}
	}
}
