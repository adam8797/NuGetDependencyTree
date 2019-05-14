using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGetDependencyTree
{
    //https://martinbjorkstrom.com/posts/2018-09-19-revisiting-nuget-client-libraries

    class Program
    {
        static async Task Main(string[] args)
        {
            var settings = Settings.LoadDefaultSettings(root: null);
            var sourceRepositoryProvider = new SourceRepositoryProvider(settings, Repository.Provider.GetCoreV3());

            var rootPackageId = "cake.nuget";

            using (var cacheContext = new SourceCacheContext())
            using (var writer = new StreamWriter("output.dgml"))
            {
                var repositories = sourceRepositoryProvider.GetRepositories();
                var availablePackages = new HashSet<SourcePackageDependencyInfo>(PackageIdentityComparer.Default);
                await GetPackageDependencies(
                    new PackageIdentity(rootPackageId, NuGetVersion.Parse("0.30.0")),
                    NuGetFramework.ParseFolder("net46"), cacheContext, NullLogger.Instance, repositories, availablePackages);

                writer.WriteLine($"<?xml version='1.0' encoding='utf-8'?>");
                writer.WriteLine($"<DirectedGraph Title='{rootPackageId}' xmlns='http://schemas.microsoft.com/vs/2009/dgml'>");
                writer.WriteLine($"    <Nodes>");

                foreach (var pac in availablePackages)
                    writer.WriteLine($"        <Node Id='{pac.Id}' Label='{pac.Id}' />");

                writer.WriteLine($"    </Nodes>");
                writer.WriteLine($"    <Links>");
                foreach (var pac in availablePackages)
                {
                    foreach (var dep in pac.Dependencies)
                    {
                        writer.WriteLine($"        <Link Source='{pac.Id}' Target='{dep.Id}' />");
                    }
                }
                writer.WriteLine($"    </Links>");
                writer.WriteLine("</DirectedGraph>");

            }

            async Task GetPackageDependencies(PackageIdentity package,
                NuGetFramework framework,
                SourceCacheContext cacheContext,
                ILogger logger,
                IEnumerable<SourceRepository> repositories,
                ISet<SourcePackageDependencyInfo> availablePackages)
            {
                if (availablePackages.Contains(package)) return;

                foreach (var sourceRepository in repositories)
                {
                    var dependencyInfoResource = await sourceRepository.GetResourceAsync<DependencyInfoResource>();
                    var dependencyInfo = await dependencyInfoResource.ResolvePackage(
                        package, framework, cacheContext, logger, CancellationToken.None);

                    if (dependencyInfo == null) continue;

                    availablePackages.Add(dependencyInfo);
                    foreach (var dependency in dependencyInfo.Dependencies)
                    {
                        await GetPackageDependencies(
                            new PackageIdentity(dependency.Id, dependency.VersionRange.MinVersion),
                            framework, cacheContext, logger, repositories, availablePackages);
                    }
                }
            }
        }
    }
}
