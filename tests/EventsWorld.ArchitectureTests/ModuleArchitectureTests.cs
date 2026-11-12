using System.Reflection;
using System.Xml.Linq;
using NetArchTest.Rules;

namespace EventsWorld.ArchitectureTests;

public sealed class ModuleArchitectureTests
{
    private static readonly string[] Modules =
    [
        "Accounts",
        "Events",
        "Registrations",
        "Payments",
        "Files",
        "Notifications",
        "Audit",
        "Legal"
    ];

    private static readonly string[] ForbiddenDomainDependencies =
    [
        "Microsoft.EntityFrameworkCore",
        "Microsoft.AspNetCore"
    ];

    [Fact]
    public void Modules_ShouldNot_ReferenceOtherModuleImplementationProjects()
    {
        var solutionRoot = FindSolutionRoot();
        var violations = new List<string>();

        foreach (var module in Modules)
        {
            var moduleCsproj = Path.Combine(
                solutionRoot,
                "src",
                "Modules",
                module,
                $"EventsWorld.Modules.{module}",
                $"EventsWorld.Modules.{module}.csproj");

            var contractsCsproj = Path.Combine(
                solutionRoot,
                "src",
                "Modules",
                module,
                $"EventsWorld.Modules.{module}.Contracts",
                $"EventsWorld.Modules.{module}.Contracts.csproj");

            ValidateProjectReferences(module, moduleCsproj, isContractsProject: false, violations);
            ValidateProjectReferences(module, contractsCsproj, isContractsProject: true, violations);

            var moduleAssembly = Assembly.Load($"EventsWorld.Modules.{module}");
            var forbiddenOtherModuleNamespaces = Modules
                .Where(other => !string.Equals(other, module, StringComparison.Ordinal))
                .SelectMany(other => new[]
                {
                    $"EventsWorld.Modules.{other}.Domain",
                    $"EventsWorld.Modules.{other}.Application",
                    $"EventsWorld.Modules.{other}.Infrastructure",
                    $"EventsWorld.Modules.{other}.Api"
                })
                .ToArray();

            var result = Types.InAssembly(moduleAssembly)
                .ShouldNot()
                .HaveDependencyOnAny(forbiddenOtherModuleNamespaces)
                .GetResult();

            if (!result.IsSuccessful)
            {
                var failing = string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>());
                violations.Add($"Module '{module}' depends on another module implementation: {failing}");
            }
        }

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void Domain_ShouldNot_ReferenceEfCoreAspNetCoreOrOtherModules()
    {
        var violations = new List<string>();

        foreach (var module in Modules)
        {
            var moduleAssembly = Assembly.Load($"EventsWorld.Modules.{module}");
            var otherModules = Modules
                .Where(other => !string.Equals(other, module, StringComparison.Ordinal))
                .Select(other => $"EventsWorld.Modules.{other}");

            var forbiddenNamespaces = ForbiddenDomainDependencies
                .Concat(otherModules)
                .ToArray();

            var result = Types.InAssembly(moduleAssembly)
                .That()
                .ResideInNamespace($"EventsWorld.Modules.{module}.Domain")
                .ShouldNot()
                .HaveDependencyOnAny(forbiddenNamespaces)
                .GetResult();

            if (!result.IsSuccessful)
            {
                var failing = string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>());
                violations.Add($"Domain of '{module}' has forbidden dependencies: {failing}");
            }
        }

        // El Shared Kernel tampoco debe depender de EF Core, ASP.NET Core, Shared.Infrastructure ni módulos
        var kernelAssembly = Assembly.Load("EventsWorld.Shared.Kernel");
        var kernelResult = Types.InAssembly(kernelAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore",
                "EventsWorld.Shared.Infrastructure",
                "EventsWorld.Modules")
            .GetResult();

        if (!kernelResult.IsSuccessful)
        {
            var failing = string.Join(", ", kernelResult.FailingTypeNames ?? Array.Empty<string>());
            violations.Add($"Shared.Kernel has forbidden dependencies: {failing}");
        }

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    /// <summary>Api → Application → Domain ← Infrastructure (backend.md §1, ADR-0003 §7).</summary>
    [Fact]
    public void Layers_Should_FollowTheDependencyRule()
    {
        var violations = new List<string>();

        foreach (var module in Modules)
        {
            var moduleAssembly = Assembly.Load($"EventsWorld.Modules.{module}");
            var root = $"EventsWorld.Modules.{module}";

            CheckLayer(
                moduleAssembly,
                $"{root}.Domain",
                [$"{root}.Application", $"{root}.Infrastructure", $"{root}.Api", "EventsWorld.Shared.Infrastructure"],
                violations);

            CheckLayer(
                moduleAssembly,
                $"{root}.Application",
                [$"{root}.Infrastructure", $"{root}.Api"],
                violations);
        }

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void ModuleTypes_Should_BeInternal_UnlessInApiOrContracts()
    {
        var violations = new List<string>();

        foreach (var module in Modules)
        {
            var moduleAssembly = Assembly.Load($"EventsWorld.Modules.{module}");

            var result = Types.InAssembly(moduleAssembly)
                .That()
                .DoNotResideInNamespace($"EventsWorld.Modules.{module}.Api")
                .And()
                .DoNotResideInNamespace($"EventsWorld.Modules.{module}.Contracts")
                .And()
                .DoNotHaveName($"{module}Module")
                .Should()
                .NotBePublic()
                .GetResult();

            if (!result.IsSuccessful)
            {
                var failing = string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>());
                violations.Add($"Module '{module}' exposes public types outside Api/Contracts: {failing}");
            }
        }

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void EveryModule_Should_ExposeAddModuleExtension_AndBeRegisteredInBootstrapper()
    {
        var solutionRoot = FindSolutionRoot();
        var programPath = Path.Combine(solutionRoot, "src", "Bootstrapper", "EventsWorld.Api", "Program.cs");
        var programContent = File.ReadAllText(programPath);

        var bootstrapperAssembly = typeof(Program).Assembly;
        var referencedAssemblies = bootstrapperAssembly
            .GetReferencedAssemblies()
            .Select(a => a.Name)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var module in Modules)
        {
            var moduleAssemblyName = $"EventsWorld.Modules.{module}";
            Assert.Contains(moduleAssemblyName, referencedAssemblies);

            var moduleAssembly = Assembly.Load(moduleAssemblyName);
            // The module's composition root lives at the project root (backend.md §1).
            var moduleType = moduleAssembly.GetType($"EventsWorld.Modules.{module}.{module}Module");

            Assert.NotNull(moduleType);
            Assert.True(moduleType.IsPublic && moduleType.IsAbstract && moduleType.IsSealed, $"{moduleType.FullName} must be public static.");

            var addMethod = moduleType.GetMethod($"Add{module}Module", BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(addMethod);

            var parameters = addMethod.GetParameters();
            Assert.Equal(2, parameters.Length);
            Assert.Equal("Microsoft.Extensions.DependencyInjection.IServiceCollection", parameters[0].ParameterType.FullName);
            Assert.Equal("Microsoft.Extensions.Configuration.IConfiguration", parameters[1].ParameterType.FullName);

            Assert.Contains($".Add{module}Module(", programContent, StringComparison.Ordinal);
        }
    }

    private static void CheckLayer(
        Assembly moduleAssembly,
        string layerNamespace,
        string[] forbiddenNamespaces,
        List<string> violations)
    {
        var result = Types.InAssembly(moduleAssembly)
            .That()
            .ResideInNamespace(layerNamespace)
            .ShouldNot()
            .HaveDependencyOnAny(forbiddenNamespaces)
            .GetResult();

        if (!result.IsSuccessful)
        {
            var failing = string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>());
            violations.Add($"'{layerNamespace}' must not depend on {string.Join(", ", forbiddenNamespaces)}: {failing}");
        }
    }

    private static void ValidateProjectReferences(
        string currentModule,
        string csprojPath,
        bool isContractsProject,
        List<string> violations)
    {
        var document = XDocument.Load(csprojPath);
        var projectRefs = document.Descendants()
            .Where(element => string.Equals(element.Name.LocalName, "ProjectReference", StringComparison.Ordinal))
            .Select(element => element.Attribute("Include")?.Value)
            .Where(include => !string.IsNullOrWhiteSpace(include));

        foreach (var include in projectRefs)
        {
            var referencedFile = Path.GetFileName(include!);
            if (!referencedFile.StartsWith("EventsWorld.Modules.", StringComparison.Ordinal))
            {
                continue;
            }

            if (isContractsProject)
            {
                if (!referencedFile.EndsWith(".Contracts.csproj", StringComparison.Ordinal))
                {
                    violations.Add($"Contracts project of '{currentModule}' cannot reference implementation project '{referencedFile}'.");
                }
            }
            else if (!referencedFile.EndsWith(".Contracts.csproj", StringComparison.Ordinal))
            {
                violations.Add($"Module '{currentModule}' cannot reference module implementation project '{referencedFile}'. Only '.Contracts' projects are allowed.");
            }
        }
    }

    private static string FindSolutionRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "EventsWorld.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate 'EventsWorld.slnx' from test output directory.");
    }
}
