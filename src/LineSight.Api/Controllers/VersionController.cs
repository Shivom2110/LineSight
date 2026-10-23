using System.Reflection;
using LineSight.Core.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace LineSight.Api.Controllers;

[ApiController]
[Route("api/version")]
public sealed class VersionController : ControllerBase
{
    // The .NET SDK stamps the git commit into the informational version: "1.0.0+<sha>".
    private static readonly VersionResponse Current = Create();

    /// <summary>Which build is running. The deploy pipeline checks Commit against the pushed SHA.</summary>
    [HttpGet]
    public VersionResponse Get() => Current;

    private static VersionResponse Create()
    {
        var informational = typeof(VersionController).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
        var parts = informational.Split('+', 2);
        return new VersionResponse(parts[0], parts.Length > 1 ? parts[1] : null);
    }
}
