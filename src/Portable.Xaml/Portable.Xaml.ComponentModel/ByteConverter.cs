using System;
using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Collections.Generic;

namespace Portable.Xaml.ComponentModel
{
	public class ByteConverter : BaseNumberConverter
	{
		internal override Type NumberType { get { return typeof(byte); } }

		internal override object FromString (string value, int fromBase)
		{
			return Byte.Parse (value, CultureInfo.InvariantCulture);
		}

		internal override object FromString (string value, NumberFormatInfo formatInfo)
		{
			return Byte.Parse (value, formatInfo);
		}
	}

}
