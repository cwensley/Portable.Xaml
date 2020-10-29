#!/bin/bash

DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
dotnet build /v:Minimal /p:BuildVersion=$1 $DIR/Portable.Xaml.sln

dotnet pack /v:Minimal /p:BuildVersion=$1 $DIR/Portable.Xaml.sln
