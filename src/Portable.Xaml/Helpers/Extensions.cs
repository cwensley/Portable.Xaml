using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Linq.Expressions;

namespace Portable.Xaml
{
	static class Extensions
	{
		public static IList<T> ToReadOnly<T>(this IEnumerable<T> enumerable)
		{
			return new ReadOnlyCollection<T>(enumerable.ToList());
		}
	}
}

