using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;
using NuGet.Versioning;
using Nuke.Common;
using Nuke.Common.Git;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tools.MSBuild;
using Nuke.Common.Tools.GitVersion;
using Nuke.Common.Utilities;
using Nuke.Common.Utilities.Collections;
using static Nuke.Common.EnvironmentInfo;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.IO.PathConstruction;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;

class Build : NukeBuild
{
    public static void Main ()
    {
        Compile();
    }

        static void Compile()        
        {
            try
            {
/*                
                MyGetLocalInstalledPackage(
                    "GitVersion.Tool",
                    @"C:\0\TestBed\test2\build\obj\project.assets.json",
                    null,
                    true);
*/                
                MyGetLocalInstalledPackage(
                    "GitVersion.Tool",
                    @"C:\0\TestBed\test2\build\_build.csproj",
                    null,
                    true);
                
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
/*            
            DotNetTasks.DotNetBuild(_ => _
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration)
                .SetAssemblyVersion(GitVersion.AssemblySemVer)
                .SetFileVersion(GitVersion.AssemblySemFileVer)
                .SetInformationalVersion(GitVersion.InformationalVersion)
                .EnableNoRestore());
*/
        }

    [CanBeNull]
    public static NuGetPackageResolver.InstalledPackage MyGetLocalInstalledPackage(
        string packageId,
        string packagesConfigFile,
        string version = null,
        bool resolveDependencies = true)
    {
        var qq =  NuGetPackageResolver.GetLocalInstalledPackages(
                packagesConfigFile,
                resolveDependencies,
                x => x.PackageId.EqualsOrdinalIgnoreCase(packageId))
            .Where(
                x => x.Id.EqualsOrdinalIgnoreCase(packageId) && (x.Version.ToString() == version || version == null))
            .ToList();
        return qq.SingleOrDefaultOrError(
            x =>
            {
                var qq = x.Id.EqualsOrdinalIgnoreCase(packageId) &&
                         (x.Version.ToString() == version || version == null);
                return qq;
            },
            $"Package '{packageId}' is referenced with multiple versions. Use NuGetPackageResolver and SetToolPath.");

/*        
        var r = MyGetLocalInstalledPackages(
            packagesConfigFile,
            resolveDependencies,
            x =>
            {
                var result = x.PackageId.EqualsOrdinalIgnoreCase(packageId);
                return result;
            });
        var list = r.ToList();
        return r.SingleOrDefaultOrError(
                x =>
                {
                    var qq = x.Id.EqualsOrdinalIgnoreCase(packageId) &&
                           (x.Version.ToString() == version || version == null);
                    return qq;
                },
                $"Package '{packageId}' is referenced with multiple versions. Use NuGetPackageResolver and SetToolPath.");
*/
    }
    
    public static IEnumerable<NuGetPackageResolver.InstalledPackage> MyGetLocalInstalledPackages(
        string packagesConfigFile,
        bool resolveDependencies = true,
        Func<(string PackageId, string Version), bool> preFilter = null)
    {
        if (packagesConfigFile.EndsWithOrdinalIgnoreCase("json"))
        {
            var q = GetLocalInstalledPackagesFromAssetsFile(packagesConfigFile, resolveDependencies, preFilter);
            return q;
        }
        var x=  GetLocalInstalledPackagesFromConfigFile(packagesConfigFile, resolveDependencies);
        var z = x.ToList();
        return x;
    }    
    
        [ItemNotNull]
        private static IEnumerable<NuGetPackageResolver.InstalledPackage> GetLocalInstalledPackagesFromAssetsFile(
            string packagesConfigFile,
            bool resolveDependencies = true,
            Func<(string PackageId, string Version), bool> preFilter = null)
        {
            return GetLocalInstalledPackagesFromAssetsFileWithoutLoading(packagesConfigFile, resolveDependencies)
                .Where(x => preFilter == null || preFilter.Invoke(x))
                .Select(x => GetGlobalInstalledPackage(x.PackageId, x.Version, packagesConfigFile))
                .WhereNotNull();
        }

        [ItemNotNull]
        private static IEnumerable<(string PackageId, string Version)> GetLocalInstalledPackagesFromAssetsFileWithoutLoading(
            string packagesConfigFile,
            bool resolveDependencies = true)
        {
            var assetsObject = SerializationTasks.JsonDeserializeFromFile<JObject>(packagesConfigFile);

            // ReSharper disable HeapView.BoxingAllocation
            var directPackageReferences =
                assetsObject["project"].NotNull()["frameworks"].NotNull()
                    .Single().Single()["dependencies"]
                    ?.Children<JProperty>()
                    .Select(x => x.Name).ToList()
                ?? new List<string>();

            var packageReferences =
                assetsObject["libraries"].NotNull()
                    .Children<JProperty>()
                    .Where(x => x.Value["type"].NotNull().ToString() == "package")
                    .Select(x => x.Name.Split('/'))
                    .Select(x => (
                        PackageId: x.First(),
                        Version: x.Last()
                    ))
                    .Where(x => resolveDependencies || directPackageReferences.Contains(x.PackageId))
                    .OrderByDescending(x => directPackageReferences.Contains(x.PackageId))
                    .ToList();

            var packageDownloads =
                assetsObject["project"].NotNull()["frameworks"].NotNull()
                    .Single().Single()["downloadDependencies"]
                    ?.Children<JObject>()
                    .Select(x => (
                        PackageId: x.Property("name").NotNull().Value.ToString(),
                        Version: x.Property("version").NotNull().Value.ToString().Trim('[', ']').Split(',').First().Trim()
                    )).ToList()
                ?? new List<(string, string)>();
            // ReSharper restore HeapView.BoxingAllocation

            return packageDownloads.Concat(packageReferences);
        }
        
        [ItemNotNull]
        private static IEnumerable<NuGetPackageResolver.InstalledPackage> GetLocalInstalledPackagesFromConfigFile(
            string packagesConfigFile,
            bool resolveDependencies = true)
        {
            var packageIds = XmlTasks.XmlPeek(
                    packagesConfigFile,
                    IsLegacyFile(packagesConfigFile)
                        ? ".//package/@id"
                        : ".//*[local-name() = 'PackageReference' or local-name() = 'PackageDownload']/@Include")
                .Distinct();

            var installedPackages = new HashSet<NuGetPackageResolver.InstalledPackage>(NuGetPackageResolver.InstalledPackage.Comparer.Instance);
            foreach (var packageId in packageIds)
            {
                // TODO: use xml namespaces
                // TODO: version as tag
                var versions = XmlTasks.XmlPeek(
                        packagesConfigFile,
                        IsLegacyFile(packagesConfigFile)
                            ? $".//package[@id='{packageId}']/@version"
                            : $".//*[local-name() = 'PackageReference' or local-name() = 'PackageDownload'][@Include='{packageId}']/@Version")
                    .SelectMany(x => x.Split(';'));

                foreach (var version in versions)
                {
                    var package = GetGlobalInstalledPackage(packageId, version, packagesConfigFile);
                    if (package == null)
                        continue;

                    installedPackages.Add(package);
                    yield return package;
                }
            }

            if (resolveDependencies && !IsLegacyFile(packagesConfigFile))
            {
                var packagesToCheck = new Queue<NuGetPackageResolver.InstalledPackage>(installedPackages);
                while (packagesToCheck.Any())
                {
                    var packageToCheck = packagesToCheck.Dequeue();

                    var dpkgs = GetDependentPackages(packageToCheck, packagesConfigFile);
                    var pkgList = dpkgs.ToList();
                    foreach (var dependentPackage in pkgList)
                    {
                        if (installedPackages.Contains(dependentPackage))
                            continue;

                        installedPackages.Add(dependentPackage);
                        packagesToCheck.Enqueue(dependentPackage);

                        yield return dependentPackage;
                    }
                }
            }
        }

        private static IEnumerable<NuGetPackageResolver.InstalledPackage> GetDependentPackages(NuGetPackageResolver.InstalledPackage packageToCheck, string packagesConfigFile)
        {
            var q1 = packageToCheck.Metadata.GetDependencyGroups().ToList();
            var q2 = q1.SelectMany(x => x.Packages).ToList();
            var r1 = q2
                .Select(x =>
                {
                    var pkg = GetGlobalInstalledPackage(x.Id, x.VersionRange, packagesConfigFile);
                    return pkg;
                })
                .WhereNotNull();
            var r2 = r1.ToList(); 
            var r3 = r2.Distinct(x => new { x.Id, x.Version });
            return r3;
        }

        [CanBeNull]
        public static NuGetPackageResolver.InstalledPackage GetGlobalInstalledPackage(string packageId, [CanBeNull] string version, [CanBeNull] string packagesConfigFile)
        {
            if (version != null &&
                !version.Contains("*") &&
                !version.StartsWith("[") &&
                !version.EndsWith("]"))
                version = $"[{version}]";

            VersionRange.TryParse(version, out var versionRange);
            return GetGlobalInstalledPackage(packageId, versionRange, packagesConfigFile);
        }

        // TODO: add parameter for auto download?
        // TODO: add parameter for highest/lowest?
        [CanBeNull]
        public static NuGetPackageResolver.InstalledPackage GetGlobalInstalledPackage(
            string packageId,
            [CanBeNull] VersionRange versionRange,
            [CanBeNull] string packagesConfigFile,
            bool? includePrereleases = null)
        {
            packageId = packageId.ToLowerInvariant();
            var packagesDirectory = GetPackagesDirectory(packagesConfigFile);
            if (packagesDirectory == null)
                return null;

            var packagesDirectoryInfo = new DirectoryInfo(packagesDirectory);
            var packages = packagesDirectoryInfo
                .GetDirectories(packageId)
                .SelectMany(x => x.GetDirectories())
                .SelectMany(x => x.GetFiles($"{packageId}*.nupkg"))
                .Concat(packagesDirectoryInfo
                    .GetDirectories($"{packageId}*")
                    .SelectMany(x => x.GetFiles($"{packageId}*.nupkg")))
                .Where(x => x.Name.StartsWithOrdinalIgnoreCase(packageId))
                .Select(x => x.FullName);

            var candidatePackages = packages.Select(x => new NuGetPackageResolver.InstalledPackage(x))
                // packages can contain false positives due to present/missing version specification
                .Where(x => x.Id.EqualsOrdinalIgnoreCase(packageId))
                .Where(x => !x.Version.IsPrerelease || !includePrereleases.HasValue || includePrereleases.Value)
                .OrderByDescending(x => x.Version)
                .ToList();

            return versionRange == null
                ? candidatePackages.FirstOrDefault()
                : candidatePackages.SingleOrDefault(x =>
                {
                    if (x.FileName.ToLower().Contains("filesystem"))
                    {
                        var i = 1;
                    }

                    var v = versionRange.FindBestMatch(candidatePackages.Select(y => y.Version));
                    return x.Version == v;
                });
        }

        [CanBeNull]
        public static string GetPackagesConfigFile(string projectDirectory)
        {
            var projectDirectoryInfo = new DirectoryInfo(projectDirectory);
            var packagesConfigFile = projectDirectoryInfo.GetFiles("packages.config").SingleOrDefault()
                                     ?? projectDirectoryInfo.GetFiles("*.csproj")
                                         .SingleOrDefaultOrError("Directory contains multiple project files.");
            return packagesConfigFile?.FullName;
        }

        // TODO: check for config ( repositoryPath / globalPackagesFolder )
        [CanBeNull]
        private static string GetPackagesDirectory([CanBeNull] string packagesConfigFile)
        {
            string TryGetFromEnvironmentVariable()
                => EnvironmentInfo.GetVariable<string>("NUGET_PACKAGES");

            string TryGetGlobalDirectoryFromConfig()
                => GetConfigFiles(packagesConfigFile)
                    .Select(x => new
                                 {
                                     File = x,
                                     Setting = XmlTasks.XmlPeekSingle(x, ".//add[@key='globalPackagesFolder']/@value")
                                 })
                    .Where(x => x.Setting != null)
                    .Select(x => Path.IsPathRooted(x.Setting)
                        ? x.Setting
                        : Path.Combine(Path.GetDirectoryName(x.File).NotNull(), x.Setting))
                    .FirstOrDefault();

            string TryGetDefaultGlobalDirectory()
                => packagesConfigFile == null || !IsLegacyFile(packagesConfigFile)
                    ? Path.Combine(
                        EnvironmentInfo.SpecialFolder(SpecialFolders.UserProfile)
                            .NotNull("EnvironmentInfo.SpecialFolder(SpecialFolders.UserProfile) != null"),
                        ".nuget",
                        "packages")
                    : null;

            string TryGetLocalDirectory()
                => packagesConfigFile != null
                    ? new FileInfo(packagesConfigFile).Directory.NotNull()
                        .DescendantsAndSelf(x => x.Parent)
                        .Where(x => x.GetFiles("*.sln").Any())
                        .Select(x => Path.Combine(x.FullName, "packages"))
                        .FirstOrDefault(Directory.Exists)
                    : null;

            var packagesDirectory = TryGetFromEnvironmentVariable() ??
                                    TryGetGlobalDirectoryFromConfig() ??
                                    TryGetDefaultGlobalDirectory() ??
                                    TryGetLocalDirectory();
            return packagesDirectory != null && Directory.Exists(packagesDirectory)
                ? packagesDirectory
                : null;
        }

        public static bool IsLegacyFile(string packagesConfigFile)
        {
            return packagesConfigFile.EndsWithOrdinalIgnoreCase(".config");
        }

        private static bool IncludesDependencies(string packagesConfigFile)
        {
            return IsLegacyFile(packagesConfigFile);
        }

        private static IEnumerable<string> GetConfigFiles([CanBeNull] string packagesConfigFile)
        {
            var directories = new List<string>();

            if (packagesConfigFile != null)
            {
                directories.AddRange(Directory.GetParent(packagesConfigFile)
                    .DescendantsAndSelf(x => x.Parent)
                    .Select(x => x.FullName));
            }

            if (EnvironmentInfo.IsWin)
            {
                directories.Add(Path.Combine(
                    EnvironmentInfo.SpecialFolder(SpecialFolders.ApplicationData).NotNull(),
                    "NuGet"));

                directories.Add(Path.Combine(
                    EnvironmentInfo.SpecialFolder(SpecialFolders.ProgramFilesX86).NotNull(),
                    "NuGet",
                    "Config"));
            }

            if (EnvironmentInfo.IsUnix)
            {
                directories.Add(Path.Combine(
                    EnvironmentInfo.SpecialFolder(SpecialFolders.UserProfile).NotNull(),
                    ".config",
                    "NuGet"));

                directories.Add(Path.Combine(
                    EnvironmentInfo.SpecialFolder(SpecialFolders.UserProfile).NotNull(),
                    ".nuget",
                    "NuGet"));

                var dataHomeDirectoy = EnvironmentInfo.GetVariable<string>("XDG_DATA_HOME");
                if (!string.IsNullOrEmpty(dataHomeDirectoy))
                {
                    directories.Add(dataHomeDirectoy);
                }
                else
                {
                    directories.Add(Path.Combine(
                        EnvironmentInfo.SpecialFolder(SpecialFolders.UserProfile).NotNull(),
                        ".local",
                        "share"));

                    // TODO: /usr/local/share
                }
            }

            return directories
                .Where(Directory.Exists)
                .SelectMany(x => Directory.GetFiles(x, "nuget.config", SearchOption.TopDirectoryOnly))
                .Where(File.Exists);
        }
    
}
