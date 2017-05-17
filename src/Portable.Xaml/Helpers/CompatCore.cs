#if NETSTANDARD
using System;
namespace Portable.Xaml
{
	[System.AttributeUsage(AttributeTargets.All, Inherited = false, AllowMultiple = true)]
	sealed class SerializableAttribute : Attribute
	{
	}
}

#endif