using System;
using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Collections.Generic;

namespace Portable.Xaml.ComponentModel
{
	public class Int32Converter : BaseNumberConverter
	{
		internal override Type NumberType { get { return typeof(Int32); } }

		internal override object FromString (string value, int fromBase)
		{
			return Int32.Parse (value, CultureInfo.InvariantCulture);
		}

		internal override object FromString (string value, NumberFormatInfo formatInfo)
		{
			return Int32.Parse (value, formatInfo);
		}
	}

}
