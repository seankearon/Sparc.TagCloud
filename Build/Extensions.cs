using System;
using System.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tools.Git;

namespace Build
{
    public static class Extensions
    {
        public static string ReadText(this    string  filePath)                   => File.ReadAllText(filePath);
        public static void   SaveText(this    string  filePath, string  contents) => File.WriteAllText(filePath, contents);
        public static void   SaveVersion(this Project project,  Version version)  => project.VersionFilePath().SaveText(version.ThreeString());

        public static string VersionFilePath(this Project project)
        {
            return project.Directory / $"Ver.{project.Name.ToLowerInvariant()}.txt";
        }

        public static string ThreeString(this Version version) => $"{version.Major}.{version.Minor}.{version.Build}";

        public static Project TagRepository(this Project project, Version version)
        {
            GitTasks.Git($"tag {project.Name}-{version.ThreeString()}");
            return project;
        }

        public static Project SaveAndUpdateVersionFile(this Project project, Version version)
        {
            project.SaveVersion(version);
            GitTasks.Git($"stage {project.VersionFilePath()}");
            GitTasks.Git("commit -m \"Version number updated by the build.\" ");
            // GitTasks.Git("push");
            return project;
        }

        public static Version GetNextVersion(this Project project)
        {
            var currentVersionString = project.VersionFilePath().ReadText();
            var version              = Version.Parse(currentVersionString);
            return new Version(version.Major, version.Minor, version.Build + 1);
        }
    }}