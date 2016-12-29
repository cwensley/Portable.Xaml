Portable.Xaml is a fork of [mono's System.Xaml](https://github.com/mono/mono/tree/master/mcs/class/System.Xaml) converted to PCL.

This is intended to be used to read Xaml on desktop, mobile, and CoreCLR platforms.

The main difference between this and System.Xaml is that it comes with its own (minimal) `TypeConverter` implementation as the PCL profiles do not support them. To hook into the existing `System.ComponentModel.TypeConverter` on platforms that support it, a shim can be added [to be documented].

[![Join the chat at https://gitter.im/cwensley/Portable.Xaml](https://badges.gitter.im/Join%20Chat.svg)](https://gitter.im/cwensley/Portable.Xaml?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)
[![Build status](https://ci.appveyor.com/api/projects/status/tsbibgrcmd73a7tl/branch/master?svg=true)](https://ci.appveyor.com/project/cwensley/portable-xaml/branch/master)

## Supported Profiles

Portable.Xaml currently supports the following profiles:

- Profile 259 - For .NET 4.5, CoreCLR, WinRT, Xamarin, etc.
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

What about performance you ask? Portable.Xaml can actually be much faster than .NET's System.Xaml in some cases, especially when loading xaml.

Portable.Xaml's performance has also been drastically improved over mono's initial implementation, which used to be many times slower than .NET.

### Load
Method |          Mean |     StdDev | Scaled | Scaled-StdDev |   Gen 0 | Allocated |
------------- |-------------- |----------- |------- |-------------- |-------- |---------- |
 PortableXaml |   897.6095 us | 24.2359 us |   1.00 |          0.00 | 19.7368 | 200.19 kB |
   SystemXaml | 1,427.0262 us | 52.7652 us |   1.59 |          0.07 |  3.0382 | 155.19 kB |

### Save
Method |        Mean |     StdDev | Scaled | Scaled-StdDev |   Gen 0 | Allocated |
------------- |------------ |----------- |------- |-------------- |-------- |---------- |
 PortableXaml | 990.8799 us | 18.2260 us |   1.00 |          0.00 | 25.0000 |    408 kB |
   SystemXaml | 923.7489 us | 21.5989 us |   0.93 |          0.03 |       - | 120.03 kB |

## License

Portable.Xaml is licensed under MIT.

