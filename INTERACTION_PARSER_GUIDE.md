# Interaction Parser Documentation

## Overview

The Web Testing AI Agent now supports importing user interaction sequences from a specific text format. This feature allows you to convert recorded interactions into the system's native recording format for test case creation and execution.

## Supported Format

The parser supports interaction sequences in the following format:

```
#<step_number> <ACTION> <selector> ["value"] <url> <timestamp>
```

### Format Components

- **step_number**: Sequential step number (e.g., 2, 3, 4...)
- **ACTION**: Interaction type (`CLICK`, `INPUT`, `SELECT`, `NAVIGATE`, `WAIT`)
- **selector**: Element selector (CSS, ID, class, etc.)
- **value**: Input value in quotes (only for INPUT actions)
- **url**: URL path where the interaction occurred
- **timestamp**: Time in HH:mm:ss format

### Example Format

```
#2 CLICK #UserName /Account/Login 08:20:12
#3 INPUT #UserName "rumon" /Account/Login 08:20:17
#4 INPUT #UserName "rumon." /Account/Login 08:20:20
#5 INPUT #UserName "rumon.onno" /Account/Login 08:20:25
#6 INPUT #UserName "rumon.onnorokom" /Account/Login 08:20:30
#7 INPUT #UserName "rumon.onnorokom@gmail.com" /Account/Login 08:20:38
#8 CLICK div.panel-body /Account/Login 08:20:37
#9 CLICK #Password /Account/Login 08:20:45
#10 INPUT #Password "Mrumon4726" /Account/Login 08:20:52
```

## API Endpoints

### Validate Interactions

**Endpoint**: `POST /api/recording/validate`

**Purpose**: Validate interaction text format and get step count

**Request Body**:
```json
{
  "interactionText": "#2 CLICK #UserName /Account/Login 08:20:12\n#3 INPUT #UserName \"rumon\" /Account/Login 08:20:17"
}
```

**Response**:
```json
{
  "isValid": true,
  "message": "Valid interaction format with 2 steps",
  "stepCount": 2
}
```

### Import Interactions

**Endpoint**: `POST /api/recording/import`

**Purpose**: Import interaction sequence and create a recording session

**Request Body**:
```json
{
  "sessionName": "Login Flow Test",
  "baseUrl": "https://myapp.com",
  "interactionText": "#2 CLICK #UserName /Account/Login 08:20:12\n#3 INPUT #UserName \"rumon\" /Account/Login 08:20:17"
}
```

**Response**: Returns a complete `RecordingSession` object with parsed steps

## CLI Commands

### Validate Interactions

```bash
cd WebTestingAiAgent.Cli
dotnet run -- recording validate-interactions --file interactions.txt
```

**Output**:
```
🔍 Validating interactions from: interactions.txt
✓ Valid interaction format with 9 steps
   📊 Total steps: 9
```

### Import Interactions

```bash
cd WebTestingAiAgent.Cli
dotnet run -- recording import-interactions \
  --file interactions.txt \
  --name "My Test Session" \
  --base-url "https://myapp.com" \
  --out session.json
```

**Output**:
```
📄 Reading interactions from: interactions.txt
📝 Session name: My Test Session
✓ Recording session imported and saved to: session.json
```

## Usage Examples

### 1. Validate a File

```bash
# Create interaction file
cat > login_test.txt << EOF
#2 CLICK #UserName /Account/Login 08:20:12
#3 INPUT #UserName "testuser@example.com" /Account/Login 08:20:17
#4 CLICK #Password /Account/Login 08:20:20
#5 INPUT #Password "password123" /Account/Login 08:20:25
#6 CLICK #LoginButton /Account/Login 08:20:30
EOF

# Validate the format
dotnet run -- recording validate-interactions --file login_test.txt
```

### 2. Import and Create Session

```bash
# Import interactions and save as session
dotnet run -- recording import-interactions \
  --file login_test.txt \
  --name "User Login Flow" \
  --base-url "https://mywebsite.com" \
  --out login_session.json

# View the generated session
cat login_session.json | python3 -m json.tool | head -20
```

### 3. Using the API Directly

```bash
# Start the API server
cd WebTestingAiAgent.Api && dotnet run &

# Validate interactions via API
curl -X POST http://localhost:5146/api/recording/validate \
  -H "Content-Type: application/json" \
  -d '{
    "interactionText": "#2 CLICK #UserName /Account/Login 08:20:12\n#3 INPUT #UserName \"testuser\" /Account/Login 08:20:17"
  }'

# Import interactions via API
curl -X POST http://localhost:5146/api/recording/import \
  -H "Content-Type: application/json" \
  -d '{
    "sessionName": "API Test Session",
    "baseUrl": "https://example.com",
    "interactionText": "#2 CLICK #UserName /Account/Login 08:20:12\n#3 INPUT #UserName \"testuser\" /Account/Login 08:20:17"
  }' | python3 -m json.tool
```

## Generated Output Structure

The parser creates `RecordedStep` objects with the following structure:

```json
{
  "id": "generated-guid",
  "order": 2,
  "action": "click",
  "elementSelector": "#UserName",
  "value": null,
  "url": "/Account/Login",
  "metadata": {
    "originalLine": "#2 CLICK #UserName /Account/Login 08:20:12",
    "parsedTimestamp": "08:20:12",
    "importedAt": "2025-08-25T08:31:04.770402Z"
  },
  "timestamp": "2025-08-25T08:20:12+00:00"
}
```

## Integration with Existing Features

### Convert to Test Case

Once imported, recording sessions can be saved as test cases using existing functionality:

```bash
# After importing a session via API, use the session ID to save as test case
curl -X POST http://localhost:5146/api/recording/{sessionId}/save \
  -H "Content-Type: application/json" \
  -d '{
    "testCaseName": "Login Test Case",
    "description": "Test case created from imported interactions"
  }'
```

### Execute Recording Sessions

Imported sessions can be executed directly:

```bash
curl -X POST http://localhost:5146/api/recording/{sessionId}/execute \
  -H "Content-Type: application/json" \
  -d '{
    "browser": "chrome",
    "headless": false,
    "timeoutMs": 30000
  }'
```

## Error Handling

### Common Issues

1. **Invalid Format**: Lines that don't match the expected pattern are skipped
2. **File Not Found**: CLI provides clear error message and file path
3. **API Server Down**: CLI suggests starting the API server
4. **Invalid Timestamp**: Throws format exception with specific timestamp

### Error Examples

```bash
# Invalid file path
✗ File not found: nonexistent.txt

# Invalid format
✗ Invalid interaction format. Please check the format and try again.

# API server not running
✗ Error importing interactions: HttpRequestException
   Make sure the API server is running: cd WebTestingAiAgent.Api && dotnet run
```

## Best Practices

1. **File Organization**: Keep interaction files organized by test scenario
2. **Naming Conventions**: Use descriptive session names that indicate the test purpose
3. **Base URL**: Provide accurate base URLs for proper test execution
4. **Validation First**: Always validate interaction format before importing
5. **Backup**: Keep original interaction files as documentation

## Troubleshooting

### Parsing Issues

If interactions aren't parsing correctly:

1. Check the format matches exactly: `#<number> <ACTION> <selector> ["value"] <url> <timestamp>`
2. Ensure quoted values use double quotes, not single quotes
3. Verify timestamps are in HH:mm:ss format
4. Check that each line is on a separate line

### API Issues

If API calls fail:

1. Ensure the API server is running on port 5146
2. Check CORS settings if calling from web browser
3. Verify JSON request format is correct
4. Check API server logs for detailed error information

### CLI Issues

If CLI commands fail:

1. Ensure you're in the correct directory (`WebTestingAiAgent.Cli`)
2. Check that the API server is accessible
3. Verify file paths are correct (relative to CLI directory)
4. Ensure .NET 8.0 is installed and available