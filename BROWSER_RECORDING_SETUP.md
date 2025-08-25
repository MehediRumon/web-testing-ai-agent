# Browser Recording Setup Guide

This guide helps you set up visible browser recording for the Web Testing AI Agent to capture user interactions during test case creation.

## 🎯 Overview

The Web Testing AI Agent now supports **visible browser recording** where:
- Browser opens visibly for user interaction capture
- All user interactions (clicks, typing, form submissions) are automatically captured
- Test cases can be saved and replayed later
- Graceful fallback to headless mode if visible mode fails

## ✅ Quick Setup (Desktop Environment)

If you're running on a desktop environment with GUI support:

```bash
# 1. Ensure Chrome is installed
google-chrome --version

# 2. Start the API
cd WebTestingAiAgent.Api
dotnet run

# 3. Create a recording session (browser will open visibly)
curl -X POST http://localhost:5146/api/recording/start \
  -H "Content-Type: application/json" \
  -d '{
    "name": "My Test Recording",
    "baseUrl": "https://example.com",
    "settings": {
      "headless": false,
      "forceVisible": true,
      "timeoutMs": 30000
    }
  }'
```

## 🖥️ Server/Headless Environment Setup

**Important**: As of the latest update, the system **prioritizes local displays** over virtual displays. Virtual displays are only used when explicitly requested.

For server environments without GUI (VPS, CI/CD, Docker), you have these options:

### Option 1: Using Real Display (Recommended)

```bash
# Use X11 forwarding (SSH)
ssh -X user@your-server

# Verify X11 forwarding works
echo $DISPLAY
# Should show something like localhost:10.0

# Now run recording - will use real forwarded display
```

### Option 2: Using Virtual Display (Explicit)

**Note**: You must now explicitly enable virtual display usage.

```bash
# Install Xvfb
sudo apt-get update
sudo apt-get install xvfb

# Create recording with virtual display enabled
curl -X POST http://localhost:5146/api/recording/start \
  -H "Content-Type: application/json" \
  -d '{
    "name": "My Test Recording",
    "baseUrl": "https://example.com",
    "settings": {
      "headless": false,
      "forceVisible": true,
      "useVirtualDisplay": true,
      "timeoutMs": 30000
    }
  }'
```

### Option 3: Using VNC/Remote Desktop

1. Install VNC server on your server
2. Connect via VNC client
3. Run the application within the VNC session

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
  "headless": false,              // Default mode
  "captureScreenshots": true,     // Enable screenshots
  "timeoutMs": 30000             // General timeout
}
```

## 🔧 Troubleshooting

### Browser Fails to Start

**Symptom**: "ChromeDriver initialization timed out"

**Solutions**:
1. **Install Xvfb**: `sudo apt-get install xvfb`
2. **Check Chrome installation**: `google-chrome --version`
3. **Verify DISPLAY**: `echo $DISPLAY`
4. **Try headless fallback**: Set `"headless": true` in settings

### No Display Available

**Symptom**: "No DISPLAY environment variable found"

**New Behavior**: The system now prioritizes real displays over virtual displays.

**Solutions** (in order of preference):
1. **Desktop Environment**: Run on a machine with GUI (recommended)
2. **X11 Forwarding**: Use `ssh -X` to connect to server
3. **VNC/RDP**: Set up remote desktop access  
4. **Virtual Display**: Set `"useVirtualDisplay": true` in recording settings
5. **Headless Mode**: Use `"headless": true` for automated execution

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