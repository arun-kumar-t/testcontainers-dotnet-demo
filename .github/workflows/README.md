# GitHub Actions Workflows

This directory contains CI/CD workflows for automated testing.

> **Quick Start**: After pushing to GitHub, workflows run automatically. See setup instructions below.

## Workflows

### 1. `ci.yml` - Main CI Pipeline
Runs both unit and integration tests on every push and pull request.

**Features:**
- Runs unit tests (fast, no Docker)
- Runs integration tests (with Testcontainers, requires Docker)
- Generates code coverage reports
- Uploads test results and coverage as artifacts
- Publishes test results as PR annotations

**Triggers:**
- Push to `main` or `develop` branches
- Pull requests to `main` or `develop` branches

### 2. `unit-tests.yml` - Unit Tests Only
Fast unit test execution (no Docker required).

**Use Cases:**
- Quick feedback on code changes
- Pre-commit validation
- Manual trigger for unit tests only

**Triggers:**
- Push to `main` or `develop` branches
- Pull requests to `main` or `develop` branches
- Manual trigger (`workflow_dispatch`)

### 3. `integration-tests.yml` - Integration Tests Only
Integration tests using Testcontainers (requires Docker).

**Features:**
- Verifies Docker is available
- Runs integration tests with Testcontainers
- Generates coverage reports
- Uploads results as artifacts

**Triggers:**
- Push to `main` or `develop` branches
- Pull requests to `main` or `develop` branches
- Manual trigger (`workflow_dispatch`)

## Docker-in-Docker (DinD)

Integration tests require Docker because Testcontainers needs to:
- Start MQTT broker containers
- Start fake device containers
- Manage container lifecycle

The workflows use GitHub Actions' Docker-in-Docker service to provide Docker support.

## Test Results

Test results are published in multiple ways:

1. **GitHub Actions UI**: View test results in the Actions tab
2. **PR Annotations**: Test results appear as comments on pull requests
3. **Artifacts**: Download test result files and coverage reports
4. **Badges**: CI status badge in README (update with your repo URL)

## Coverage Reports

Code coverage reports are generated using:
- **Coverlet**: Collects coverage data during test execution
- **ReportGenerator**: Generates HTML and text summary reports

Coverage reports are uploaded as artifacts and can be downloaded from the Actions UI.

## Manual Workflow Execution

You can manually trigger workflows from the GitHub Actions UI:
1. Go to Actions tab
2. Select the workflow
3. Click "Run workflow"
4. Choose branch and click "Run workflow"

## Troubleshooting

### Docker Issues

If integration tests fail with Docker errors:
- Ensure Docker service is properly configured in workflow
- Check Docker-in-Docker service health
- Verify Testcontainers can access Docker socket

### Test Timeouts

If tests timeout:
- Integration tests may take 30-60 seconds (container startup)
- Increase timeout values if needed
- Check container startup logs

### Coverage Reports

If coverage reports are missing:
- Ensure `coverlet.collector` package is referenced
- Check that tests actually ran
- Verify ReportGenerator tool installation

## Customization

To customize workflows for your needs:

1. **Update .NET version**: Change `dotnet-version` in setup step
2. **Add more test projects**: Add additional `dotnet test` commands
3. **Change triggers**: Modify `on:` section
4. **Add notifications**: Add Slack/email notifications on failure
5. **Add deployment**: Add deployment steps after successful tests

## Quick Setup

### Step 1: Push to GitHub

After creating your GitHub repository, push the code:

```bash
git add .
git commit -m "Add GitHub Actions workflows"
git push origin main
```

### Step 2: Verify Workflows

1. Go to your repository on GitHub
2. Click on the **Actions** tab
3. You should see the workflows listed
4. The workflows will run automatically on push/PR

## Best Practices

- ✅ Run unit tests first (faster feedback)
- ✅ Run integration tests separately (slower, requires Docker)
- ✅ Upload artifacts for debugging
- ✅ Publish test results for visibility
- ✅ Generate coverage reports
- ✅ Use matrix builds for multiple .NET versions (if needed)

