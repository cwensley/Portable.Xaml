using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Portable.Xaml
{
	static class ReflectionHelpers
	{
		static Assembly componentModelAssembly = typeof(System.ComponentModel.CancelEventArgs).GetTypeInfo().Assembly;
		static Assembly corlibAssembly = typeof(int).GetTypeInfo().Assembly;

		public static readonly Type TypeConverterType = ReflectionHelpers.GetComponentModelType("System.ComponentModel.TypeConverter");

		public static Type GetComponentModelType(string name) => componentModelAssembly.GetType(name);
		public static Type GetCorlibType(string name) => corlibAssembly.GetType(name);
	}
}
