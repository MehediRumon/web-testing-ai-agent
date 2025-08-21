#!/bin/bash

# Web Testing AI Agent - Test Runner Script
# This script demonstrates the automated testing infrastructure

set -e

echo "🧪 Web Testing AI Agent - Running Automated Tests"
echo "================================================="

# Build the solution first
echo "📦 Building solution..."
dotnet build --verbosity quiet

# Run all tests with detailed output
echo ""
echo "🧪 Running all tests..."
dotnet test --verbosity normal --logger "trx;LogFileName=test-results.trx" --results-directory ./TestResults

# Display test summary
echo ""
echo "📊 Test Summary:"
echo "- Core Model Tests: ✅ Unit tests for data models and configuration"
echo "- API Integration Tests: ✅ Full HTTP endpoint testing"
echo "- CLI Integration Tests: ✅ Command-line interface testing"
echo "- Validation Service Tests: ✅ Input validation and error handling"

echo ""
echo "🎉 All tests completed successfully!"
echo "📄 Test results saved to: ./TestResults/test-results.trx"

# Optional: Show test count
echo ""
echo "📈 Quick Stats:"
dotnet test --verbosity quiet | grep -E "(Passed:|Failed:|Total:)" | tail -3