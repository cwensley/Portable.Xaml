using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.Linq;

namespace Portable.Xaml.ComponentModel
{
	class TypeAttributeProvider : ICustomAttributeProvider
	{
		readonly Type type;

		public TypeAttributeProvider(Type type)
		{
			this.type = type;
		}

		public object[] GetCustomAttributes(bool inherit)
		{
			return type.GetTypeInfo().GetCustomAttributes(inherit).ToArray();
		}

		public object[] GetCustomAttributes(Type attributeType, bool inherit)
		{
			return type.GetTypeInfo().GetCustomAttributes(attributeType, inherit).ToArray();
		}

		public bool IsDefined(Type attributeType, bool inherit)
		{
			return type.GetTypeInfo().IsDefined(attributeType, inherit);
		}
	}

	class MemberAttributeProvider : ICustomAttributeProvider
	{
		readonly MemberInfo info;

		public MemberAttributeProvider(MemberInfo info)
		{
			this.info = info;
		}

		public object[] GetCustomAttributes(bool inherit)
		{
			return info.GetCustomAttributes(inherit).ToArray();
		}

		public object[] GetCustomAttributes(Type attributeType, bool inherit)
		{
			return info.GetCustomAttributes(attributeType, inherit).ToArray();
		}

		public bool IsDefined(Type attributeType, bool inherit)
		{
			return info.IsDefined(attributeType, inherit);
		}
	}

	public interface ICustomAttributeProvider
	{
		object[] GetCustomAttributes(bool inherit);

		object[] GetCustomAttributes(Type attributeType, bool inherit);

		bool IsDefined(Type attributeType, bool inherit);
	}
}
