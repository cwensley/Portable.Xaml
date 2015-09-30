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

	class DecimalConverter : BaseNumberConverter
	{
		internal override bool AllowHex { get { return false; } }

		internal override Type NumberType { get { return typeof(decimal); } }

		internal override object FromString(string value, int fromBase)
		{
			return Convert.ToDecimal(value, CultureInfo.InvariantCulture);
		}

		internal override object FromString(string value, NumberFormatInfo formatInfo)
		{
			return decimal.Parse(value, NumberStyles.Float, formatInfo);
		}
	}
	
}
#endif