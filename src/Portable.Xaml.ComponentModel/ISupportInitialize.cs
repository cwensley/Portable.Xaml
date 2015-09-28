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