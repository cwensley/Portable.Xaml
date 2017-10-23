#if NETSTANDARD2_0 || NET40 || NET45
using System.Runtime.CompilerServices;

[assembly:TypeForwardedTo(typeof(System.ComponentModel.ISupportInitialize))]
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
	/// Interface for objects that support initialization.
	/// </summary>
	public interface ISupportInitialize
	{
		/// <summary>
		/// Called before initialization from serialization.
		/// </summary>
		void BeginInit();

		/// <summary>
		/// Called after initialization from serialization.
		/// </summary>
		void EndInit();
	}
	
}
#endif