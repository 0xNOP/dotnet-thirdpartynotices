# dotnet-thirdpartynotices

[![NuGet](https://img.shields.io/nuget/v/DotnetThirdPartyNotices.svg)](https://www.nuget.org/packages/DotnetThirdPartyNotices/)
[![NuGet downloads](https://img.shields.io/nuget/dt/DotnetThirdPartyNotices.svg)](https://www.nuget.org/packages/DotnetThirdPartyNotices/)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](https://github.com/bugproof/DotnetThirdPartyNotices/blob/master/LICENSE)

![bcs](https://i.giphy.com/media/giFr1HNq8p5gOQ3nCv/200.gif)

## Installation

```
dotnet tool install -g DotnetThirdPartyNotices
```

## Get started

Go inside the project or solution directory and run:

```
dotnet-thirdpartynotices
```

If your project is in a different directory:

```
dotnet-thirdpartynotices <project directory path>
```

Options:

- `--output-filename` allow to change output filename
- `--copy-to-outdir` allow to copy output file to output directory in Release configuration
- `--filter` allow to use regex to filter project files
- `--configuration` allow to change configuration name (tool uses Release by default)
- `--github-token` allow to use GitHub's token

Note that if you use the solution folder and don't use `--copy-to-outdir` then licenses from all projects will be merged to single file.

## How it works

### 1. Resolve assemblies

It uses MSBuild to resolve assemblies that should land in the publish folder or release folder. 

For .NET Core and .NET Standard projects this is done using `ComputeFilesToPublish` target. 

For traditional .NET Framework projects this is done using `ResolveAssemblyReferences` target

### 2. Try to find license based on the information from .nuspec or FileVersionInfo

It tries to find `.nuspec` for those assemblies and attempts to crawl the license content either from licenseUrl or projectUrl. 

Crawling from projectUrl currently works only with github.com projectUrls

Crawling from licenseUrl works with github.com, opensource.org and anything with `text/plain` Content-Type.

If `.nuspec` cannot be found it tries to guess the license using `FileVersionInfo` and checking things like product name.

## Notice

This tool is experimental and might not work with certain projects.
