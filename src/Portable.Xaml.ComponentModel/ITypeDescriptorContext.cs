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
	/// Interface for a type descriptor context, for type converter compatibility in portable class libraries.
	/// </summary>
	public interface ITypeDescriptorContext : IServiceProvider
	{
	}
	
}
#endif