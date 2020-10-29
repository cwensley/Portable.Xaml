using System;
using BenchmarkDotNet.Attributes;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Benchmark.Portable.Xaml.Json
{
	public class JsonSaveComplexBenchmark : SaveBenchmark
	{
		public override object Instance => new TestObject
		{
			StringProperty = "Hello",
			Children = {
				new ChildObject { StringProperty = "There", BoolProperty = true, DoubleProperty = 123.456, IntProperty = 123 },
				new ChildObject { StringProperty = "There", BoolProperty = true, DoubleProperty = 123.456, IntProperty = 123 },
				new ChildObject { StringProperty = "There", BoolProperty = true, DoubleProperty = 123.456, IntProperty = 123 },
				new ChildObject { StringProperty = "There", BoolProperty = true, DoubleProperty = 123.456, IntProperty = 123 },
				new ChildObject { StringProperty = "There", BoolProperty = true, DoubleProperty = 123.456, IntProperty = 123 },
				new ChildObject { StringProperty = "There", BoolProperty = true, DoubleProperty = 123.456, IntProperty = 123 },
				new ChildObject { StringProperty = "There", BoolProperty = true, DoubleProperty = 123.456, IntProperty = 123 },
				new ChildObject { StringProperty = "There", BoolProperty = true, DoubleProperty = 123.456, IntProperty = 123 },
				new ChildObject { StringProperty = "There", BoolProperty = true, DoubleProperty = 123.456, IntProperty = 123 },
				new ChildObject { StringProperty = "There", BoolProperty = true, DoubleProperty = 123.456, IntProperty = 123 },
				new ChildObject { StringProperty = "There", BoolProperty = true, DoubleProperty = 123.456, IntProperty = 123 },
				new ChildObject { StringProperty = "There", BoolProperty = true, DoubleProperty = 123.456, IntProperty = 123 },
				new ChildObject { StringProperty = "There", BoolProperty = true, DoubleProperty = 123.456, IntProperty = 123 },
				new ChildObject { StringProperty = "There", BoolProperty = true, DoubleProperty = 123.456, IntProperty = 123 },
				new ChildObject { StringProperty = "There", BoolProperty = true, DoubleProperty = 123.456, IntProperty = 123 },
				new ChildObject { StringProperty = "There", BoolProperty = true, DoubleProperty = 123.456, IntProperty = 123 },
				new ChildObject { StringProperty = "There", BoolProperty = true, DoubleProperty = 123.456, IntProperty = 123 },
				new ChildObject { StringProperty = "There", BoolProperty = true, DoubleProperty = 123.456, IntProperty = 123 },
				new ChildObject { StringProperty = "There", BoolProperty = true, DoubleProperty = 123.456, IntProperty = 123 },
				new ChildObject { StringProperty = "There", BoolProperty = true, DoubleProperty = 123.456, IntProperty = 123 },
				new ChildObject { StringProperty = "There", BoolProperty = true, DoubleProperty = 123.456, IntProperty = 123 },
				new ChildObject { StringProperty = "There", BoolProperty = true, DoubleProperty = 123.456, IntProperty = 123 },
				new ChildObject { StringProperty = "There", BoolProperty = true, DoubleProperty = 123.456, IntProperty = 123 },
				new ChildObject { StringProperty = "There", BoolProperty = true, DoubleProperty = 123.456, IntProperty = 123 },
				new ChildObject { StringProperty = "There", BoolProperty = true, DoubleProperty = 123.456, IntProperty = 123 },
				new ChildObject { StringProperty = "There", BoolProperty = true, DoubleProperty = 123.456, IntProperty = 123 },
				new ChildObject { StringProperty = "There", BoolProperty = true, DoubleProperty = 123.456, IntProperty = 123 },
				new ChildObject { StringProperty = "There", BoolProperty = true, DoubleProperty = 123.456, IntProperty = 123 },
				new ChildObject { StringProperty = "There", BoolProperty = true, DoubleProperty = 123.456, IntProperty = 123 },
				new ChildObject { StringProperty = "There", BoolProperty = true, DoubleProperty = 123.456, IntProperty = 123 },
				new ChildObject { StringProperty = "There", BoolProperty = true, DoubleProperty = 123.456, IntProperty = 123 },
				new ChildObject { StringProperty = "There", BoolProperty = true, DoubleProperty = 123.456, IntProperty = 123 },
				new ChildObject { StringProperty = "There", BoolProperty = true, DoubleProperty = 123.456, IntProperty = 123 },
				new ChildObject { StringProperty = "There", BoolProperty = true, DoubleProperty = 123.456, IntProperty = 123 },
				new ChildObject { StringProperty = "There", BoolProperty = true, DoubleProperty = 123.456, IntProperty = 123 },
				new ChildObject { StringProperty = "There", BoolProperty = true, DoubleProperty = 123.456, IntProperty = 123 },
				new ChildObject { StringProperty = "There", BoolProperty = true, DoubleProperty = 123.456, IntProperty = 123 },
				new ChildObject { StringProperty = "There", BoolProperty = true, DoubleProperty = 123.456, IntProperty = 123 },
				new ChildObject { StringProperty = "There", BoolProperty = true, DoubleProperty = 123.456, IntProperty = 123 },
				new ChildObject { StringProperty = "There", BoolProperty = true, DoubleProperty = 123.456, IntProperty = 123 },
				new ChildObject { StringProperty = "There", BoolProperty = true, DoubleProperty = 123.456, IntProperty = 123 },
				new ChildObject { StringProperty = "There", BoolProperty = true, DoubleProperty = 123.456, IntProperty = 123 },
				new ChildObject { StringProperty = "There", BoolProperty = true, DoubleProperty = 123.456, IntProperty = 123 },
				new ChildObject { StringProperty = "There", BoolProperty = true, DoubleProperty = 123.456, IntProperty = 123 },
				new ChildObject { StringProperty = "There", BoolProperty = true, DoubleProperty = 123.456, IntProperty = 123 },
				new ChildObject { StringProperty = "There", BoolProperty = true, DoubleProperty = 123.456, IntProperty = 123 },
				new ChildObject { StringProperty = "There", BoolProperty = true, DoubleProperty = 123.456, IntProperty = 123 },
				new ChildObject { StringProperty = "There", BoolProperty = true, DoubleProperty = 123.456, IntProperty = 123 },
				new ChildObject { StringProperty = "There", BoolProperty = true, DoubleProperty = 123.456, IntProperty = 123 },
				new ChildObject { StringProperty = "There", BoolProperty = true, DoubleProperty = 123.456, IntProperty = 123 }
			}
		};
	}
}
