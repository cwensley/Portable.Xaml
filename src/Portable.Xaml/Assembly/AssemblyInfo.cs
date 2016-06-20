//
// AssemblyInfo.cs
//
// Author:
//   Andreas Nahr (ClassDevelopment@A-SoftTech.com)
//
// (C) 2003 Ximian, Inc.  http://www.ximian.com
//

//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.Reflection;
using System.Resources;
using System.Security;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#if !PCL
using System.Security.Permissions;
#endif

// General Information about the System.Xaml assembly

[assembly: AssemblyTitle ("Portable.Xaml.dll")]
[assembly: AssemblyDescription ("Portable.Xaml.dll")]
[assembly: AssemblyDefaultAlias ("Portable.Xaml.dll")]

[assembly: NeutralResourcesLanguage ("en-US")]
[assembly: CLSCompliant (true)]
//[assembly: ComVisible (false)]

[assembly: AssemblyVersion ("0.13.*")]
[assembly: AssemblyInformationalVersion ("0.14.0-alpha")]
[assembly: AssemblyCompany("Mono development team")]
[assembly: AssemblyCopyright("(c) Various Mono authors")]



#if !PCL

[assembly: AssemblyCompany (Consts.MonoCompany)]
[assembly: AssemblyProduct (Consts.MonoProduct)]
[assembly: AssemblyCopyright (Consts.MonoCopyright)]
[assembly: AssemblyVersion (Consts.FxVersion)]
[assembly: SatelliteContractVersion (Consts.FxVersion)]
[assembly: AssemblyInformationalVersion (Consts.FxFileVersion)]
[assembly: AssemblyFileVersion (Consts.FxFileVersion)]

[assembly: AssemblyDelaySign (true)]

#if NET_2_1
[assembly: AssemblyKeyFile ("../silverlight.pub")]
#else
[assembly: AssemblyKeyFile ("../ecma.pub")]
[assembly: AllowPartiallyTrustedCallers]
[assembly: ComCompatibleVersion (1, 0, 3300, 0)]
[assembly: SecurityCritical (SecurityCriticalScope.Explicit)]
#endif


#if !MOBILE
[assembly: TypeForwardedTo (typeof (System.Windows.Markup.ValueSerializerAttribute))]
#endif
#endif