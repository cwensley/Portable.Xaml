using System;
using System.IO;

namespace Portable.Xaml.Json
{
	[EnhancedXaml]
	public static class XamlJsonServices
	{
		public static Object Load(Stream stream) => XamlServices.Load(new XamlJsonReader(stream));

		public static Object Load(TextReader textReader) => XamlServices.Load(new XamlJsonReader(textReader));

		public static object Parse(string xaml) => Load(new StringReader(xaml));

		public static string Save(object instance)
		{
			using (var sw = new StringWriter())
			{
				Save(sw, instance);
				return sw.ToString();
			}
		}

		public static void Save(Stream stream, object instance) => Save(new StreamWriter(stream), instance);

		public static void Save(TextWriter textWriter, object instance)
		{
			XamlServices.Save(new XamlJsonWriter(textWriter, new XamlSchemaContext(), new XamlJsonWriterSettings { UseNamespaces = true }), instance);
		}
	}
}
