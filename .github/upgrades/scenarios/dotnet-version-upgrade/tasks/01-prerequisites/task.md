# 01-prerequisites: Verify SDK and toolchain

Verify that .NET 10 SDK is installed on the machine and that any `global.json` files in both repos are compatible with net10.0. Both the VisualHFT repo and the OxyPlot repo should be checked. This is a zero-code-change task — only toolchain validation.

**Done when**: `dotnet --list-sdks` shows a .NET 10 SDK installed; no `global.json` file pins to a version incompatible with net10.0; both repos confirm ready to build with net10.0 toolchain.
