using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.Linq;

#if NETSTANDARD2_0 || NET_451
[assembly:TypeForwardedTo(typeof(System.Reflection.ICustomAttributeProvider))]
#else
namespace System.Reflection
{
	public interface ICustomAttributeProvider
	{
		object[] GetCustomAttributes(bool inherit);

		object[] GetCustomAttributes(Type attributeType, bool inherit);

		bool IsDefined(Type attributeType, bool inherit);
	}
}
#endif

namespace Portable.Xaml.ComponentModel
{
	class TypeAttributeProvider : System.Reflection.ICustomAttributeProvider
	{
		readonly Type type;

		public TypeAttributeProvider(Type type)
		{
			this.type = type;
		}

		public object[] GetCustomAttributes(bool inherit)
		{
			var attr = type.GetTypeInfo().GetCustomAttributes(inherit).ToArray();
			return (attr as object[]) ?? attr.ToArray();
		}

		public object[] GetCustomAttributes(Type attributeType, bool inherit)
		{
			var attr = type.GetTypeInfo().GetCustomAttributes(attributeType, inherit);
			return (attr as object[]) ?? attr.ToArray();
		}

		public bool IsDefined(Type attributeType, bool inherit)
		{
			return type.GetTypeInfo().IsDefined(attributeType, inherit);
		}
	}

	class MemberAttributeProvider : System.Reflection.ICustomAttributeProvider
	{
		readonly MemberInfo info;

		public MemberAttributeProvider(MemberInfo info)
		{
			this.info = info;
		}

		public object[] GetCustomAttributes(bool inherit)
		{
			var attr = info.GetCustomAttributes(inherit).ToArray();
			return (attr as object[]) ?? attr.ToArray();
		}

		public object[] GetCustomAttributes(Type attributeType, bool inherit)
		{
			var attr = info.GetCustomAttributes(attributeType, inherit);
			return (attr as object[]) ?? attr.ToArray();
		}

		public bool IsDefined(Type attributeType, bool inherit)
		{
			return info.IsDefined(attributeType, inherit);
		}
	}
}