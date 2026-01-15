using System.Diagnostics;
using System.Text.Json;
using LibGit2Sharp;
using SbomGitTool;

if (args.Length < 2)
{
    Console.WriteLine("Usage: SbomGitTool <config.json> <output-folder>");
    Console.WriteLine("  config.json: Path to JSON configuration file containing repository URLs");
    Console.WriteLine("  output-folder: Path to folder where SBOM files will be written");
    return 1;
}

string configPath = args[0];
string outputFolder = args[1];

// Validate inputs
if (!File.Exists(configPath))
{
    Console.WriteLine($"Error: Configuration file not found: {configPath}");
    return 1;
}

// Create output folder if it doesn't exist
try
{
    Directory.CreateDirectory(outputFolder);
    Console.WriteLine($"Output folder: {outputFolder}");
}
catch (Exception ex)
{
    Console.WriteLine($"Error creating output folder: {ex.Message}");
    return 1;
}

// Read configuration
RepositoryConfig config;
try
{
    string jsonContent = File.ReadAllText(configPath);
    config = JsonSerializer.Deserialize<RepositoryConfig>(jsonContent) ?? new RepositoryConfig();
    Console.WriteLine($"Loaded configuration with {config.Repositories.Count} repositories");
}
catch (Exception ex)
{
    Console.WriteLine($"Error reading configuration file: {ex.Message}");
    return 1;
}

if (config.Repositories.Count == 0)
{
    Console.WriteLine("Error: No repositories found in configuration file");
    return 1;
}

// Process each repository
string reposFolder = Path.Combine(Directory.GetCurrentDirectory(), "repos");
Directory.CreateDirectory(reposFolder);

bool allSucceeded = true;

foreach (string repoUrl in config.Repositories)
{
    try
    {
        Console.WriteLine($"\n--- Processing repository: {repoUrl} ---");
        
        // Validate repository URL
        if (!IsValidGitUrl(repoUrl))
        {
            Console.WriteLine($"Error: Invalid repository URL: {repoUrl}");
            allSucceeded = false;
            continue;
        }
        
        // Extract repository name from URL
        string repoName = GetRepositoryName(repoUrl);
        Console.WriteLine($"Repository name: {repoName}");
        
        // Clone or update repository
        string repoPath = Path.Combine(reposFolder, repoName);
        
        if (Directory.Exists(repoPath))
        {
            Console.WriteLine($"Repository already exists, pulling latest changes...");
            try
            {
                using var repo = new Repository(repoPath);
                Commands.Pull(repo, new Signature("SBOM Tool", "sbom@tool.local", DateTimeOffset.Now),
                    new PullOptions());
                Console.WriteLine("Repository updated successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not pull latest changes: {ex.Message}");
                Console.WriteLine("Continuing with existing repository state...");
            }
        }
        else
        {
            Console.WriteLine($"Cloning repository to: {repoPath}");
            Repository.Clone(repoUrl, repoPath);
            Console.WriteLine("Repository cloned successfully");
        }
        
        // Run SBOM tool
        Console.WriteLine("Running Microsoft SBOM Tool...");
        string sbomOutputPath = Path.Combine(outputFolder, $"{repoName}.sbom.json");
        
        bool sbomSuccess = RunSbomTool(repoPath, outputFolder, repoName);
        
        if (!sbomSuccess)
        {
            Console.WriteLine($"Error: SBOM generation failed for repository: {repoUrl}");
            allSucceeded = false;
        }
        else
        {
            Console.WriteLine($"SBOM generated successfully: {sbomOutputPath}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error processing repository {repoUrl}: {ex.Message}");
        allSucceeded = false;
    }
}

if (allSucceeded)
{
    Console.WriteLine("\n=== All repositories processed successfully ===");
    return 0;
}
else
{
    Console.WriteLine("\n=== One or more repositories failed ===");
    return 1;
}

static bool IsValidGitUrl(string url)
{
    if (string.IsNullOrWhiteSpace(url))
        return false;
    
    // Check if it's a valid URI
    if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
        return false;
    
    // Accept http, https, git, ssh schemes
    string scheme = uri.Scheme.ToLowerInvariant();
    if (scheme != "http" && scheme != "https" && scheme != "git" && scheme != "ssh")
        return false;
    
    return true;
}

static string GetRepositoryName(string repoUrl)
{
    // Extract repository name from URL
    // e.g., https://github.com/user/repo.git -> repo
    string name = repoUrl.TrimEnd('/');
    
    if (name.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
    {
        name = name[..^4];
    }
    
    int lastSlash = name.LastIndexOf('/');
    if (lastSlash >= 0)
    {
        name = name[(lastSlash + 1)..];
    }
    
    return name;
}

static bool RunSbomTool(string repoPath, string outputFolder, string repoName)
{
    try
    {
        // Create a temporary manifest directory for sbom-tool
        string manifestDir = Path.Combine(outputFolder, $"_manifest_{repoName}");
        Directory.CreateDirectory(manifestDir);
        
        var startInfo = new ProcessStartInfo
        {
            FileName = "sbom-tool",
            Arguments = $"generate -b \"{repoPath}\" -bc \"{repoPath}\" -pn \"{repoName}\" -pv \"1.0.0\" -ps \"Organization\" -nsb \"https://example.com\" -m \"{manifestDir}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        
        using var process = Process.Start(startInfo);
        if (process == null)
        {
            Console.WriteLine("Error: Could not start sbom-tool process");
            return false;
        }
        
        // Read output asynchronously to prevent deadlocks
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        
        process.WaitForExit();
        
        string output = outputTask.Result;
        string error = errorTask.Result;
        
        if (!string.IsNullOrWhiteSpace(output))
        {
            Console.WriteLine($"SBOM Tool Output: {output}");
        }
        
        if (!string.IsNullOrWhiteSpace(error))
        {
            Console.WriteLine($"SBOM Tool Error: {error}");
        }
        
        if (process.ExitCode != 0)
        {
            Console.WriteLine($"SBOM Tool exited with code: {process.ExitCode}");
            return false;
        }
        
        // Find the generated SBOM file and rename it
        string manifestFile = Path.Combine(manifestDir, "_manifest", "spdx_2.2", "manifest.spdx.json");
        string targetFile = Path.Combine(outputFolder, $"{repoName}.sbom.json");
        
        if (File.Exists(manifestFile))
        {
            File.Copy(manifestFile, targetFile, true);
            // Clean up temporary manifest directory
            try
            {
                Directory.Delete(manifestDir, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
            return true;
        }
        else
        {
            Console.WriteLine($"Warning: SBOM file not found at expected location: {manifestFile}");
            return false;
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error running SBOM tool: {ex.Message}");
        return false;
    }
}
