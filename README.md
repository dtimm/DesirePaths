# Desire Paths

A desire path is a route that is created by natural foot traffic.
https://en.wikipedia.org/wiki/Desire_path

This mod causes pawns in RimWorld to trample snow (and optionally vegetation)
along common paths.

## Development

This project was built with Visual Studio 2019. To set up the build
environment, copy `UnityEngine.dll` and `Assembly-CSharp.dll` from the
`RimWorld/RimWorld*_Data/Managed/` directory to `./DesirePaths/References/`.
You will also need to copy
[HugsLib>7.1.0](https://github.com/UnlimitedHugs/RimworldHugsLib) to the
`./DesirePaths/References/` directory.

The project can also be build with `xbuild` on Linux.
```
apt install mono-xbuild mono-devel
xbuild DesirePaths.sln
```
