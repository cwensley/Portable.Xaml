//
// Copyright (C) 2010 Novell Inc. http://novell.com
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Portable.Xaml.Schema
{
	public class XamlMemberInvoker
	{
		static readonly XamlMemberInvoker unknown = new XamlMemberInvoker();
		Type _targetType;
		Type _propertyType;
		Func<object, object> getDelegate;
		Action<object, object> setDelegate;

		public static XamlMemberInvoker UnknownInvoker => unknown;

		[EnhancedXaml]
		protected XamlMember Member { get; }

		public MethodInfo UnderlyingGetter => Member?.UnderlyingGetter;

		public MethodInfo UnderlyingSetter => Member?.UnderlyingSetter;

		Type TargetType => _targetType ?? (_targetType = Member.TargetType.UnderlyingType);

		Type PropertyType => _propertyType ?? (_propertyType = Member.Type.UnderlyingType);

		protected XamlMemberInvoker()
		{
		}

		public XamlMemberInvoker(XamlMember member)
		{
			if (member == null)
				throw new ArgumentNullException("member");
			Member = member;
		}

		public virtual object GetValue(object instance)
		{
			if (instance == null)
				throw new ArgumentNullException(nameof(instance));

			if (getDelegate != null)
			{
				// all checks already done
				return getDelegate(instance);
			}

			if (Member == null)
				throw new NotSupportedException("Current operation is invalid for unknown member.");

			if (Member.IsDirective)
				throw new NotSupportedException($"not supported operation on directive member {Member}");

			var getter = UnderlyingGetter;
			if (getter == null)
				throw new NotSupportedException($"Attempt to get value from write-only property or event {Member}");

			var mode = Member.SchemaContext.InvokerOptions;
			if (mode.HasFlag(XamlInvokerOptions.DeferCompile))
			{
				getDelegate = i => getter.Invoke(i, null);
				Task.Factory.StartNew(() => getDelegate = getter.BuildGetExpression());
			}
			else if (mode.HasFlag(XamlInvokerOptions.Compile))
			{
				getDelegate = getter.BuildGetExpression();
			}
			else
			{
				getDelegate = i => getter.Invoke(i, null);
			}


			return getDelegate(instance);
		}

		public virtual void SetValue(object instance, object value)
		{
			if (instance == null)
				throw new ArgumentNullException(nameof(instance));

			if (setDelegate != null)
			{
				// all checks already done
				setDelegate(instance, value);
				return;
			}

			if (Member == null)
				throw new NotSupportedException("Current operation is invalid for unknown member.");

			if (Member.IsDirective)
				throw new NotSupportedException($"not supported operation on directive member {Member}");

			var setter = UnderlyingSetter;
			if (setter == null)
				throw new NotSupportedException($"Attempt to set value from read-only property {Member}");

			var mode = Member.SchemaContext.InvokerOptions;
			if (mode.HasFlag(XamlInvokerOptions.DeferCompile))
			{
				CreateSetDelegate(setter);
				Task.Factory.StartNew(() => setDelegate = setter.BuildCallExpression());
			}
			else if (mode.HasFlag(XamlInvokerOptions.Compile))
			{
				setDelegate = setter.BuildCallExpression();
			}
			else
			{
				CreateSetDelegate(setter);
			}

			setDelegate(instance, value);
		}

		void CreateSetDelegate(MethodInfo setter)
		{
			if (Member.IsAttachable)
				setDelegate = (i, v) => setter.Invoke(null, new object[] { i, v });
			else
				setDelegate = (i, v) => setter.Invoke(i, new object[] { v });
		}

		public virtual ShouldSerializeResult ShouldSerializeValue(object instance)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Gets a value indicating that the instance is considered the default value of the member.
		/// </summary>
		/// <remarks>
		/// This uses the DefaultValueAttribute normally, but for immutable structs this is also useful to  define that 
		/// the value is default.
		/// 
		/// E.g. for immutable collections, this uses the IsDefault property to determine if it should be written to xaml.
		/// </remarks>
		/// <returns><c>true</c>, if the instance is the default value, <c>false</c> otherwise.</returns>
		/// <param name="instance">instance of the object to test if it is default.</param>
		[EnhancedXaml]
		public virtual bool IsDefaultValue(object instance)
		{
			if (Member == null)
				return false;
			if (Member.DefaultValue != null)
				return Equals(Member.DefaultValue.Value, instance);
			if (Member.Type?.IsMutableDefault(instance) == true)
				return true;
			return false;
		}
	}
}
