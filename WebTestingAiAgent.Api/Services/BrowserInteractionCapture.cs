using System.Collections.Concurrent;
using OpenQA.Selenium;
using WebTestingAiAgent.Core.Models;

namespace WebTestingAiAgent.Api.Services;

public class BrowserInteractionCapture
{
    private readonly ConcurrentQueue<RecordedStep> _capturedEvents = new();
    private bool _isCapturing = false;

    public bool IsCapturing => _isCapturing;

    public void StartCapturing()
    {
        _isCapturing = true;
    }

    public void StopCapturing()
    {
        _isCapturing = false;
    }

    public void ClearCapturedEvents()
    {
        while (_capturedEvents.TryDequeue(out _)) { }
    }

    public List<RecordedStep> GetCapturedEvents()
    {
        var events = new List<RecordedStep>();
        while (_capturedEvents.TryDequeue(out var evt))
        {
            events.Add(evt);
        }
        return events;
    }

    /// <summary>
    /// Get only ready events - non-input events or input events that have aged past the debounce period
    /// </summary>
    public List<RecordedStep> GetReadyCapturedEvents()
    {
        var events = new List<RecordedStep>();
        var tempEvents = new List<RecordedStep>();
        
        // Extract all events first
        while (_capturedEvents.TryDequeue(out var evt))
        {
            tempEvents.Add(evt);
        }
        
        var now = DateTime.UtcNow;
        foreach (var evt in tempEvents)
        {
            // For input events, only include if they've aged past debounce period (600ms to be safe)
            if (evt.Action.ToLower() == "input")
            {
                var age = now - evt.Timestamp;
                if (age.TotalMilliseconds >= 600)
                {
                    events.Add(evt);
                }
                else
                {
                    // Re-queue events that aren't ready yet
                    _capturedEvents.Enqueue(evt);
                }
            }
            else
            {
                // Non-input events are always ready
                events.Add(evt);
            }
        }
        
        return events;
    }

    public void InjectCapturingScript(IWebDriver driver)
    {
        var jsExecutor = (IJavaScriptExecutor)driver;
        
        var captureScript = @"
        window.webTestingCapture = window.webTestingCapture || {
            events: [],
            isCapturing: true,
            lastInputValue: {},  // Track last input value per element
            inputTimeout: {},    // Track input timeouts per element
            
            captureEvent: function(eventType, element, value, url) {
                if (!this.isCapturing) return;
                
                var selector = this.getSelector(element);
                var event = {
                    action: eventType,
                    elementSelector: selector,
                    value: value || '',
                    url: url || window.location.href,
                    timestamp: new Date().toISOString(),
                    metadata: {
                        tagName: element.tagName,
                        type: element.type || '',
                        id: element.id || '',
                        className: element.className || '',
                        name: element.name || '',
                        placeholder: element.placeholder || ''
                    }
                };
                
                // Special handling for input events to reduce noise
                if (eventType === 'input') {
                    var elementKey = selector + '_' + element.type;
                    
                    // Clear previous timeout for this element
                    if (this.inputTimeout[elementKey]) {
                        clearTimeout(this.inputTimeout[elementKey]);
                    }
                    
                    // Store the current value
                    this.lastInputValue[elementKey] = value;
                    
                    // Set a timeout to capture the final value after user stops typing
                    this.inputTimeout[elementKey] = setTimeout(function() {
                        var finalEvent = {
                            action: 'input',
                            elementSelector: selector,
                            value: window.webTestingCapture.lastInputValue[elementKey],
                            url: window.location.href,
                            timestamp: new Date().toISOString(),
                            metadata: event.metadata
                        };
                        window.webTestingCapture.events.push(finalEvent);
                        delete window.webTestingCapture.lastInputValue[elementKey];
                        delete window.webTestingCapture.inputTimeout[elementKey];
                    }, 500); // Wait 500ms after user stops typing
                } else {
                    // For non-input events, add immediately
                    this.events.push(event);
                }
            },
            
            getSelector: function(element) {
                if (element.id) return '#' + element.id;
                if (element.name) return '[name=""' + element.name + '""]';
                
                var selector = element.tagName.toLowerCase();
                if (element.className) {
                    var classes = element.className.trim().split(/\s+/);
                    if (classes.length > 0) {
                        selector += '.' + classes[0];
                    }
                }
                
                // Add position if multiple elements match
                var siblings = document.querySelectorAll(selector);
                if (siblings.length > 1) {
                    var index = Array.from(siblings).indexOf(element);
                    selector += ':nth-of-type(' + (index + 1) + ')';
                }
                
                return selector;
            },
            
            start: function() {
                this.isCapturing = true;
            },
            
            stop: function() {
                this.isCapturing = false;
            },
            
            getEvents: function() {
                var events = this.events.slice();
                this.events = [];
                return events;
            }
        };

        // Add event listeners with improved input handling
        document.addEventListener('click', function(e) {
            if (e.target) {
                window.webTestingCapture.captureEvent('click', e.target);
            }
        }, true);

        // Improved input handling - capture on input events but debounce
        document.addEventListener('input', function(e) {
            if (e.target && (e.target.type === 'text' || e.target.type === 'email' || 
                           e.target.type === 'password' || e.target.type === 'search' ||
                           e.target.type === 'tel' || e.target.type === 'url' ||
                           e.target.tagName === 'TEXTAREA')) {
                window.webTestingCapture.captureEvent('input', e.target, e.target.value);
            }
        }, true);

        // Also capture on blur to ensure we get final values
        document.addEventListener('blur', function(e) {
            if (e.target && (e.target.type === 'text' || e.target.type === 'email' || 
                           e.target.type === 'password' || e.target.type === 'search' ||
                           e.target.type === 'tel' || e.target.type === 'url' ||
                           e.target.tagName === 'TEXTAREA')) {
                // Force capture final input value immediately on blur
                var selector = window.webTestingCapture.getSelector(e.target);
                var elementKey = selector + '_' + e.target.type;
                
                if (window.webTestingCapture.inputTimeout[elementKey]) {
                    clearTimeout(window.webTestingCapture.inputTimeout[elementKey]);
                    delete window.webTestingCapture.inputTimeout[elementKey];
                }
                
                var finalEvent = {
                    action: 'input',
                    elementSelector: selector,
                    value: e.target.value,
                    url: window.location.href,
                    timestamp: new Date().toISOString(),
                    metadata: {
                        tagName: e.target.tagName,
                        type: e.target.type || '',
                        id: e.target.id || '',
                        className: e.target.className || '',
                        name: e.target.name || '',
                        placeholder: e.target.placeholder || ''
                    }
                };
                window.webTestingCapture.events.push(finalEvent);
                delete window.webTestingCapture.lastInputValue[elementKey];
            }
        }, true);

        document.addEventListener('change', function(e) {
            if (e.target && (e.target.type === 'select-one' || e.target.type === 'select-multiple' || 
                           e.target.type === 'checkbox' || e.target.type === 'radio')) {
                var value = e.target.type === 'checkbox' || e.target.type === 'radio' ? e.target.checked : e.target.value;
                window.webTestingCapture.captureEvent('select', e.target, value);
            }
        }, true);

        document.addEventListener('submit', function(e) {
            if (e.target && e.target.tagName === 'FORM') {
                window.webTestingCapture.captureEvent('submit', e.target);
            }
        }, true);

        // Navigation capture
        var originalPushState = history.pushState;
        var originalReplaceState = history.replaceState;
        
        history.pushState = function() {
            originalPushState.apply(history, arguments);
            setTimeout(function() {
                window.webTestingCapture.captureEvent('navigate', document.body, '', window.location.href);
            }, 100);
        };
        
        history.replaceState = function() {
            originalReplaceState.apply(history, arguments);
            setTimeout(function() {
                window.webTestingCapture.captureEvent('navigate', document.body, '', window.location.href);
            }, 100);
        };

        window.addEventListener('popstate', function() {
            setTimeout(function() {
                window.webTestingCapture.captureEvent('navigate', document.body, '', window.location.href);
            }, 100);
        });
        ";

        try
        {
            jsExecutor.ExecuteScript(captureScript);
        }
        catch (Exception ex)
        {
            // Log error but don't throw - capture script injection failure shouldn't break recording
            Console.WriteLine($"Failed to inject capture script: {ex.Message}");
        }
    }

    public void CollectEventsFromBrowser(IWebDriver driver)
    {
        if (!_isCapturing) return;

        try
        {
            var jsExecutor = (IJavaScriptExecutor)driver;
            var events = jsExecutor.ExecuteScript("return window.webTestingCapture ? window.webTestingCapture.getEvents() : [];");
            
            if (events is System.Collections.IEnumerable enumerable)
            {
                foreach (var eventObj in enumerable)
                {
                    if (eventObj is System.Collections.Generic.Dictionary<string, object> eventDict)
                    {
                        var step = ConvertJsEventToRecordedStep(eventDict);
                        if (step != null)
                        {
                            _capturedEvents.Enqueue(step);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // Log error but don't throw - event collection failure shouldn't break recording
            Console.WriteLine($"Failed to collect events from browser: {ex.Message}");
        }
    }

    private RecordedStep? ConvertJsEventToRecordedStep(Dictionary<string, object> eventDict)
    {
        try
        {
            var step = new RecordedStep
            {
                Id = Guid.NewGuid().ToString(),
                Action = eventDict.TryGetValue("action", out var action) ? action.ToString() ?? "" : "",
                ElementSelector = eventDict.TryGetValue("elementSelector", out var selector) ? selector.ToString() : null,
                Value = eventDict.TryGetValue("value", out var value) ? value.ToString() : null,
                Url = eventDict.TryGetValue("url", out var url) ? url.ToString() : null,
                Timestamp = DateTime.UtcNow,
                Metadata = new Dictionary<string, object>()
            };

            if (eventDict.TryGetValue("metadata", out var metadata) && metadata is Dictionary<string, object> metadataDict)
            {
                step.Metadata = metadataDict;
            }

            return step;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to convert JS event to RecordedStep: {ex.Message}");
            return null;
        }
    }

    public void SetCapturingState(IWebDriver driver, bool isCapturing)
    {
        _isCapturing = isCapturing;
        
        try
        {
            var jsExecutor = (IJavaScriptExecutor)driver;
            var command = isCapturing ? "start" : "stop";
            jsExecutor.ExecuteScript($"if (window.webTestingCapture) window.webTestingCapture.{command}();");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to set capturing state in browser: {ex.Message}");
        }
    }
}