using System;
using Portable.Xaml;
using System.IO;
using System.Reflection;
using Portable.Xaml.Markup;

[assembly: Portable.Xaml.Markup.XmlnsDefinition("test.example.com", "TestRoslyn")]

namespace TestRoslyn
{
	[ContentProperty("Child")]
	public class ParentObject
	{
		public string StringValue { get; set; }

		public double DoubleValue { get; set; }

		public ChildObject Child { get; set; }
	}

	[RuntimeNameProperty("ID")]
	public class ChildObject
	{
		public string ID { get; set; }
		public string SomeProperty { get; set; }
	}

	class MainClass
	{
		public static void Main(string[] args)
		{
			var xaml = 
				@"<ParentObject xmlns='test.example.com' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
						x:Class='MyClass'
						StringValue='Woo'
						DoubleValue='123.456'>
					<ChildObject x:Name='myChild' SomeProperty='Hello There' />
				</ParentObject>";

			try
			{
				var sc = new XamlSchemaContext(new[] { Assembly.GetExecutingAssembly() });

				var reader = new XamlXmlReader(new StringReader(xaml));
				var rw = new Portable.Xaml.Roslyn.RoslynWriter(sc);
				XamlServices.Transform(reader, rw);
				Console.WriteLine(rw.ToCode());
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex);
			}

		}
	}
}
