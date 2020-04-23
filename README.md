Portable.Xaml is a fork of [mono's System.Xaml](https://github.com/mono/mono/tree/master/mcs/class/System.Xaml) converted to .NET Standard.

This is intended to be used to read and write XAML on desktop, mobile, and .NET Core platforms.

[![Join the chat at https://gitter.im/cwensley/Portable.Xaml](https://badges.gitter.im/Join%20Chat.svg)](https://gitter.im/cwensley/Portable.Xaml?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)
[![Build status](https://ci.appveyor.com/api/projects/status/tsbibgrcmd73a7tl/branch/master?svg=true)](https://ci.appveyor.com/project/cwensley/portable-xaml/branch/master)

## Supported Profiles

Portable.Xaml currently supports the following profiles:

- .NET Standard 1.0 - For .NET 4.5
- .NET Standard 1.3 - For .NET 4.6
- .NET Standard 2.0 - For mono, .NET Core, UWP, Xamarin, etc.

Other profiles can be contributed if desired, but these should support the widest range of platforms.

## Downloads

The primary way to use Portable.Xaml is by adding the [nuget package](https://www.nuget.org/packages/Portable.Xaml/) to your project.

To get daily builds, add the [appveyor feed](https://ci.appveyor.com/nuget/portable-xaml) to your nuget package sources.  Make sure you check 'include prerelease' to show the prerelease builds.

## Goals

The goal of this library is not necessarily to replicate the functionality of System.Xaml going forward, so breaking changes may occur, but only when necessary to keep migrating to Portable.Xaml straightforward.

Some of the enhanced functionality of Portable.Xaml include (but not limited to):

- Supports `System.Collections.Immutable` collections and dictionaries.
- Automatic support for immutable types (with no default constructor) without having to use `x:Arguments` by using properties flagged with `[ConstructorArgumentAttribute]` automatically.
- Easier to override `XamlMember` creation using `ParameterInfo/PropertyInfo/MethodInfo/etc` from `XamlSchemaContext`
- `TypeConverter` is used for items when adding to any `ICollection<T>` inherited list, if the type does not match.


## Project Status

While this library should be usable to read & write XAML files including markup extensions, it may have some bugs.

Contributors are more than welcome! Ideally this library can become well supported to bring XAML to more applications.

## Performance

What about performance you ask? Portable.Xaml's performance has been drastically improved over mono's initial implementation, and is actually much faster than .NET's System.Xaml in most cases.

Here's some results using [BenchmarkDotNet](http://benchmarkdotnet.org):

### Load
|              Method |       Runtime |       Mean |       Error |   StdDev |    Op/s | Ratio | RatioSD | Rank |   Gen 0 |   Gen 1 |  Gen 2 | Allocated |
|-------------------- |-------------- |-----------:|------------:|---------:|--------:|------:|--------:|-----:|--------:|--------:|-------:|----------:|
|        PortableXaml |      .NET 4.8 |   614.6 us |   323.19 us | 17.72 us | 1,627.1 |  1.00 |    0.00 |    1 | 17.5781 |  0.9766 |      - |  113271 B |
| PortableXamlNoCache |      .NET 4.8 | 1,694.8 us | 1,785.79 us | 97.89 us |   590.0 |  2.76 |    0.23 |    4 |       - |       - |      - |  254008 B |
|          SystemXaml |      .NET 4.8 |   785.1 us |    99.78 us |  5.47 us | 1,273.7 |  1.28 |    0.04 |    2 | 40.0391 |  2.9297 |      - |  257822 B |
|   SystemXamlNoCache |      .NET 4.8 | 1,127.7 us |   187.94 us | 10.30 us |   886.7 |  1.84 |    0.05 |    3 | 50.7813 | 25.3906 | 1.9531 |  321342 B |
|                     |               |            |             |          |         |       |         |      |         |         |        |           |
|        PortableXaml | .NET Core 3.1 |   471.7 us |    17.33 us |  0.95 us | 2,120.0 |  1.00 |    0.00 |    1 | 12.6953 |  0.9766 |      - |  106952 B |
| PortableXamlNoCache | .NET Core 3.1 | 1,011.9 us |   157.52 us |  8.63 us |   988.2 |  2.15 |    0.02 |    2 | 23.4375 | 11.7188 |      - |  201949 B |


### Save
|              Method |       Runtime |     Mean |     Error |   StdDev |    Op/s | Ratio | RatioSD | Rank |   Gen 0 |   Gen 1 |  Gen 2 | Allocated |
|-------------------- |-------------- |---------:|----------:|---------:|--------:|------:|--------:|-----:|--------:|--------:|-------:|----------:|
|        PortableXaml |      .NET 4.8 | 380.5 us |  37.76 us |  2.07 us | 2,627.9 |  1.00 |    0.00 |    1 | 30.2734 |  2.4414 |      - |  192242 B |
| PortableXamlNoCache |      .NET 4.8 | 727.0 us | 228.94 us | 12.55 us | 1,375.5 |  1.91 |    0.04 |    4 | 36.1328 |  0.9766 |      - |  234264 B |
|          SystemXaml |      .NET 4.8 | 506.5 us |  62.80 us |  3.44 us | 1,974.1 |  1.33 |    0.02 |    2 | 33.2031 |  4.8828 |      - |  214071 B |
|   SystemXamlNoCache |      .NET 4.8 | 697.6 us | 378.09 us | 20.72 us | 1,433.6 |  1.83 |    0.04 |    3 | 39.0625 | 19.5313 | 1.9531 |  251932 B |
|                     |               |          |           |          |         |       |         |      |         |         |        |           |
|        PortableXaml | .NET Core 3.1 | 329.8 us |  23.11 us |  1.27 us | 3,032.0 |  1.00 |    0.00 |    1 | 22.4609 |  1.9531 |      - |  191088 B |
| PortableXamlNoCache | .NET Core 3.1 | 576.9 us |  46.65 us |  2.56 us | 1,733.5 |  1.75 |    0.01 |    2 | 26.3672 | 12.6953 |      - |  227713 B |

## License

Portable.Xaml is licensed under MIT.

