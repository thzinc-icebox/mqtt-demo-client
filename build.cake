#tool nuget:?package=NUnit.ConsoleRunner&version=3.4.0
#tool "nuget:?package=xunit.runner.console"
#addin "Cake.Docker"

//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");

//////////////////////////////////////////////////////////////////////
// PREPARATION
//////////////////////////////////////////////////////////////////////

// Define directories.
var buildDir = Directory("./") + Directory(configuration);
var currentDirectory = MakeAbsolute(Directory("./"));

var version = "";

//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////
Task("GetVersion")
    .Does(() =>
    {
        var arguments = new ProcessSettings
        {
            Arguments = " -c \"git describe --tags --always --long | cut -d '-' -f 1-2 | sed 's/-/./g'\"",
            RedirectStandardOutput = true
        };

        using(var process = StartAndReturnProcess("/bin/bash", arguments))
        {
            process.WaitForExit();
            var exitCode = process.GetExitCode();
            if(exitCode != 0)
                throw new Exception("git version did not exit cleanly");

            version = process.GetStandardOutput().First();
            Information($"Version is : {version}");
        }
    });

Task("Clean")
    .IsDependentOn("GetVersion")
    .Does(() =>
    {
        CleanDirectory(buildDir);
    });

Task("Build")
    .Does(() =>
    {
        var settings = new DockerRunSettings
        {
            Volume = new string[] { $"{currentDirectory}:/artifacts"},
            Workdir = "/artifacts",
            Env = new string[]
            {
                $"TEST_URL=\"{EnvironmentVariable("TEST_URL")}\""
            }
        };

        DockerRun(settings, "syncromatics/build-box", "/artifacts/build.sh -t InnerBuild --verbosity Diagnostic");
    });

Task("InnerRestore")
    .IsDependentOn("Clean")
    .Does(() =>
    {
        DotNetCoreRestore();
    });

Task("InnerBuild")
    .IsDependentOn("InnerRestore")
    .Does(() =>
    {
        var buildSettings = new ProcessSettings
        {
            Arguments = $"/property:Configuration={configuration}"
        };

        using(var process = StartAndReturnProcess("msbuild", buildSettings))
        {
            process.WaitForExit();
            var exitCode = process.GetExitCode();
            if(exitCode != 0)
                throw new Exception("Build Failed.");
        }
    });

//////////////////////////////////////////////////////////////////////
// TASK TARGETS
//////////////////////////////////////////////////////////////////////

Task("Default")
    .IsDependentOn("Build");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);
