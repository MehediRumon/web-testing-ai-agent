# Web Testing AI Agent

Always reference these instructions first and fallback to search or bash commands only when you encounter unexpected information that does not match the info here.

The Web Testing AI Agent is a .NET 8.0 solution that provides an autonomous, goal-driven testing platform accepting natural-language test scenarios, generating executable plans, and validating outcomes using AI reasoning.

## Working Effectively

### Prerequisites and Setup
- Ensure .NET 8.0 SDK is installed: `dotnet --version` should return 8.0.x or higher
- This is a standard .NET solution with no special environment requirements

### Build and Restore
- **Restore packages**: `dotnet restore` -- takes ~2 seconds. Always run this first after cloning.
- **Build solution**: `dotnet build` -- takes ~3 seconds. NEVER CANCEL - this is a fast build.
- **Build warnings are expected**: The CLI project has security warnings about System.Text.Json package version - these are known and documented.

### Running Components

#### API Server
- **Start API**: `cd WebTestingAiAgent.Api && dotnet run` -- starts in ~3 seconds
- **API URL**: http://localhost:5146
- **Swagger UI**: http://localhost:5146/swagger
- **Test endpoint**: `curl -X POST http://localhost:5146/api/runs -H "Content-Type: application/json" -d '{"objective": "Test login", "baseUrl": "https://example.com"}'`

#### Web Application (Blazor WebAssembly)
- **Start web app**: `cd WebTestingAiAgent.Web && dotnet run` -- starts in ~3 seconds  
- **Web URL**: http://localhost:5201
- **Features**: Dashboard, Create Run form, Active Runs monitoring, Reports section

#### CLI Tool
- **Run CLI**: `cd WebTestingAiAgent.Cli && dotnet run -- [command]`
- **Help**: `dotnet run -- --help`
- **Create plan**: `dotnet run -- plan --objective "Test functionality" --baseUrl "https://example.com" --out plan.json`
- **Execute plan**: `dotnet run -- run --plan plan.json --parallel 4`
- **Generate report**: `dotnet run -- report --runId <id> --format html`

## Validation Scenarios

### Essential Testing Workflow
Always validate changes by running through this complete workflow:

1. **Build validation**: `dotnet build` (expect ~3 seconds, no errors)
2. **CLI functionality**: Create a test plan using CLI and verify JSON output is generated
3. **API functionality**: Start API server and test the `/api/runs` POST endpoint
4. **Web interface**: Start web app and verify it loads properly at http://localhost:5201

### Manual Testing Requirements
- **ALWAYS test complete user scenarios** - not just startup/shutdown
- **CLI testing**: Create a plan, verify the JSON file is generated with proper structure
- **API testing**: Use curl or Swagger UI to create a run and verify response contains a runId
- **Web testing**: Load the dashboard and verify the interface renders correctly with navigation and feature cards

## Architecture and Key Locations

### Project Structure
The solution contains 4 projects:

#### 1. WebTestingAiAgent.Core
**Location**: `WebTestingAiAgent.Core/`
- **Data Models**: `Models/AgentConfig.cs`, `Models/PlanJson.cs`, `Models/TestStep.cs`
- **Service Interfaces**: `Interfaces/Services.cs`, `Interfaces/Infrastructure.cs`
- **Key files to check when modifying core functionality**

#### 2. WebTestingAiAgent.Api  
**Location**: `WebTestingAiAgent.Api/`
- **Controllers**: `Controllers/RunsController.cs`, `Controllers/ReportsController.cs`, `Controllers/HooksController.cs`
- **Services**: `Services/` directory contains all service implementations
- **Configuration**: `appsettings.json`, `Properties/launchSettings.json`

#### 3. WebTestingAiAgent.Web
**Location**: `WebTestingAiAgent.Web/`
- **Blazor Components**: Frontend interface for creating and monitoring test runs
- **Configuration**: `Properties/launchSettings.json`

#### 4. WebTestingAiAgent.Cli
**Location**: `WebTestingAiAgent.Cli/`
- **Entry Point**: `Program.cs` - implements plan, run, and report commands
- **Output**: Builds to `ai-agent.dll` (note the custom AssemblyName)

### Development Workflow Recommendations

#### When modifying service interfaces:
1. Update interface in `WebTestingAiAgent.Core/Interfaces/`
2. Update implementation in `WebTestingAiAgent.Api/Services/`
3. Ensure API controllers in `WebTestingAiAgent.Api/Controllers/` use updated interface
4. Test with both CLI and API endpoints

#### When adding new models:
1. Add to `WebTestingAiAgent.Core/Models/`
2. Update validation in `WebTestingAiAgent.Api/Services/ValidationService.cs` if needed
3. Test serialization with CLI JSON output and API endpoints

#### When modifying API endpoints:
1. Update controller in `WebTestingAiAgent.Api/Controllers/`
2. Test with Swagger UI at http://localhost:5146/swagger
3. Verify CLI still works if it calls the modified endpoint

## Common Issues and Solutions

### Build Issues
- **Security warnings**: The CLI project shows System.Text.Json security warnings - these are known and acceptable
- **Nullability warnings**: Some API services show nullability warnings - these do not prevent building
- **Missing dependencies**: Run `dotnet restore` if build fails with missing package errors

### Runtime Issues  
- **Port conflicts**: API uses 5146, Web uses 5201 - ensure these ports are available
- **CORS issues**: Web app is configured to work with API, but check CORS policy in `WebTestingAiAgent.Api/Program.cs` if having issues

### Testing Issues
- **No test projects exist**: This solution currently has no automated tests - validation must be done manually
- **CLI output verification**: Always check that JSON files are created and have valid structure when testing CLI
- **API response verification**: Ensure API responses contain expected fields like runId

## No Automated Linting or Formatting
- **No linting tools configured**: Solution has no EditorConfig, Omnisharp configuration, or automated formatting
- **No pre-commit hooks**: No automated code style enforcement
- **No GitHub Actions**: No CI/CD pipelines configured

## Time Expectations
- **Restore**: ~2 seconds  
- **Build**: ~3 seconds - NEVER CANCEL
- **API startup**: ~3 seconds
- **Web app startup**: ~3 seconds
- **CLI execution**: Nearly instantaneous for plan creation

## Complete Validation Workflow Example
```bash
# 1. Build validation
cd /path/to/repo
dotnet restore
dotnet build

# 2. CLI functionality test
cd WebTestingAiAgent.Cli
dotnet run -- plan --objective "Test functionality" --baseUrl "https://example.com" --out test-plan.json
cat test-plan.json | head -n 10  # Verify JSON structure

# 3. API functionality test  
cd ../WebTestingAiAgent.Api
dotnet run &  # Start in background
sleep 5       # Wait for startup
curl -X POST http://localhost:5146/api/runs -H "Content-Type: application/json" -d '{"objective": "Test login", "baseUrl": "https://example.com"}'
curl -s http://localhost:5146/swagger/index.html | grep "Swagger UI"
kill %1       # Stop API server

# 4. Web interface test
cd ../WebTestingAiAgent.Web  
dotnet run &  # Start in background
sleep 5       # Wait for startup
curl -s http://localhost:5201/ | grep "Web Testing AI Agent"
kill %1       # Stop web server
```