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

	/// <summary>
	/// Type descriptor for conversion compatibility.
	/// </summary>
	public static class TypeDescriptor
	{
		static readonly Dictionary<Type, Type> converters = new Dictionary<Type, Type>
		{
			{ typeof(bool), typeof(BoolConverter) }
		};

		/// <summary>
		/// Gets the type converter for the specified type.
		/// </summary>
		/// <returns>The type converter, or null if the type has no defined converter.</returns>
		/// <param name="type">Type to get the converter for.</param>
		public static TypeConverter GetConverter(Type type)
		{
			var attr = type.GetTypeInfo().GetCustomAttribute<TypeConverterAttribute>();
			Type converterType = null;
			if (attr != null)
				converterType = Type.GetType(attr.ConverterTypeName);
			if (converterType == null)
			{
				if (!converters.TryGetValue(type, out converterType))
				{
					if (type.GetTypeInfo().IsGenericType && type.GetTypeInfo().GetGenericTypeDefinition() == typeof(Nullable<>))
						return new NullableConverter(type);
					if (type.GetTypeInfo().IsEnum)
						return new EnumConverter(type);
				}
			}
			
			if (converterType != null)
			{
				if (converterType.GetTypeInfo().DeclaredConstructors.Any(r => r.GetParameters().Select(p => p.ParameterType).SequenceEqual(new [] { typeof(Type) })))
					return Activator.CreateInstance(converterType, type) as TypeConverter;
				return Activator.CreateInstance(converterType) as TypeConverter;
			}
			
			return null;
		}
	}
	
}
#endif