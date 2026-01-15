# sbom-git-tool

A .NET 8 console application that reads a JSON configuration containing multiple Git repository URLs, clones or updates each repository locally, runs the Microsoft SBOM Tool against each repo, and writes the SBOM output to a specified folder.

## Features

- Reads repository URLs from a JSON configuration file
- Clones repositories if they don't exist locally, or pulls latest changes if they do
- Generates Software Bill of Materials (SBOM) for each repository using Microsoft SBOM Tool
- Outputs SBOM files named `<repositoryName>.sbom.json`
- Provides logging for each operation
- Fails the process if any repository fails

## Prerequisites

- .NET 8 SDK or later
- Microsoft SBOM Tool (`sbom-tool`) installed and available in PATH
  - Install via: `dotnet tool install -g Microsoft.Sbom.DotNetTool`

## Usage

1. Build the application:
   ```bash
   cd SbomGitTool
   dotnet build
   ```

2. Create a configuration file (JSON) with repository URLs:
   ```json
   {
     "repositories": [
       "https://github.com/user/repo1.git",
       "https://github.com/user/repo2.git"
     ]
   }
   ```

3. Run the application:
   ```bash
   dotnet run --project SbomGitTool <config.json> <output-folder>
   ```

   Example:
   ```bash
   dotnet run --project SbomGitTool config.json ./output
   ```

## Configuration File Format

The configuration file is a JSON file with the following structure:

```json
{
  "repositories": [
    "https://github.com/username/repository1.git",
    "https://github.com/username/repository2.git"
  ]
}
```

## Output

For each repository, the tool generates:
- A cloned/updated repository in the `repos/` folder
- An SBOM file named `<repositoryName>.sbom.json` in the specified output folder

## Exit Codes

- `0`: All repositories processed successfully
- `1`: One or more repositories failed or invalid input

## Example

```bash
# Using the example configuration
dotnet run --project SbomGitTool config.example.json ./sbom-output
```

This will:
1. Read the repository URLs from `config.example.json`
2. Clone or update each repository to the `repos/` folder
3. Generate SBOM files for each repository
4. Save the SBOM files to `./sbom-output/` folder with names like `Hello-World.sbom.json`

## License

This project is open source.
