# Feature Management Platform Makefile

API_DIR = FMP.API
TEST_DIR = FMP.API.Tests

# Default target
.PHONY: help
help:
	@echo "Feature Management Platform"
	@echo ""
	@echo "Usage:"
	@echo "  make help           - Show this help message"
	@echo "  make runapi-debug   - Run the API in debug mode with in-memory store"
	@echo "  make runtests       - Run all tests"
	@echo "  make testflags      - Run just the FeatureFlag tests"
	@echo "  make testrules      - Run just the TargetingRule tests"
	@echo "  make testenv        - Run just the Environment tests"
	@echo "  make testanalytics  - Run just the Analytics tests"
	@echo "  make test name=TestName - Run a specific test by name"
	@echo "  make build          - Build the solution"
	@echo "  make clean          - Clean the solution"
	@echo ""
	@echo "Future commands (not yet implemented):"
	@echo "  make run-cosmos     - Run with Cosmos DB emulator"
	@echo "  make run-ui         - Run the UI (when available)"
	@echo "  make run-all        - Run both API and UI (when available)"

# Run the API in debug mode (renamed from 'run' to be more explicit)
.PHONY: runapi-debug
runapi-debug:
	@echo "Starting API in debug mode with in-memory store..."
	@cd $(API_DIR) && dotnet run --debug

# Add an alias for backward compatibility
.PHONY: run
run: runapi-debug

# Run all tests
.PHONY: runtests
runtests:
	@echo "Running all tests..."
	@cd $(TEST_DIR) && dotnet test

# Run specific test categories
.PHONY: testflags
testflags:
	@echo "Running FeatureFlag tests..."
	@cd $(TEST_DIR) && dotnet test --filter "FullyQualifiedName~FMP.API.Tests.FeatureFlags"

.PHONY: testrules
testrules:
	@echo "Running TargetingRule tests..."
	@cd $(TEST_DIR) && dotnet test --filter "FullyQualifiedName~FMP.API.Tests.TargetingRules"

.PHONY: testenv
testenv:
	@echo "Running Environment tests..."
	@cd $(TEST_DIR) && dotnet test --filter "FullyQualifiedName~FMP.API.Tests.Environments"

.PHONY: testanalytics
testanalytics:
	@echo "Running Analytics tests..."
	@cd $(TEST_DIR) && dotnet test --filter "FullyQualifiedName~FMP.API.Tests.Analytics"

# Add a target to run a specific test by name
.PHONY: test
test:
	@if [ -z "$(name)" ]; then \
		echo "Please provide a test name, e.g. make test name=DeleteTargetingRule_WithValidIds_ReturnsNoContent"; \
		exit 1; \
	fi
	@echo "Running test: $(name)"
	@cd $(TEST_DIR) && dotnet test --filter "FullyQualifiedName~$(name)"

# Build the solution
.PHONY: build
build:
	@echo "Building solution..."
	@dotnet build

# Clean the solution
.PHONY: clean
clean:
	@echo "Cleaning solution..."
	@dotnet clean
	@find . -name "bin" -type d -exec rm -rf {} +
	@find . -name "obj" -type d -exec rm -rf {} +

# Placeholder commands for future functionality
.PHONY: run-cosmos
run-cosmos:
	@echo "This command will run the API with Cosmos DB when implemented"
	@echo "For now, start the Cosmos DB emulator manually and run: cd $(API_DIR) && dotnet run"

.PHONY: run-ui
run-ui:
	@echo "This command will run the UI when implemented"
	@echo "UI not yet available"

.PHONY: run-all
run-all:
	@echo "This command will run both API and UI when implemented"
	@echo "For now, only the API is available. Run 'make run' to start it."

# Watch for changes and auto-restart API (development helper)
.PHONY: watch
watch:
	@echo "Watching for changes and auto-restarting API..."
	@cd $(API_DIR) && dotnet watch run --debug

# Generate documentation
.PHONY: docs
docs:
	@echo "Generating documentation..."
	@echo "Documentation generation not yet implemented"
