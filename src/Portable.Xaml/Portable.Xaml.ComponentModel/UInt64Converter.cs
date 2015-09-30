using System;
using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Collections.Generic;

namespace Portable.Xaml.ComponentModel
{

	public class UInt64Converter : BaseNumberConverter
	{
		internal override Type NumberType { get { return typeof(UInt64); } }

		internal override object FromString (string value, int fromBase)
		{
			return UInt64.Parse (value, CultureInfo.InvariantCulture);
		}

		internal override object FromString (string value, NumberFormatInfo formatInfo)
		{
			return UInt64.Parse (value, formatInfo);
		}
	}

}
