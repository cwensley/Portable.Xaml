using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;
using System.IO;
using System.Collections.ObjectModel;
using System.Collections;
using n = NUnit.Framework;
#if PCL
using Portable.Xaml.Markup;
using Portable.Xaml;
using Portable.Xaml.Schema;
using Portable.Xaml.ComponentModel;
#else
using System.Windows.Markup;
using System.Xaml;
using System.Xaml.Schema;
using System.ComponentModel;
#endif

namespace MonoTests.Portable.Xaml
{
    public class CollectionItemConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            return sourceType == typeof(OtherItem) || base.CanConvertFrom(context, sourceType);
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            var otherItem = value as OtherItem;
            if (otherItem != null)
            {
                return otherItem.CollectionItem;
            }
            return base.ConvertFrom(context, culture, value);
        }
    }

    public class OtherItem
    {
        public CollectionItem CollectionItem {  get { return new CollectionItem { Name = "FromOther" }; } }
    }

    [TypeConverter(typeof(CollectionItemConverter))]
    public class CollectionItem
    {
        public string Name { get; set; }
    }

    public class CollectionItemCollectionAddOverride : Collection<CollectionItem>, IList
    {
        int IList.Add(object item)
        {
            var other = item as OtherItem;
            if (other != null)
                Add(other.CollectionItem);
            else
                Add((CollectionItem)item);
            return Count - 1;
        }
    }

    public class CollectionItemCollection : Collection<CollectionItem>
    {
    }

    [ContentProperty("Items")]
    public class CollectionParentCustomAddOverride
    {
        public CollectionItemCollectionAddOverride Items { get; } = new CollectionItemCollectionAddOverride();
    }

    [ContentProperty("Items")]
    public class CollectionParentGenericList
    {
        public List<CollectionItem> Items { get; } = new List<CollectionItem>();
    }

    [ContentProperty("Items")]
    public class CollectionParentCustomNoOverride
    {
        public CollectionItemCollection Items { get; } = new CollectionItemCollection();
    }

    [TestFixture]
    public class XamlObjectWriterCollectionTests
    {
        /// <summary>
        /// Test adding a different type to a custom collection using an explicitly implemented IList.Add(object) method
        /// </summary>
        [Test] // works on both MS.NET and Portable.Xaml, but no way to make use of type converters
        public void TestCustomCollectionAddOverride()
        {
            var xaml = @"<CollectionParentCustomAddOverride xmlns='clr-namespace:MonoTests.Portable.Xaml;assembly=Portable.Xaml_test_net_4_0'><OtherItem/></CollectionParentCustomAddOverride>".UpdateXml();
            var parent = (CollectionParentCustomAddOverride)XamlServices.Load(new StringReader(xaml));

            Assert.IsNotNull(parent, "#1");
            Assert.IsInstanceOf<CollectionParentCustomAddOverride>(parent, "#2");
            Assert.AreEqual(1, parent.Items.Count, "#3");
            var item = parent.Items.FirstOrDefault();
            Assert.IsNotNull(item, "#4");
            Assert.AreEqual("FromOther", item.Name, "#5");
        }

        /// <summary>
        /// Test adding a different type to a generic collection using the typeconverter on the list item type.
        /// </summary>
        [Test, n.Category(Categories.NotOnSystemXaml)] // doesn't work in MS.NET, but theoretically should
        public void TestListCollectionItemConverter()
        {
            var xaml = @"<CollectionParentGenericList xmlns='clr-namespace:MonoTests.Portable.Xaml;assembly=Portable.Xaml_test_net_4_0'><OtherItem/></CollectionParentGenericList>".UpdateXml();
            var parent = (CollectionParentGenericList)XamlServices.Load(new StringReader(xaml));

            Assert.IsNotNull(parent, "#1");
            Assert.IsInstanceOf<CollectionParentGenericList>(parent, "#2");
            Assert.AreEqual(1, parent.Items.Count, "#3");
            var item = parent.Items.FirstOrDefault();
            Assert.IsNotNull(item, "#4");
            Assert.AreEqual("FromOther", item.Name, "#5");
        }

        /// <summary>
        /// Test adding a different type to a custom collection that implements a generic collection using the typeconverter of the list item type.
        /// </summary>
        [Test, n.Category(Categories.NotOnSystemXaml)] // New in Portable.Xaml, doesn't work in MS.NET, but should
        public void TestCustomCollectionItemConverter()
        {
            var xaml = @"<CollectionParentCustomNoOverride xmlns='clr-namespace:MonoTests.Portable.Xaml;assembly=Portable.Xaml_test_net_4_0'><OtherItem/></CollectionParentCustomNoOverride>".UpdateXml();
            var parent = (CollectionParentCustomNoOverride)XamlServices.Load(new StringReader(xaml));

            Assert.IsNotNull(parent, "#1");
            Assert.IsInstanceOf<CollectionParentCustomNoOverride>(parent, "#2");
            Assert.AreEqual(1, parent.Items.Count, "#3");
            var item = parent.Items.FirstOrDefault();
            Assert.IsNotNull(item, "#4");
            Assert.AreEqual("FromOther", item.Name, "#5");
        }
    }
}
