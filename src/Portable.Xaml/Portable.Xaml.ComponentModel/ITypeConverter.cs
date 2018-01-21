using System;
using System.Globalization;

namespace Portable.Xaml.ComponentModel
{
#if !HAS_TYPE_CONVERTER

	interface ITypeDescriptorContext : IServiceProvider
	{
		object Instance { get; }

		void OnComponentChanged();

		bool OnComponentChanging();
	}

#endif
}