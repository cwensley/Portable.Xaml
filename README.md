Portable.Xaml is a fork of [mono's System.Xaml](https://github.com/mono/mono/tree/master/mcs/class/System.Xaml) converted to PCL.

This is intended to be used to read Xaml on desktop, mobile, and CoreCLR platforms.

The main difference between this and System.Xaml is that it comes with its own (minimal) `TypeConverter` implementation as the PCL profiles do not support them. To hook into the existing `System.ComponentModel.TypeConverter` on platforms that support it, a shim can be added [to be documented].

[![Join the chat at https://gitter.im/cwensley/Portable.Xaml](https://badges.gitter.im/Join%20Chat.svg)](https://gitter.im/cwensley/Portable.Xaml?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)
[![Build status](https://ci.appveyor.com/api/projects/status/tsbibgrcmd73a7tl/branch/master?svg=true)](https://ci.appveyor.com/project/cwensley/portable-xaml/branch/master)

## Supported Profiles

Portable.Xaml currently supports the following profiles:

- Profile 259 - For .NET 4.5, .NET Core, WinRT, Xamarin, etc.
- Profile 136 - For .NET 4.0 support

Other profiles can be contributed if desired, but these should support the widest range of platforms.

## Downloads

The primary way to use Portable.Xaml is by adding the [nuget package](https://www.nuget.org/packages/Portable.Xaml/) to your project.

To get daily builds, add the [appveyor feed](https://ci.appveyor.com/nuget/portable-xaml) to your nuget package sources.  Make sure you check 'include prerelease' to show the prerelease builds.

## Goals

The goal of this library is not necessarily to replicate the functionality of System.Xaml going forward, so breaking changes may occur, but only when necessary to keep migrating to Portable.Xaml straightforward.

## Project Status

While this library should be usable to read & write xaml files including markup extensions, it may have some bugs.

Contributors are more than welcome! Ideally this library can become well supported to bring xaml to more applications.

## Performance

What about performance you ask? Portable.Xaml can actually be faster than .NET's System.Xaml in most cases, especially when loading xaml.

Portable.Xaml's performance has also been drastically improved over mono's initial implementation by 11x loading, and 28x saving.

Here's some results using [BenchmarkDotNet](http://benchmarkdotnet.org):

### Load
Method |          Mean |     StdDev | Scaled | Scaled-StdDev | Allocated |-------------------- |-------------- |----------- |------- |-------------- |---------- |        PortableXaml |   726.6193 us | 28.2493 us |   1.00 |          0.00 |  72.23 kB |          SystemXaml | 1,330.3755 us | 31.5314 us |   1.83 |          0.08 | 155.19 kB | PortableXamlNoCache | 1,598.9083 us | 22.4152 us |   2.20 |          0.09 | 129.89 kB |   SystemXamlNoCache | 1,899.0161 us | 90.5633 us |   2.62 |          0.16 | 187.14 kB |

### Save
Method |          Mean |     StdDev | Scaled | Scaled-StdDev |   Gen 0 | Allocated |-------------------- |-------------- |----------- |------- |-------------- |-------- |---------- |        PortableXaml |   813.9052 us | 11.2787 us |   1.00 |          0.00 |       - | 146.18 kB |          SystemXaml |   874.2129 us | 15.8162 us |   1.07 |          0.02 |       - | 120.03 kB | PortableXamlNoCache | 1,588.3347 us |  9.7873 us |   1.95 |          0.03 | 15.6250 | 197.46 kB |   SystemXamlNoCache | 1,187.0039 us | 13.3269 us |   1.46 |          0.03 |       - | 142.01 kB |

## License

Portable.Xaml is licensed under MIT.

