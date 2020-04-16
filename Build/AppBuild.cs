using System;
using Build;
using Microsoft.Extensions.Configuration;
using Nuke.Common;
using Nuke.Common.CI;
using Nuke.Common.CI.AzurePipelines;
using Nuke.Common.Execution;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.Git;
using Nuke.Common.Tools.NuGet;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

public class NugetConfig
{
    public string ApiKey { get; set; }
    public string Source { get; set; }
}

[AzurePipelines(
     AzurePipelinesImage.UbuntuLatest,
    InvokedTargets = new[] { nameof(Push) })]
[CheckBuildProjectConfigurations]
[UnsetVisualStudioEnvironmentVariables]
// ReSharper disable once CheckNamespace
// ReSharper disable once ClassNeverInstantiated.Global
class AppBuild : NukeBuild
{
    public AppBuild()
    {
        var configuration = new ConfigurationBuilder()
               .AddJsonFile("appsettings.json", true)
                .AddUserSecrets(typeof(AppBuild).Assembly)
               .Build();

        NugetConfig = configuration.GetSection("Nuget").Get<NugetConfig>();
    }

    public NugetConfig NugetConfig { get; }

    public static int Main () => Execute<AppBuild>(x => x.Push);

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Solution] readonly Solution Solution;
    [GitRepository] readonly GitRepository GitRepository;
    [PathExecutable] readonly Tool NuGet;

    AbsolutePath PackageOutputDirectory => RootDirectory / "output";
    AbsolutePath TestProject => RootDirectory / "TagCloud.Core.Tests" / "TagCloud.Core.Tests.csproj";
    Project TagCloudCoreProject => Solution.GetProject("TagCloud.Core");
    AbsolutePath TagCloudCoreOutputPath => RootDirectory / "TagCloud.Core" / "bin" / Configuration / "netstandard2.0";
    Version NextVersion => TagCloudCoreProject.GetNextVersion();
    readonly string NuspecFileName = "Sparc.TagCloud.Core.nuspec";
    AbsolutePath PackageFile =>  PackageOutputDirectory / $"Sparc.TagCloud.Core.{NextVersion.ThreeString()}.nupkg";

    Target Clean => _ => _
        .Before(Restore)
        .Executes(() =>
        {
            EnsureCleanDirectory(PackageOutputDirectory);
        });

    Target Restore => _ => _
        .Executes(() =>
        {
            DotNetRestore(s => s.SetProjectFile(Solution));
        });

    Target Compile => _ => _
       .DependsOn(Restore)
       .Executes(() =>
        {
            var version = NextVersion;
            TagCloudCoreProject.TagRepository(version);
            TagCloudCoreProject.SaveAndUpdateVersionFile(NextVersion);

            DotNetBuild(
                s => s
                   .SetProjectFile(Solution)
                   .SetConfiguration(Configuration)
                   .SetAssemblyVersion(version.ToString())
                   .SetFileVersion(version.ToString())
                   .SetInformationalVersion(version.ToString())
                   .EnableNoRestore());
        });

    Target Test => _ => _
       .DependsOn(Compile)
       .Executes(() =>
        {
            DotNetTest(s => s.SetProjectFile(TestProject));
        });

    Target Pack => _ => _
       .Produces(PackageOutputDirectory / "*.nupkg")
       .Requires(() => GitTasks.GitHasCleanWorkingCopy())
       .DependsOn(Test)
       .Executes(() =>
        {
            NuGetTasks.NuGetPack(
                s => s
                   .SetTargetPath(NuspecFileName)
                   .DisableBuild()
                   .SetWorkingDirectory(TagCloudCoreOutputPath)
                   .SetConfiguration(Configuration)
                   .SetVersion(NextVersion.ThreeString())
                   .SetOutputDirectory(PackageOutputDirectory)
            );
        });

    Target Push => _ => _
       .DependsOn(Clean, Pack)
       .Executes(() =>
        {
            NuGetTasks.NuGetPush(
                s => s
                   .SetTargetPath(PackageFile)
                   .SetWorkingDirectory(PackageOutputDirectory)
                   .SetApiKey(NugetConfig.ApiKey)
                   .SetSource(NugetConfig.Source)
            );
        });
}
