# 05-tier4-main-app: Upgrade Tier 4 — main VisualHFT application

Upgrade the root application project and the final testing framework that wraps it.

Projects in scope:
- `VisualHFT.csproj` — net8.0-windows10.0.22621.0 → net10.0-windows10.0.22621.0. 2,035 mandatory issues — the highest count in the solution, dominated by WPF/XAML behavioral changes, binary-incompatible APIs (Api.0001), source-incompatible APIs (Api.0002), and one incompatible NuGet package (NuGet.0001). The NuGet.0001 package must be resolved inline — identify the package, find a net10.0-compatible version or remove it. Fix compilation failures inline; behavioral-only issues that don't block compilation are acceptable per the minimal migration approach.
- `VisualHFT.TriggerService.TestingFramework\VisualHFT.TriggerService.TestingFramework.csproj` — net8.0-windows10.0.22621.0 → net10.0-windows10.0.22621.0. 1 mandatory issue.

This is the highest-risk task due to the large issue count in the main app. Take a methodical approach: update TFM and packages first, then iteratively fix compilation errors before addressing warnings.

**Done when**: Full solution builds without errors on net10.0. All tests pass. No incompatible NuGet packages remain. Solution is ready for user review and manual commit.
