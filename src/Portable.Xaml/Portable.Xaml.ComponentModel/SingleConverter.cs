#if PCL
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Portable.Xaml.ComponentModel;
using System.Runtime.Serialization;
using System.Reflection;

namespace Portable.Xaml.ComponentModel
{

	public class SingleConverter : BaseNumberConverter
	{
		internal override bool AllowHex { get { return false; } }

		internal override Type NumberType { get { return typeof(float); } }

		internal override object FromString(string value, int fromBase)
		{
			return Convert.ToSingle(value, CultureInfo.InvariantCulture);
		}

		internal override object FromString(string value, NumberFormatInfo formatInfo)
		{
			return float.Parse(value, NumberStyles.Float, formatInfo);
		}

		internal override string ToString(object value, NumberFormatInfo formatInfo)
		{
			return ((float)value).ToString("R", formatInfo);
		}
	}
	
}
#endif