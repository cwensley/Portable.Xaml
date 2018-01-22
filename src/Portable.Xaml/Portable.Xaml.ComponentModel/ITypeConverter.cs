using System;
using System.Globalization;
using System.ComponentModel;

namespace Portable.Xaml.ComponentModel
{
	[EnhancedXaml]
	public interface IXamlTypeConverter
	{
		bool CanConvertFrom(object context, Type sourceType);
		bool CanConvertTo(object context, Type destinationType);
		object ConvertFrom(object context, CultureInfo culture, object value);
		object ConvertTo(object context, CultureInfo culture, object value, Type destinationType);
	}

	class XamlTypeConverter : TypeConverter
	{
		IXamlTypeConverter _converter;

		public XamlTypeConverter(IXamlTypeConverter converter)
		{
			_converter = converter ?? throw new ArgumentNullException(nameof(converter));
		}

		public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
		{
			return _converter.CanConvertFrom(context, sourceType);
		}

		public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
		{
			return _converter.CanConvertTo(context, destinationType);
		}

		public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
		{
			return _converter.ConvertFrom(context, culture, value);
		}

		public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
		{
			return _converter.ConvertTo(context, culture, value, destinationType);
		}
	}


#if !HAS_TYPE_CONVERTER

	interface ITypeDescriptorContext : IServiceProvider
	{
		object Instance { get; }

		void OnComponentChanged();

		bool OnComponentChanging();
	}

#endif
}