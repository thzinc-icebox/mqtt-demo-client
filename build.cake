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
var buildDir = Directory("./bin") + Directory(configuration);
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
        };

        DockerRun(settings, "syncromatics/build-box", "/artifacts/build.sh -t InnerBundle --verbosity Diagnostic");
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

Task("InnerLink")
    .IsDependentOn("InnerBuild")
    .Does(() => 
    {
        var workingDirectory = buildDir + Directory("net46");
        var assemblies = GetFiles($"{workingDirectory}/*.exe").Select(f => $"-a {f}");
        var linkerSettings = new ProcessSettings
        {
            WorkingDirectory = workingDirectory,
            Arguments = $"-c copy {string.Join(" ", assemblies)}"
        };

        Information($"Working directory is {linkerSettings.WorkingDirectory}");

        using(var process = StartAndReturnProcess("monolinker", linkerSettings))
        {
            process.WaitForExit();
            var exitCode = process.GetExitCode();
            if(exitCode != 0)
                throw new Exception("Linker Failed.");
        }
    });

Task("InnerBundle")
    .IsDependentOn("InnerLink")
    .IsDependentOn("InnerFetchTarget")
    .Does(() => 
    {
        var bundledFile = MakeAbsolute(buildDir + File("mqtt-demo-client"));
        var workingDirectory = buildDir + Directory("net46/output");
        var assemblies = GetFiles($"{workingDirectory}/app.exe")
            .Concat(GetFiles($"{workingDirectory}/*.dll"));

        var bundleSettings = new ProcessSettings
        {
            WorkingDirectory = workingDirectory,
            Arguments = $"--simple --cross mono-5.0.1-debian-8-arm.zip --static -z --config /etc/mono/config -o {bundledFile} {string.Join(" ", assemblies)}"
        };

        Information($"Working directory is {bundleSettings.WorkingDirectory}");

        using(var process = StartAndReturnProcess("mkbundle", bundleSettings))
        {
            process.WaitForExit();
            var exitCode = process.GetExitCode();
            if(exitCode != 0)
                throw new Exception("Bundler Failed.");
        }
    });

Task("InnerFetchTarget")
    .Does(() =>
    {
        var bundlerSettings = new ProcessSettings
        {
            Arguments = $"--fetch-target mono-5.0.1-debian-8-arm.zip"
        };

        using(var process = StartAndReturnProcess("mkbundle", bundlerSettings))
        {
            process.WaitForExit();
            var exitCode = process.GetExitCode();
            if(exitCode != 0)
                throw new Exception("Linker fetch target failed.");
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
