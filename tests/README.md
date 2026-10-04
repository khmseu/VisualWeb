# Tests

Use one xUnit v3 test project per implemented source project:
`tests/<source-project>.Tests/<source-project>.Tests.csproj`.
The [cache tests](Tools.SpecCache.Tests/) are the phase-1 example.
The [shared platform suite](Platform.Tests/) tests portable contracts, backend
selection, process services and OS paths without initializing native displays.
It groups both OS backends to avoid duplicating contract/fake tests.
The [core suite](Core.Tests/) groups URL, encoding and MIME utilities with
[pinned official fixtures](Core.Tests/Data/README.md) and offline regression tests.
The [networking suite](Engine.Net.Tests/) uses fake transports and
[pinned Fetch data URL fixtures](Engine.Net.Tests/Data/README.md). Its integration
tests bind only owned, ephemeral loopback ports and never contact remote hosts.
The [DOM suite](Engine.Dom.Tests/) checks tree mutation invariants and the
[HTML suite](Engine.Html.Tests/) runs [pinned html5lib data](Engine.Html.Tests/Data/README.md)
and static parser/limit regressions. No HTML test invokes scripts or fetches resources.

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <IsTestProject>true</IsTestProject>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit.v3" />
    <PackageReference Include="xunit.runner.visualstudio" PrivateAssets="all" />
    <ProjectReference Include="../../src/Area/Project/Project.csproj" />
  </ItemGroup>
</Project>
```

Versions belong in the root central package file. Add each new test project to
`VisualWeb.slnx`. Pass `TestContext.Current.CancellationToken` to asynchronous
operations and inject clocks/HTTP handlers rather than depending on real time
or network services.

```sh
dotnet test tests/Tools.SpecCache.Tests/Tools.SpecCache.Tests.csproj
dotnet test tests/Platform.Tests/Platform.Tests.csproj
dotnet test tests/Core.Tests/Core.Tests.csproj
dotnet test tests/Engine.Net.Tests/Engine.Net.Tests.csproj
dotnet test tests/Engine.Dom.Tests/Engine.Dom.Tests.csproj
dotnet test tests/Engine.Html.Tests/Engine.Html.Tests.csproj
dotnet build VisualWeb.slnx
dotnet test VisualWeb.slnx --no-build
```

Building the solution is the all-project build smoke check, not a recursive
unit test that starts another build. Empty source projects do not have dummy
test suites; add meaningful tests as their implementations are introduced.

Tests do not read/download live standards. Future WPT/html5lib fixtures must
be versioned or otherwise reproducibly provisioned with upstream license and
provenance. Standards documentation has its own [cache workflow](../docs/standards.md).
