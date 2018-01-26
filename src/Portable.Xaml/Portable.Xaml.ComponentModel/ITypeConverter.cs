using System;
using System.Globalization;
using System.ComponentModel;

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