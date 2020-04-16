using System;
using Build;
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

[AzurePipelines(
     AzurePipelinesImage.UbuntuLatest
   // , AzurePipelinesImage.WindowsLatest,
   //  AzurePipelinesImage.MacOsLatest
   ,
    InvokedTargets = new[] { nameof(Pack) })]
[CheckBuildProjectConfigurations]
[UnsetVisualStudioEnvironmentVariables]
// ReSharper disable once CheckNamespace
// ReSharper disable once ClassNeverInstantiated.Global
class AppBuild : NukeBuild
{
    public static int Main () => Execute<AppBuild>(x => x.Pack);

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
       .DependsOn(Compile, Test)
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
            TagCloudCoreProject.SaveAndUpdateVersionFile(NextVersion);
        });
}
