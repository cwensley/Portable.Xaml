#if NETSTANDARD || NET40 || NET45

using System.Runtime.CompilerServices;

[assembly: TypeForwardedTo(typeof(System.ComponentModel.TypeConverterAttribute))]

#else
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Portable.Xaml.ComponentModel;
using System.Runtime.Serialization;
using System.Reflection;

namespace System.ComponentModel
{

	/// <summary>
	/// Type converter attribute, for type converter compatibility in portable class libraries.
	/// </summary>
	public class TypeConverterAttribute : Attribute
	{
		/// <summary>
		/// Gets the name of the type for the type converter of the associated type.
		/// </summary>
		/// <value>The name of the type.</value>
		public string ConverterTypeName { get; private set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="TypeConverterAttribute"/> class.
		/// </summary>
		/// <param name="type">Type of the type converter.</param>
		public TypeConverterAttribute(Type type)
		{
			ConverterTypeName = type.AssemblyQualifiedName;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="TypeConverterAttribute"/> class.
		/// </summary>
		/// <param name="typeName">Type name of the type converter.</param>
		public TypeConverterAttribute(string typeName)
		{
			ConverterTypeName = typeName; 
		}
	}
	
}
#endif