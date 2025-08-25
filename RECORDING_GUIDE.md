# Browser Recording Guide

## Overview

The Web Testing AI Agent now supports enhanced browser recording with improved visibility and interaction capture capabilities. This guide explains how to use the recording functionality effectively.

## ✅ Key Improvements Made

### Enhanced Browser Initialization
- **Configurable Timeouts**: Browser initialization timeout now configurable via `BrowserInitTimeoutMs`
- **Automatic Fallback**: System tries visible mode first, falls back to headless if needed
- **Improved Error Handling**: Detailed error messages and troubleshooting guidance
- **Virtual Display Integration**: Automatic Xvfb setup when no DISPLAY available

### Robust Environment Support
- **Desktop Environment**: Full visible browser support with user interaction capture
- **Server Environment**: Automatic virtual display setup with Xvfb
- **Container Environment**: Docker-compatible with X11 forwarding support
- **CI/CD Environment**: Graceful fallback ensures recording works in all environments

## Key Features

### ✅ Visible Browser Recording
- **Default Mode**: Browser opens visibly for user interaction
- **Force Visible**: Can force visible mode even in environments without display
- **Interactive Capture**: Records all user interactions including clicks, typing, form submissions

### ✅ Comprehensive Interaction Capture
The recording system captures:
- **Clicks** - on buttons, links, and any clickable elements
- **Text Input** - typing in text fields, emails, passwords, textareas
- **Form Interactions** - dropdown selections, checkboxes, radio buttons
- **Form Submissions** - complete form submission events
- **Navigation** - URL changes and page navigation

### ✅ Smart Element Selection
- Uses ID selectors when available
- Falls back to name attributes
- Generates CSS selectors with class names
- Includes position-based selectors for disambiguation

## Configuration Options

### RecordingSettings

```json
{
  "captureScreenshots": true,
  "captureNetwork": false,
  "captureConsole": false,
  "maxSteps": 100,
  "timeoutMs": 30000,
  "maxRecordingMinutes": 60,
  "autoExecuteAfterRecording": false,
  "headless": false,           // New: Control browser visibility
  "forceVisible": true         // New: Force visible even without DISPLAY
}
```

### ExecutionSettings

```json
{
  "browser": "chrome",
  "headless": false,           // Changed: Now defaults to false
  "timeoutMs": 30000,
  "captureScreenshots": true,
  "stopOnError": true
}
```

## Usage Examples

### 1. API Recording Session

```bash
curl -X POST http://localhost:5146/api/recording/start \
  -H "Content-Type: application/json" \
  -d '{
    "name": "User Login Flow",
    "baseUrl": "https://example.com/login",
    "settings": {
      "headless": false,
      "forceVisible": true,
      "captureScreenshots": true,
      "maxSteps": 50
    }
  }'
```

### 2. CLI Recording

```bash
# Start a new recording session
dotnet run -- recording start \
  --name "Shopping Cart Test" \
  --url "https://shop.example.com" \
  --visible true

# List active recordings
dotnet run -- recording list

# Stop and save recording
dotnet run -- recording stop <session-id> --save-as "shopping-cart-test"
```

### 3. Web Interface

1. Navigate to http://localhost:5201/recording
2. Click "Start New Recording"
3. Configure recording settings:
   - Set name and base URL
   - Ensure "Visible Browser" is enabled
   - Configure capture options
4. Click "Start Recording"
5. Browser will open visibly for interaction
6. Perform your test actions in the browser
7. Return to web interface to stop recording

## Environment Considerations

### Desktop Environment (Recommended)
- **DISPLAY available**: Browser opens normally in visible mode
- **Full interaction support**: All user interactions captured
- **Best experience**: Ideal for manual test creation

### Server/Headless Environment
- **No DISPLAY**: System will warn but attempt visible mode if `forceVisible=true`
- **Fallback available**: Can fall back to headless mode if needed
- **Limited interaction**: Headless mode supports programmatic actions only

### Docker/Container Environment
- **X11 forwarding**: Can enable visible browser with proper X11 setup
- **VNC/Remote desktop**: Use remote desktop solutions for GUI access
- **Headless fallback**: Falls back gracefully when display unavailable

## Browser Options

### Visible Mode (Recording)
- Window size: 1280x720
- Starts maximized
- GPU acceleration enabled (when available)
- Visual quality optimized

### Headless Mode (Execution)
- No visual output
- GPU disabled for performance
- Optimized for server environments

## Troubleshooting

### Common Issues

1. **"No DISPLAY environment variable found"**
   - This is expected in server environments
   - Set `forceVisible: false` for server environments
   - Use headless mode for automated execution

2. **"ChromeDriver initialization timed out"**
   - Check Chrome/Chromium installation
   - Verify chromedriver is in PATH
   - Check system resources

3. **Browser not opening visibly**
   - Verify DISPLAY environment variable
   - Check X11 forwarding in SSH sessions
   - Try headless mode as fallback

### Debug Mode

Enable verbose logging by setting environment variable:
```bash
export ASPNETCORE_ENVIRONMENT=Development
```

This will show detailed browser startup and interaction capture logs.

## Best Practices

1. **Recording Sessions**
   - Use descriptive names for recording sessions
   - Keep recordings focused on specific workflows
   - Test in the same environment where you'll execute

2. **Element Interactions**
   - Use stable selectors (IDs, names) when possible
   - Avoid complex CSS selectors
   - Test element selection after recording

3. **Environment Setup**
   - Use visible mode for recording creation
   - Use headless mode for automated execution
   - Configure timeouts based on application speed

4. **Recording Quality**
   - Perform actions slowly for better capture
   - Wait for page loads between actions
   - Verify important interactions are captured

## Integration with CI/CD

For automated test execution in CI/CD pipelines:

```yaml
# Example GitHub Actions
- name: Run Tests
  run: |
    dotnet run -- execution start \
      --testcase "user-login-flow" \
      --headless true \
      --timeout 60000
```

The recording functionality provides a robust foundation for creating maintainable web tests that capture real user interactions and can be executed reliably in various environments.