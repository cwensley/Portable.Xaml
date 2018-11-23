Portable.Xaml is a fork of [mono's System.Xaml](https://github.com/mono/mono/tree/master/mcs/class/System.Xaml) converted to .NET Standard.

This is intended to be used to read and write XAML on desktop, mobile, and .NET Core platforms.

[![Join the chat at https://gitter.im/cwensley/Portable.Xaml](https://badges.gitter.im/Join%20Chat.svg)](https://gitter.im/cwensley/Portable.Xaml?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)
[![Build status](https://ci.appveyor.com/api/projects/status/tsbibgrcmd73a7tl/branch/master?svg=true)](https://ci.appveyor.com/project/cwensley/portable-xaml/branch/master)

## Supported Profiles

Portable.Xaml currently supports the following profiles:

- .NET Standard 1.0 - For .NET 4.5
- .NET Standard 1.3 - For .NET 4.6, .NET Core, UWP, Xamarin, etc.

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
|              Method |       Mean |     StdDev |    Op/s | Scaled |    Gen 0 |  Allocated |
|-------------------- |-----------:|-----------:|--------:|-------:|---------:|-----------:|
|        PortableXaml |   569.8 us |  16.294 us | 1,754.9 |   1.00 |  11.7188 |   50.56 KB |
|          SystemXaml | 1,325.2 us |  11.485 us |   754.6 |   2.33 |  35.1563 |  151.53 KB |
| PortableXamlNoCache | 1,409.3 us |   7.649 us |   709.6 |   2.48 |  25.3906 |  106.38 KB |
|   SystemXamlNoCache | 1,892.2 us |  28.304 us |   528.5 |   3.32 |  44.9219 |  184.67 KB |
|   OmniXamlBenchmark | 8,095.5 us | 310.062 us |   123.5 |  14.22 | 406.2500 | 1689.29 KB |
      
### Save
|              Method |       Mean |    StdDev |    Op/s | Scaled |   Gen 0 | Allocated |
|-------------------- |-----------:|----------:|--------:|-------:|--------:|----------:|
|        PortableXaml |   481.8 us |  2.805 us | 2,075.4 |   1.00 | 26.8555 |  110.4 KB |
|          SystemXaml |   809.2 us | 12.997 us | 1,235.8 |   1.68 | 28.3203 |  117.2 KB |
| PortableXamlNoCache | 1,060.2 us | 34.809 us |   943.3 |   2.20 | 33.2031 | 138.91 KB |
|   SystemXamlNoCache | 1,151.5 us | 18.052 us |   868.5 |   2.39 | 33.2031 | 139.17 KB |

## License

Portable.Xaml is licensed under MIT.

