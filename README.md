Portable.Xaml is a fork of [mono's System.Xaml](https://github.com/mono/mono/tree/master/mcs/class/System.Xaml) converted to PCL 259.

This is intended to be used to read Xaml on desktop, mobile, and CoreCLR platforms.

The main difference between this and System.Xaml is that it comes with its own (minimal) `TypeConverter` implementation as PCL profile 259 does not have access to this. To hook into the existing `System.ComponentModel.TypeConverter` on platforms that support it, a shim can be added [to be documented].

### Why Profile 259? 

Profile 259 has the broadest reach for portable projects, including CoreCLR.  Additional profiles may make sense and can be contributed if desired.  Additionally, dotnet/corefx does come with its own TypeConverter, so eventually that will be the preferred way to go.

### Goals

The goal of this library is not necessarily to replicate the functionality of System.Xaml going forward, so breaking changes may occur, but only when necessary to keep migrating to Portable.Xaml straightforward.

Project Status
--------------

While this library should be usable to read xaml files including markup extensions, it may have either PCL conversion or other bugs.

Contributors are more than welcome! Ideally this library can become well supported for xaml support in more applications.


License
-------

Portable.Xaml is licensed under MIT.

