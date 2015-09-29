using System;

namespace Portable.Xaml.Markup
{
	[AttributeUsageAttribute(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Enum | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Interface, AllowMultiple = false, Inherited = true)]
	//[System.Runtime.CompilerServices.TypeForwardedFrom (Consts.AssemblyWindowsBase)]
	public sealed class ValueSerializerAttribute : Attribute
	{
		public Type ValueSerializerType { get; }

		public string ValueSerializerTypeName { get; }
	}
}

