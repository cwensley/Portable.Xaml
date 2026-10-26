using System;
using System.Runtime.Serialization;

namespace Portable.Xaml.Yaml
{
	[EnhancedXaml]
	public class XamlYamlWriterException : XamlException
	{
		public XamlYamlWriterException()
			: this("Yaml writer error")
		{
		}

		public XamlYamlWriterException(string message)
			: this(message, null)
		{
		}

		public XamlYamlWriterException(string message, Exception innerException)
			: base(message, innerException)
		{
		}

#if HAS_SERIALIZATION_INFO
		protected XamlYamlWriterException(SerializationInfo info, StreamingContext context)
			: base(info, context)
		{
		}
#endif
	}
}
