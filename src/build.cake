﻿///////////////////////////////////////////////////////////////////////////////
// ARGUMENTS
///////////////////////////////////////////////////////////////////////////////

var target = Argument<string>("target", "Default");
var configuration = Argument<string>("configuration", "Release");

//////////////////////////////////////////////////////////////////////
// EXTERNAL NUGET TOOLS
//////////////////////////////////////////////////////////////////////

#Tool "NUnit.ConsoleRunner"

//////////////////////////////////////////////////////////////////////
// EXTERNAL NUGET LIBRARIES
//////////////////////////////////////////////////////////////////////

///////////////////////////////////////////////////////////////////////////////
// GLOBAL VARIABLES
///////////////////////////////////////////////////////////////////////////////

var solutions = GetFiles("./**/*.sln");
var solutionPaths = solutions.Select(solution => solution.GetDirectory());
var srcDir = MakeAbsolute(Directory("./"));
var outputDir = MakeAbsolute(Directory("./../Output"));

///////////////////////////////////////////////////////////////////////////////
// SETUP / TEARDOWN
///////////////////////////////////////////////////////////////////////////////

Setup(() =>
{
	Information("");
	Information("███╗   ██╗ ██████╗██████╗  █████╗ ██╗    ██╗██╗     ███████╗██████╗ ");
	Information("████╗  ██║██╔════╝██╔══██╗██╔══██╗██║    ██║██║     ██╔════╝██╔══██╗");
	Information("██╔██╗ ██║██║     ██████╔╝███████║██║ █╗ ██║██║     █████╗  ██████╔╝");
	Information("██║╚██╗██║██║     ██╔══██╗██╔══██║██║███╗██║██║     ██╔══╝  ██╔══██╗");
	Information("██║ ╚████║╚██████╗██║  ██║██║  ██║╚███╔███╔╝███████╗███████╗██║  ██║");
	Information("╚═╝  ╚═══╝ ╚═════╝╚═╝  ╚═╝╚═╝  ╚═╝ ╚══╝╚══╝ ╚══════╝╚══════╝╚═╝  ╚═╝");
	Information("");
});

Teardown(() =>
{
	Information("Finished running tasks.");
});

//////////////////////////////////////////////////////////////////////
// HELPER METHODS
//////////////////////////////////////////////////////////////////////

void CopyDirectoryVerbose(string sourceDirectory, string targetDirectory)
{
	Information("Copying from directory '" + sourceDirectory + "' to '" + targetDirectory + "'");
	CopyDirectory(sourceDirectory, targetDirectory);
}

void CleanAssemblyXmlFiles(string sourceDirectory)
{
	Information("Cleaning redundant assembly xml files recursively from directory " + sourceDirectory);
	var filesToDelete = System.IO.Directory
		.EnumerateFiles(sourceDirectory, "*.xml", SearchOption.AllDirectories)
		.Where(fileName => System.IO.File.Exists(fileName.Replace(".xml", string.Empty) + ".dll"));
	foreach(var fileName in filesToDelete)
	{
		DeleteFiles(fileName);
	}
}

//////////////////////////////////////////////////////////////////////
// PRIVATE TASKS
//////////////////////////////////////////////////////////////////////

Task("__Clean")
	.Does(() =>
{
	Information("Cleaning {0}", outputDir.ToString());
	CleanDirectories(outputDir.ToString());
	foreach(var path in solutionPaths)
	{
		Information("Cleaning {0}", path);
		try
		{
			CleanDirectories(path + "/**/bin/" + configuration);
		}
		catch
		{
		}

		try
		{
			CleanDirectories(path + "/**/obj/" + configuration);
		}
		catch
		{
		}
	}
});

Task("__RestoreNugetPackages")
	.Does(() =>
{
	foreach(var solution in solutions)
	{
		Information("Restoring NuGet Packages for {0}", solution);
		NuGetRestore(solution, new NuGetRestoreSettings {
			Source = new List<string> {
				"https://nuget.org/api/v2/"
			}
		});     
	}
});

Task("__StageOutput")
	.Does(() =>
{
	CreateDirectory(outputDir.ToString() + @"/NCrawler/");
	CopyDirectoryVerbose("./NCrawler/bin/" + configuration, outputDir.ToString() + @"/NCrawler/");
	CleanAssemblyXmlFiles(outputDir.ToString());
});

Task("__BuildSolutions")
	.Does(() =>
{
	foreach(var solution in solutions)
	{
		Information("Building {0}", solution);
		MSBuild(solution, settings =>
			settings
				.SetConfiguration(configuration)
				.WithProperty("TreatWarningsAsErrors", "true")
				.UseToolVersion(MSBuildToolVersion.NET46)
				.SetVerbosity(Verbosity.Minimal)
				.SetNodeReuse(false));
	}
});

Task("__CreateNuGetPackages")
	.Does(() =>
{
	var nuspecFiles = System.IO.Directory
		.EnumerateFiles(srcDir.ToString(), "*.nuspec", SearchOption.AllDirectories);
	foreach(var nuspecFile in nuspecFiles)
	{
		CreateDirectory(outputDir.ToString() + "/nuget");
		NuGetPack(nuspecFile,
			new NuGetPackSettings
			{
				BasePath = new System.IO.FileInfo(nuspecFile).DirectoryName + "/bin/" + configuration,
				OutputDirectory = outputDir.ToString() + "/nuget"
			});
	}
});

Task("__RunTests")
	.Does(() =>
{
	var unitTestFiles = new string[] {
		"./NCrawler.Toxy.Tests/bin/" + configuration + "/NCrawler.Toxy.Tests.dll",
		"./NCrawler.LanguageDetection.Google.Tests/bin/" + configuration + "/NCrawler.LanguageDetection.Google.Tests.dll",
		"./NCrawler.HtmlProcessor.Tests/bin/" + configuration + "/NCrawler.HtmlProcessor.Tests.dll",
		"./NCrawler.Robots.Tests/bin/" + configuration + "/NCrawler.Robots.Tests.dll",
		"./NCrawler.Flurl.Tests/bin/" + configuration + "/NCrawler.Flurl.Tests.dll",
		"./NCrawler.Robots.Tests/bin/" + configuration + "/NCrawler.Robots.Tests.dll",
		"./NCrawler.SitemapProcessor.Tests/bin/" + configuration + "/NCrawler.SitemapProcessor.Tests.dll"		
	};
	NUnit3(unitTestFiles, new NUnit3Settings { 
		TeamCity=false, 
		Verbose=true,
		NoResults=true,
		Framework="net-4.5"
	});
});

//////////////////////////////////////////////////////////////////////
// BUILD TASKS
//////////////////////////////////////////////////////////////////////

Task("Build")
	.IsDependentOn("__Clean")
	.IsDependentOn("__RestoreNugetPackages")
	.IsDependentOn("__BuildSolutions")
	.IsDependentOn("__RunTests")
	.IsDependentOn("__StageOutput")
	.IsDependentOn("__CreateNuGetPackages");

///////////////////////////////////////////////////////////////////////////////
// PRIMARY TARGETS
///////////////////////////////////////////////////////////////////////////////

Task("Default")
	.IsDependentOn("Build");

///////////////////////////////////////////////////////////////////////////////
// EXECUTION
///////////////////////////////////////////////////////////////////////////////

RunTarget(target);
