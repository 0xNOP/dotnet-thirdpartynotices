using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace DotnetThirdPartyNotices.Extensions;

internal static class AssemblyExtensions
{
    public static IEnumerable<T> GetInstances<T>(this Assembly assembly) where T : class => assembly.GetTypes()
        .Where(t => t.IsClass && typeof(T).IsAssignableFrom(t))
        .Select(Activator.CreateInstance)
        .OfType<T>();
}