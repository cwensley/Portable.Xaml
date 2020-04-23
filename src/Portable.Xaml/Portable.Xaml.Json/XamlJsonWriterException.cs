using System;

namespace Portable.Xaml.Json
{
	[EnhancedXaml]
	public class XamlJsonWriterException : XamlException
	{
		public XamlJsonWriterException()
			: this("Json writer error")
		{
		}

		public XamlJsonWriterException(string message)
			: this(message, null)
		{
		}

		public XamlJsonWriterException(string message, Exception innerException)
			: base(message, innerException)
		{
		}

#if !PCL
		protected XamlJsonWriterException(SerializationInfo info, StreamingContext context)
			: base(info, context)
		{
		}
#endif
	}
}
