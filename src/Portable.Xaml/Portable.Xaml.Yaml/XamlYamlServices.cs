using System;
using System.IO;

namespace Portable.Xaml.Yaml
{
	[EnhancedXaml]
	public static class XamlYamlServices
	{
		public static Object Load(string fileName)
		{
			using (var stream = File.OpenRead(fileName))
				return Load(stream);
		}

		public static Object Load(Stream stream) => XamlServices.Load(new XamlYamlReader(stream));

		public static Object Load(TextReader textReader) => XamlServices.Load(new XamlYamlReader(textReader));

		public static object Parse(string yaml) => Load(new StringReader(yaml));

		public static string Save(object instance)
		{
			using (var sw = new StringWriter())
			{
				Save(sw, instance);
				return sw.ToString();
			}
		}

		public static void Save(string fileName, object instance)
		{
			using (var stream = File.Create(fileName))
				Save(stream, instance);
		}

		public static void Save(Stream stream, object instance) => Save(new StreamWriter(stream), instance);

		public static void Save(TextWriter textWriter, object instance)
		{
			XamlServices.Save(new XamlYamlWriter(textWriter, new XamlSchemaContext()), instance);
		}
	}
}
