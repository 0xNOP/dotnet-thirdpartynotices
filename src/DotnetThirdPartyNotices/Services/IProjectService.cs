using DotnetThirdPartyNotices.Models;
using Microsoft.Build.Evaluation;
using System.Collections.Generic;

namespace DotnetThirdPartyNotices.Services;

internal interface IProjectService
{
    string[] GetProjectFilePaths(string directoryPath);
    IEnumerable<ResolvedFileInfo> ResolveFiles(Project project);
}