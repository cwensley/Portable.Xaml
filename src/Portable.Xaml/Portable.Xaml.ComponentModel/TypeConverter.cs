#if NETSTANDARD || NET40 || NET45
using System.Runtime.CompilerServices;

[assembly:TypeForwardedTo(typeof(System.ComponentModel.TypeConverter))]
#else
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Portable.Xaml.ComponentModel;
using System.Runtime.Serialization;
using System.Reflection;

namespace System.ComponentModel
{
	/// <summary>
	/// Type converter implementation, for type converter compatibility in portable class libraries.
	/// </summary>
	public class TypeConverter
	{
		/// <summary>
		/// Determines whether this instance can convert from the specified sourceType.
		/// </summary>
		/// <returns><c>true</c> if this instance can convert from the specified sourceType; otherwise, <c>false</c>.</returns>
		/// <param name="sourceType">Source type to convert from.</param>
		public bool CanConvertFrom(Type sourceType)
		{
			return CanConvertFrom(null, sourceType);
		}

		/// <summary>
		/// Determines whether this instance can convert from the specified context sourceType.
		/// </summary>
		/// <returns><c>true</c> if this instance can convert from the specified context sourceType; otherwise, <c>false</c>.</returns>
		/// <param name="context">Context.</param>
		/// <param name="sourceType">Source type.</param>
		public virtual bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
		{
			return false;
		}

		/// <summary>
		/// Determines whether this instance can convert to the specified destinationType.
		/// </summary>
		/// <returns><c>true</c> if this instance can convert to the specified destinationType; otherwise, <c>false</c>.</returns>
		/// <param name="destinationType">Destination type.</param>
		public bool CanConvertTo(Type destinationType)
		{
			return CanConvertTo(null, destinationType);
		}

		/// <summary>
		/// Determines whether this instance can convert to the specified context destinationType.
		/// </summary>
		/// <returns><c>true</c> if this instance can convert to the specified context destinationType; otherwise, <c>false</c>.</returns>
		/// <param name="context">Context.</param>
		/// <param name="destinationType">Destination type.</param>
		public virtual bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
		{
			return (destinationType == typeof(string));
		}

		/// <summary>
		/// Converts from the specified object.
		/// </summary>
		/// <returns>The converted object.</returns>
		/// <param name="o">Object to convert.</param>
		public object ConvertFrom(object o)
		{
			return ConvertFrom(null, CultureInfo.CurrentCulture, o);
		}

		/// <summary>
		/// Converts the specified <paramref name="value"/>.
		/// </summary>
		/// <returns>The converted value.</returns>
		/// <param name="context">Context.</param>
		/// <param name="culture">Culture.</param>
		/// <param name="value">Value to convert.</param>
		public virtual object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
		{
			return GetConvertFromException(value);
		}

		/// <summary>
		/// Converts from an invariant string.
		/// </summary>
		/// <returns>The from invariant string.</returns>
		/// <param name="text">Text to convert.</param>
		public object ConvertFromInvariantString(string text)
		{
			return ConvertFromInvariantString(null, text); 
		}

		/// <summary>
		/// Converts from an invariant string.
		/// </summary>
		/// <returns>The from invariant string.</returns>
		/// <param name="context">Context.</param>
		/// <param name="text">Text to convert.</param>
		public object ConvertFromInvariantString(ITypeDescriptorContext context, string text)
		{
			return ConvertFromString(context, CultureInfo.InvariantCulture, text);
		}

		/// <summary>
		/// Converts from string.
		/// </summary>
		/// <returns>The converted object.</returns>
		/// <param name="text">Text to convert.</param>
		public object ConvertFromString(string text)
		{
			return ConvertFrom(text);
		}

		/// <summary>
		/// Converts from string.
		/// </summary>
		/// <returns>The converted object.</returns>
		/// <param name="context">Context.</param>
		/// <param name="text">Text to convert.</param>
		public object ConvertFromString(ITypeDescriptorContext context, string text)
		{
			return ConvertFromString(context, CultureInfo.CurrentCulture, text);
		}

		/// <summary>
		/// Converts from string.
		/// </summary>
		/// <returns>The converted object.</returns>
		/// <param name="context">Context.</param>
		/// <param name="culture">Culture.</param>
		/// <param name="text">Text to convert.</param>
		public object ConvertFromString(ITypeDescriptorContext context, CultureInfo culture, string text)
		{
			return ConvertFrom(context, culture, text);
		}

		/// <summary>
		/// Converts to the specified type.
		/// </summary>
		/// <returns>The converted object.</returns>
		/// <param name="value">Value to convert.</param>
		/// <param name="destinationType">Destination type to convert to.</param>
		public object ConvertTo(object value, Type destinationType)
		{
			return ConvertTo(null, null, value, destinationType);
		}

		/// <summary>
		/// Converts to the specified type.
		/// </summary>
		/// <returns>The converted object.</returns>
		/// <param name="context">Context.</param>
		/// <param name="culture">Culture.</param>
		/// <param name="value">Value to convert.</param>
		/// <param name="destinationType">Destination type to convert to.</param>
		public virtual object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value,
		                                Type destinationType)
		{
			// context?
			if (destinationType == null)
				throw new ArgumentNullException("destinationType");

			if (destinationType == typeof(string))
			{
				if (value == null)
					return String.Empty;

				if (culture != null)
					return Convert.ToString(value, culture);

				return value.ToString();
			}

			return GetConvertToException(value, destinationType);
		}

		/// <summary>
		/// Converts to an invariant string.
		/// </summary>
		/// <returns>The converted string.</returns>
		/// <param name="value">Value to convert.</param>
		public string ConvertToInvariantString(object value)
		{
			return ConvertToInvariantString(null, value);
		}

		/// <summary>
		/// Converts to invariant string.
		/// </summary>
		/// <returns>The converted string.</returns>
		/// <param name="context">Context.</param>
		/// <param name="value">Value to convert.</param>
		public string ConvertToInvariantString(ITypeDescriptorContext context, object value)
		{
			return (string)ConvertTo(context, CultureInfo.InvariantCulture, value, typeof(string));
		}

		/// <summary>
		/// Converts to a string.
		/// </summary>
		/// <returns>The converted string.</returns>
		/// <param name="value">Value to convert.</param>
		public string ConvertToString(object value)
		{
			return (string)ConvertTo(null, CultureInfo.CurrentCulture, value, typeof(string));
		}

		/// <summary>
		/// Converts to a string.
		/// </summary>
		/// <returns>The converted string.</returns>
		/// <param name="context">Context.</param>
		/// <param name="value">Value to convert.</param>
		public string ConvertToString(ITypeDescriptorContext context, object value)
		{
			return (string)ConvertTo(context, CultureInfo.CurrentCulture, value, typeof(string));
		}

		/// <summary>
		/// Converts to a string.
		/// </summary>
		/// <returns>The converted string.</returns>
		/// <param name="context">Context.</param>
		/// <param name="culture">Culture.</param>
		/// <param name="value">Value to convert.</param>
		public string ConvertToString(ITypeDescriptorContext context, CultureInfo culture, object value)
		{
			return (string)ConvertTo(context, culture, value, typeof(string));
		}

		/// <summary>
		/// Gets the exception when converting from a value.
		/// </summary>
		/// <returns>The convert from exception.</returns>
		/// <param name="value">Value converted from.</param>
		protected Exception GetConvertFromException(object value)
		{
			string destinationType;
			if (value == null)
				destinationType = "(null)";
			else
				destinationType = value.GetType().FullName;

			throw new NotSupportedException(string.Format(CultureInfo.CurrentCulture, "{0} cannot convert from {1}.", GetType().Name, destinationType));
		}

		/// <summary>
		/// Gets the exception when converting to a value.
		/// </summary>
		/// <returns>The convert to exception.</returns>
		/// <param name="value">Value converting to.</param>
		/// <param name="destinationType">Destination type.</param>
		protected Exception GetConvertToException(object value, Type destinationType)
		{
			string sourceType;
			if (value == null)
				sourceType = "(null)";
			else
				sourceType = value.GetType().FullName;

			throw new NotSupportedException(string.Format(CultureInfo.CurrentCulture, "'{0}' is unable to convert '{1}' to '{2}'.", GetType().Name, sourceType, destinationType.FullName));
		}

		/// <summary>
		/// Determines whether this instance is valid the specified value.
		/// </summary>
		/// <returns><c>true</c> if this instance is valid the specified value; otherwise, <c>false</c>.</returns>
		/// <param name="value">Value.</param>
		public bool IsValid(object value)
		{
			return IsValid(null, value);
		}

		/// <summary>
		/// Determines whether this instance is valid the specified context value.
		/// </summary>
		/// <returns><c>true</c> if this instance is valid the specified context value; otherwise, <c>false</c>.</returns>
		/// <param name="context">Context.</param>
		/// <param name="value">Value.</param>
		public virtual bool IsValid(ITypeDescriptorContext context, object value)
		{
			if (value == null)
				return false;

			if (!CanConvertFrom(context, value.GetType()))
				return false;

			try
			{
				ConvertFrom(context, CultureInfo.InvariantCulture, value);
				return true;
			}
			catch
			{
				return false;
			}
		}
	}
}
#endif