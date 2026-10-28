using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ZeroDrive.Core.Config;

/// <summary>
/// The workspace mapping config
/// </summary>
/// <param name="WorkspaceId"></param>
/// <param name="LocalPath"></param>
public sealed record WorkspaceMappingConfig(Guid WorkspaceId, string LocalPath);

/// <summary>
/// Workspace master list
/// </summary>
/// <param name="Workspaces"></param>
public sealed record ClientConfig(List<WorkspaceMappingConfig> Workspaces);