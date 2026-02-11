# Makefile for ModularGuard CLI

.PHONY: help clean restore build test pack publish-native package-all install uninstall

# Variables
SOLUTION := ModularGuard.slnx
CLI_PROJECT := src/CLI/CLI.csproj
CONFIGURATION ?= Release
DOTNET := dotnet
OUTPUT_DIR := ./artifacts
PUBLISH_DIR := ./publish
VERSION ?= $(shell grep '<Version>' $(CLI_PROJECT) | sed 's/.*<Version>\(.*\)<\/Version>/\1/')

# Runtime identifiers
RUNTIMES := linux-x64 linux-arm64 osx-x64 osx-arm64 win-x64 win-arm64

# Default target
help: ## Show this help message
	@echo 'Usage: make [target]'
	@echo ''
	@echo 'Available targets:'
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) | sort | awk 'BEGIN {FS = ":.*?## "}; {printf "  \033[36m%-20s\033[0m %s\n", $$1, $$2}'

clean: ## Clean build artifacts
	@echo "Cleaning build artifacts..."
	@rm -rf $(OUTPUT_DIR) $(PUBLISH_DIR)
	@$(DOTNET) clean $(SOLUTION) --configuration $(CONFIGURATION)

restore: ## Restore NuGet dependencies
	@echo "Restoring dependencies..."
	@$(DOTNET) restore $(SOLUTION)

build: restore ## Build the solution
	@echo "Building solution ($(CONFIGURATION))..."
	@$(DOTNET) build $(SOLUTION) --configuration $(CONFIGURATION) --no-restore

test: build ## Run tests
	@echo "Running tests..."
	@$(DOTNET) test $(SOLUTION) --configuration $(CONFIGURATION) --no-build --verbosity normal

pack: build ## Create NuGet package
	@echo "Creating NuGet package..."
	@mkdir -p $(OUTPUT_DIR)
	@$(DOTNET) pack $(CLI_PROJECT) --configuration $(CONFIGURATION) --no-build --output $(OUTPUT_DIR)

install: pack ## Install as dotnet global tool
	@echo "Installing ModularGuard as global tool..."
	@$(DOTNET) tool uninstall -g ModularGuard 2>/dev/null || true
	@$(DOTNET) tool install -g ModularGuard --add-source $(OUTPUT_DIR) --version $(VERSION)

uninstall: ## Uninstall dotnet global tool
	@echo "Uninstalling ModularGuard global tool..."
	@$(DOTNET) tool uninstall -g ModularGuard

# Publish targets for specific runtimes
publish-linux-x64: ## Publish for Linux x64
	@$(MAKE) publish-runtime RUNTIME=linux-x64

publish-linux-arm64: ## Publish for Linux ARM64
	@$(MAKE) publish-runtime RUNTIME=linux-arm64

publish-osx-x64: ## Publish for macOS x64
	@$(MAKE) publish-runtime RUNTIME=osx-x64

publish-osx-arm64: ## Publish for macOS ARM64
	@$(MAKE) publish-runtime RUNTIME=osx-arm64

publish-win-x64: ## Publish for Windows x64
	@$(MAKE) publish-runtime RUNTIME=win-x64

publish-win-arm64: ## Publish for Windows ARM64
	@$(MAKE) publish-runtime RUNTIME=win-arm64

# Internal target for publishing to specific runtime
publish-runtime:
	@echo "Publishing for $(RUNTIME)..."
	@mkdir -p $(PUBLISH_DIR)/$(RUNTIME)
	@$(DOTNET) publish $(CLI_PROJECT) \
		--configuration $(CONFIGURATION) \
		--runtime $(RUNTIME) \
		--self-contained true \
		-p:PublishSingleFile=true \
		-p:PublishTrimmed=false \
		-p:IncludeNativeLibrariesForSelfExtract=true \
		-p:AssemblyName=modularguard \
		--output $(PUBLISH_DIR)/$(RUNTIME)

# Publish for all runtimes
publish-all: $(addprefix publish-,$(RUNTIMES)) ## Publish for all supported runtimes

# Package targets
package-linux-x64: publish-linux-x64 ## Create Linux x64 archive
	@$(MAKE) create-archive RUNTIME=linux-x64 ARCHIVE_EXT=tar.gz

package-linux-arm64: publish-linux-arm64 ## Create Linux ARM64 archive
	@$(MAKE) create-archive RUNTIME=linux-arm64 ARCHIVE_EXT=tar.gz

package-osx-x64: publish-osx-x64 ## Create macOS x64 archive
	@$(MAKE) create-archive RUNTIME=osx-x64 ARCHIVE_EXT=tar.gz

package-osx-arm64: publish-osx-arm64 ## Create macOS ARM64 archive
	@$(MAKE) create-archive RUNTIME=osx-arm64 ARCHIVE_EXT=tar.gz

package-win-x64: publish-win-x64 ## Create Windows x64 archive
	@$(MAKE) create-archive RUNTIME=win-x64 ARCHIVE_EXT=zip

package-win-arm64: publish-win-arm64 ## Create Windows ARM64 archive
	@$(MAKE) create-archive RUNTIME=win-arm64 ARCHIVE_EXT=zip

# Internal target for creating archives
create-archive:
	@echo "Creating archive for $(RUNTIME)..."
	@mkdir -p $(OUTPUT_DIR)
ifeq ($(ARCHIVE_EXT),tar.gz)
	@cd $(PUBLISH_DIR)/$(RUNTIME) && tar -czf ../../$(OUTPUT_DIR)/modularguard-$(RUNTIME).tar.gz *
else
	@cd $(PUBLISH_DIR)/$(RUNTIME) && zip -r ../../$(OUTPUT_DIR)/modularguard-$(RUNTIME).zip *
endif

# Package all runtimes
package-all: ## Create archives for all supported runtimes
	@$(MAKE) package-linux-x64
	@$(MAKE) package-linux-arm64
	@$(MAKE) package-osx-x64
	@$(MAKE) package-osx-arm64
	@$(MAKE) package-win-x64
	@$(MAKE) package-win-arm64
	@echo "All packages created in $(OUTPUT_DIR)/"

# Convenience targets
all: test pack ## Build, test, and create NuGet package

ci: clean test pack ## Run CI build (clean, test, pack)

release: clean test pack package-all ## Build release artifacts (NuGet + native binaries)
