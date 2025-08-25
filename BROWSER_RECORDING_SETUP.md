# Browser Recording Setup Guide

This guide helps you set up browser recording for the Web Testing AI Agent to capture user interactions during test case creation.

## 🎯 Overview

The Web Testing AI Agent now runs browsers **exclusively in visible mode** where:
- Browser always opens visibly for user interaction capture
- All user interactions (clicks, typing, form submissions) are automatically captured
- Test cases can be saved and replayed later
- **No headless fallback** - system requires a local display environment

## ✅ Setup (Desktop Environment Required)

**Important**: The system now **requires a local display** and desktop environment:

```bash
# 1. Ensure Chrome is installed
google-chrome --version

# 2. Verify display is available
echo $DISPLAY
# Should show display number (e.g., :0, :1, localhost:10.0)

# 3. Start the API
cd WebTestingAiAgent.Api
dotnet run

# 4. Create a recording session (browser will open visibly)
curl -X POST http://localhost:5146/api/recording/start \
  -H "Content-Type: application/json" \
  -d '{
    "name": "My Test Recording",
    "baseUrl": "https://example.com",
    "settings": {
      "headless": false,
      "timeoutMs": 30000
    }
  }'
```

## 🖥️ Remote Server Setup

For remote server environments, use X11 forwarding:

```bash
# Use X11 forwarding (SSH)
ssh -X user@your-server

# Verify X11 forwarding works
echo $DISPLAY
# Should show something like localhost:10.0

# Test X11 connection
xeyes  # Should open a window if X11 is working

# Now run recording - will use forwarded display
```

## 🐳 Docker Environment Setup

For Docker containers, add X11 support:

```dockerfile
# Add to your Dockerfile
RUN apt-get update && apt-get install -y \
    xvfb \
    google-chrome-stable \
    && rm -rf /var/lib/apt/lists/*

# Set display environment
ENV DISPLAY=:99
```

Or use docker-compose with X11 forwarding:

```yaml
version: '3.8'
services:
  web-testing-agent:
    build: .
    volumes:
      - /tmp/.X11-unix:/tmp/.X11-unix:rw
    environment:
      - DISPLAY=${DISPLAY}
    network_mode: host
```

## ⚙️ Configuration Options

### Recording Settings

```json
{
  "settings": {
    "headless": false,               // Use visible browser
    "forceVisible": true,            // Force visible even without DISPLAY
    "useVirtualDisplay": false,      // Only use virtual display when explicitly enabled
    "timeoutMs": 30000,             // Page interaction timeout
    "captureScreenshots": true,      // Capture screenshots during recording
    "maxSteps": 100,                // Maximum recorded steps
    "maxRecordingMinutes": 60       // Maximum recording duration
  }
}
```

### Execution Settings

```json
{
  "browserInitTimeoutMs": 30000,  // Browser startup timeout
  "captureScreenshots": true,     // Enable screenshots
  "timeoutMs": 30000             // General timeout
}
```

## 🔧 Troubleshooting

### Browser Fails to Start

**Symptom**: "ChromeDriver initialization timed out"

**Solutions**:
1. **Check Chrome installation**: `google-chrome --version`
2. **Verify DISPLAY**: `echo $DISPLAY` (must show display number)
3. **Run on desktop environment**: Ensure GUI is available
4. **Check X11 forwarding**: If using SSH, ensure `-X` flag is used

### No Display Available

**Symptom**: "No DISPLAY environment variable found" or browser fails to start

**Solutions**:
1. **Desktop Environment**: Run on a machine with GUI (required)
2. **X11 Forwarding**: Use `ssh -X` to connect to server with display forwarding
3. **Test X11**: Try running `xeyes` to verify X11 is working

### Browser Not Opening Visibly

**Symptom**: No browser window appears

**Solutions**:
1. **Verify DISPLAY**: `echo $DISPLAY`
2. **Check window manager**: Ensure a window manager is running
3. **Test with simple browser**: Try `google-chrome --version` or `chromium-browser --version`

### Recording Not Capturing Interactions

**Symptom**: Recording session starts but no interactions captured

**Solutions**:
1. **Check JavaScript Console**: Ensure no script errors
2. **Verify Page Load**: Wait for page to fully load before interacting
3. **Test on Simple Pages**: Start with basic HTML forms
4. **Check Network Connectivity**: Ensure the target URL is accessible

## 📝 Usage Examples

### CLI Recording

```bash
cd WebTestingAiAgent.Cli

# Create a test plan (this doesn't need browser)
dotnet run -- plan --objective "Test login form" --baseUrl "https://example.com" --out test-plan.json

# For actual recording, use the API endpoints
```

### API Recording

```bash
# Start recording session
RESPONSE=$(curl -s -X POST http://localhost:5146/api/recording/start \
  -H "Content-Type: application/json" \
  -d '{"name": "Login Test", "baseUrl": "https://example.com"}')

SESSION_ID=$(echo $RESPONSE | jq -r '.id')

# Interact with the visible browser...
# (Browser will capture interactions automatically)

# Stop recording
curl -X POST http://localhost:5146/api/recording/$SESSION_ID/stop

# Get recorded steps
curl http://localhost:5146/api/recording/$SESSION_ID/steps
```

### Web Interface Recording

1. Navigate to `http://localhost:5201/recording`
2. Click "Start New Recording"
3. Configure settings (ensure "Visible Browser" is enabled)
4. Click "Start Recording"
5. **Browser will open visibly** - perform your test actions
6. Return to web interface to stop recording

## 🌟 Best Practices

### Recording Quality
- **Interact slowly**: Give time for each action to be captured
- **Wait for page loads**: Ensure pages fully load before interacting
- **Use descriptive names**: Name recordings clearly
- **Test stable elements**: Use elements with stable IDs/names

### Environment Setup
- **Use visible mode for creation**: Record with visible browser
- **Use headless for execution**: Run tests in headless mode
- **Configure appropriate timeouts**: Adjust based on application speed
- **Test in target environment**: Record in similar environment to execution

### Troubleshooting Steps
1. **Start simple**: Test with basic pages first
2. **Check logs**: Review API server console output
3. **Verify connectivity**: Ensure target URLs are accessible
4. **Test fallback**: Try headless mode if visible fails

## 🔍 Environment Verification

Run this checklist to verify your environment:

```bash
# 1. Check Chrome installation
google-chrome --version

# 2. Check ChromeDriver
chromedriver --version

# 3. Check display availability
echo $DISPLAY

# 4. Test Xvfb (if applicable)
which Xvfb

# 5. Test API health
curl http://localhost:5146/health

# 6. Run the test script
./test_recording.sh
```

## 📞 Support

If you encounter issues:

1. **Check the logs**: API server console provides detailed error information
2. **Review this guide**: Follow the environment-specific setup instructions
3. **Test basic functionality**: Ensure CLI and API work before testing recording
4. **Environment compatibility**: Some environments may require specific configuration

The recording system is designed to work in most environments with proper setup. The automatic fallback ensures that recording will work even if visible mode fails.