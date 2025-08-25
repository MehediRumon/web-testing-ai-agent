# Browser Recording Guide

## Overview

The Web Testing AI Agent now runs browsers exclusively in visible mode with enhanced interaction capture capabilities. This guide explains how to use the recording functionality effectively.

## ✅ Key Features

### Enhanced Browser Initialization
- **Always Visible**: Browser always opens in visible mode for proper interaction capture
- **Configurable Timeouts**: Browser initialization timeout configurable via `BrowserInitTimeoutMs`
- **Improved Error Handling**: Detailed error messages and troubleshooting guidance
- **Local Display Required**: System requires local desktop environment

### Environment Support
- **Desktop Environment**: Full visible browser support with user interaction capture
- **Remote X11**: Support for X11 forwarding through SSH for remote access

## Key Features

### ✅ Visible Browser Recording
- **Always Visible**: Browser always opens visibly for user interaction
- **Interactive Capture**: Records all user interactions including clicks, typing, form submissions
- **Local Display Required**: Requires desktop environment or X11 forwarding

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
  "autoExecuteAfterRecording": false
}
```

### ExecutionSettings

```json
{
  "browser": "chrome",
  "timeoutMs": 30000,
  "captureScreenshots": true,
  "stopOnError": true
}
```

## Usage Examples

### 1. API Recording Session

**Desktop Environment:**
```bash
curl -X POST http://localhost:5146/api/recording/start \
  -H "Content-Type: application/json" \
  -d '{
    "name": "User Login Flow",
    "baseUrl": "https://example.com/login",
    "settings": {
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

### Desktop Environment (Required)
- **DISPLAY available**: Browser opens normally in visible mode
- **Full interaction support**: All user interactions captured
- **Best experience**: Ideal for manual test creation

### Remote Server Environment  
- **X11 forwarding**: Use `ssh -X` to enable browser display forwarding
- **Remote desktop**: Use VNC or RDP for GUI access
- **Local display required**: No headless fallback available

### Docker/Container Environment
- **X11 forwarding**: Can enable visible browser with proper X11 setup
- **Volume mounting**: Mount X11 socket for display access
- **Local display required**: Container must have access to display

## Browser Options

### Visible Mode (Always Used)
- Window size: 1280x720
- Browser window position: 0,0
- GPU disabled for consistency
- Visual quality optimized for recording

## Troubleshooting

### Common Issues

1. **"No DISPLAY environment variable found"**
   - Run on desktop environment with GUI
   - Use X11 forwarding: `ssh -X user@server`
   - Test X11 with: `xeyes` command

2. **"ChromeDriver initialization timed out"**
   - Check Chrome/Chromium installation
   - Verify chromedriver is in PATH
   - Check system resources
   - Ensure DISPLAY is available

3. **Browser not opening visibly**
   - Verify DISPLAY environment variable: `echo $DISPLAY`
   - Check X11 forwarding in SSH sessions
   - Ensure window manager is running

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
   - Test in environments with local display

2. **Element Interactions**
   - Use stable selectors (IDs, names) when possible
   - Avoid complex CSS selectors
   - Test element selection after recording

3. **Environment Setup**
   - Always use visible mode (required)
   - Run on desktop environment or with X11 forwarding
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