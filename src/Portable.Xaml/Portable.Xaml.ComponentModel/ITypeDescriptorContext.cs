#if NETSTANDARD || NET40 || NET45
using System.Runtime.CompilerServices;

[assembly: TypeForwardedTo(typeof(System.ComponentModel.ITypeDescriptorContext))]
#endif

#if PCL && !NETSTANDARD
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
	/// Interface for a type descriptor context, for type converter compatibility in portable class libraries.
	/// </summary>
	public interface ITypeDescriptorContext : IServiceProvider
	{
		IContainer Container { get; }

		object Instance { get; }

		PropertyDescriptor PropertyDescriptor { get; }
	}

	public interface IContainer { }

	public abstract class MemberDescriptor { }

	public abstract class PropertyDescriptor : MemberDescriptor { }
}
#endif