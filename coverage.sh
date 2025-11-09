#!/bin/bash

# Run unit tests with coverage
echo "Running unit tests with coverage..."
dotnet test tests/UnitTests/UnitTests.csproj --collect:"XPlat Code Coverage" --results-directory:"./TestResults"

# Generate HTML report
echo "Generating coverage report..."
reportgenerator -reports:"TestResults/**/coverage.cobertura.xml" -targetdir:"CoverageReport" -reporttypes:"Html;TextSummary"

# Display summary
echo ""
echo "=== Coverage Summary ==="
cat CoverageReport/Summary.txt

echo ""
echo "HTML report generated at: CoverageReport/index.html"
echo "Open it with: open CoverageReport/index.html"

