using System;
using System.IO;
using System.Xml;
using NUnit.Framework;
#if PCL
using Portable.Xaml.Markup;
using Portable.Xaml;
using Portable.Xaml.Schema;
#else
using System.Windows.Markup;
using System.Xaml;
using System.Xaml.Schema;
#endif

namespace MonoTests.Portable.Xaml
{
	[TestFixture]
	public class PropertyNotFoundTest
	{
		XamlReader GetReader(string filename)
		{
			string xml = File.ReadAllText(Compat.GetTestFile(filename)).UpdateXml();
			return new XamlXmlReader(XmlReader.Create(new StringReader(xml)),new XamlXmlReaderSettings{ProvideLineInfo = true});
		}

		[Test]
		public void DoTest()
		{
			var ex = Assert.Throws<XamlObjectWriterException>(() =>
			                                                  {
				                                                  using (var xr = GetReader("PropertyNotFound.xml"))
				                                                  {
					                                                  var des = (ComplexPositionalParameterWrapper)XamlServices.Load(xr);
					                                                  Assert.Fail("Should not succeed!");
				                                                  }
			                                                  });
			Assert.AreEqual(1, ex.LineNumber);
			Assert.AreEqual(13, ex.LinePosition);
		}
	}
}