using System;
using System.Globalization;
using System.Reflection;
using System.Linq;

namespace Portable.Xaml.ComponentModel
{
	class EventConverter : TypeConverter
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
				var rootObjectProvider = context.GetService(typeof(IRootObjectProvider)) as IRootObjectProvider;
				var destinationTypeProvider = context.GetService(typeof(IDestinationTypeProvider)) as IDestinationTypeProvider;
				if (rootObjectProvider != null && destinationTypeProvider != null) {
					var target = rootObjectProvider.RootObject;
					var eventType = destinationTypeProvider.GetDestinationType ();
					var eventMethodParams = eventType.GetRuntimeMethods().First(r => r.Name == "Invoke").GetParameters ().Select(r => r.ParameterType).ToArray();

					var mi = target.GetType().GetRuntimeMethods().LastOrDefault(r => r.Name == text && r.GetParameters().Select(p => p.ParameterType).SequenceEqual(eventMethodParams));
					if (mi == null)
						throw new XamlObjectWriterException (String.Format ("Referenced value method {0} in type {1} indicated by event {2} was not found", text, target.GetType(), eventType.FullName));
					
					return mi.CreateDelegate(eventType, target);
				}
			}
			return base.ConvertFrom(context, culture, value);
		}
	}
}

