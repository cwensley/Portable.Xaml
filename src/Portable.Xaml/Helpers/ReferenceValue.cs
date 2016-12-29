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
using System.Runtime.CompilerServices;

namespace Portable.Xaml
{
	/// <summary>
	/// Struct to store a reference type and cache its value (even if null).
	/// </summary>
	struct ReferenceValue<T>
		where T: class
	{
		object value;

		static readonly object NullValue = new object();

		public T Value
		{
			get
			{ 
				if (ReferenceEquals(value, NullValue))
					return default(T);
				return (T)value;
			} 
		}

		public bool HasValue { get { return value != null; } }

		public ReferenceValue(T value)
		{
			this.value = value ?? NullValue;
		}

#if PCL259
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
		public T Get(Func<T> getValue)
		{
			if (ReferenceEquals(value, NullValue))
				return default(T);
			if (value != null)
				return (T)value;
			
			value = getValue();
			if (value == null)
			{
				value = NullValue;
				return default(T);
			}
			return (T)value;
		}

		public static implicit operator ReferenceValue<T>(T value)
		{
			return new ReferenceValue<T>(value);
		}

		public static bool operator ==(ReferenceValue<T> left, ReferenceValue<T> right)
		{
			return Equals(left.value, right.value);
		}

		public static bool operator !=(ReferenceValue<T> left, ReferenceValue<T> right)
		{
			return !Equals(left.value, right.value);
		}

		public override bool Equals(object obj)
		{
			return obj is ReferenceValue<T> && this == (ReferenceValue<T>)obj;
		}

		public override int GetHashCode()
		{
			return value?.GetHashCode() ?? base.GetHashCode();
		}
	}

}
