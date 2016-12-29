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
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace Portable.Xaml.Schema
{
	public class XamlMemberInvoker
	{
		static readonly XamlMemberInvoker unknown = new XamlMemberInvoker();

		public static XamlMemberInvoker UnknownInvoker => unknown;

		protected XamlMemberInvoker()
		{
		}

		public XamlMemberInvoker(XamlMember member)
		{
			if (member == null)
				throw new ArgumentNullException("member");
			Member = member;
		}

		[EnhancedXaml]
		protected XamlMember Member { get; }

		public MethodInfo UnderlyingGetter => Member?.UnderlyingGetter;

		public MethodInfo UnderlyingSetter => Member?.UnderlyingSetter;

#if PCL259
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
		void ThrowIfUnknownOrDirective()
		{
			if (Member == null)
				throw new NotSupportedException("Current operation is invalid for unknown member.");
			if (Member.IsDirective)
				throw new NotSupportedException($"not supported operation on directive member {Member}");
		}

		public virtual object GetValue(object instance)
		{
			if (instance == null)
				throw new ArgumentNullException(nameof(instance));

#if USE_EXPRESSIONS
			if (getDelegate != null)
				return getDelegate(instance);

			ThrowIfUnknownOrDirective();
			var getter = UnderlyingGetter;
			if (getter == null)
				throw new NotSupportedException($"Attempt to get value from write-only property or event {Member}");

			getDelegate = getter.BuildGetExpression();

			return getDelegate(instance);
#else
			ThrowIfUnknownOrDirective();
			var getter = UnderlyingGetter;
			if (getter == null)
				throw new NotSupportedException($"Attempt to get value from write-only property or event {Member}");

			return getter.Invoke(instance, null);
#endif
		}

		public virtual void SetValue(object instance, object value)
		{
			if (instance == null)
				throw new ArgumentNullException(nameof(instance));

#if USE_EXPRESSIONS
			if (setDelegate != null)
			{
				setDelegate(instance, value);
				return;
			}

			ThrowIfUnknownOrDirective();
			var setter = UnderlyingSetter;
			if (setter == null)
				throw new NotSupportedException($"Attempt to set value from read-only property {Member}");
			setDelegate = setter.BuildSetExpression();
				setDelegate(instance, value);
#else
			ThrowIfUnknownOrDirective();
			var setter = UnderlyingSetter;
			if (setter == null)
				throw new NotSupportedException($"Attempt to set value from read-only property {Member}");

			if (Member.IsAttachable)
				setter.Invoke(null, new object [] { instance, value });
			else
				setter.Invoke(instance, new object[] { value });
#endif
		}

#if USE_EXPRESSIONS
		Func<object, object> getDelegate;
		Action<object, object> setDelegate;
#endif

		public virtual ShouldSerializeResult ShouldSerializeValue(object instance)
		{
			throw new NotImplementedException();
		}
	}
}
