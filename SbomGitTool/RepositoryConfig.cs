using System.Text.Json.Serialization;

namespace SbomGitTool;

public class RepositoryConfig
{
    [JsonPropertyName("repositories")]
    public List<string> Repositories { get; set; } = new();
}
