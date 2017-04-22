#!/bin/bash

DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
msbuild /t:Package /v:Minimal /p:BuildVersion=$1 $DIR/build/Build.proj
