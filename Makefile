# CloudFilm developer entry points.
# Every target is a thin wrapper over the underlying tool, so nothing here is
# required knowledge - `docker compose up` and `dotnet build` still work directly.

COMPOSE       := docker compose
COMPOSE_DEV   := docker compose -f docker-compose.yml -f docker-compose.dev.yml

.DEFAULT_GOAL := help

.PHONY: help
help: ## Show this help
	@grep -hE '^[a-zA-Z_-]+:.*?## ' $(MAKEFILE_LIST) | awk 'BEGIN {FS = ":.*?## "}; {printf "  \033[36m%-18s\033[0m %s\n", $$1, $$2}'

.PHONY: install
install: ## Install frontend dependencies (all you need for `make dev`)
	pnpm install

.PHONY: dev
dev: ## Run the portal alone, no configuration required (same as `pnpm dev`)
	pnpm dev

.PHONY: setup
setup: install ## Generate .env for the full Docker stack, and install dependencies
	@test -f .env || ./scripts/generate-secrets.sh

.PHONY: up
up: ## Start the full stack
	$(COMPOSE) up --build -d

.PHONY: up-dev
up-dev: ## Start the full stack with the development overlay (publishes the data tier)
	$(COMPOSE_DEV) up --build -d

.PHONY: down
down: ## Stop the stack, keeping volumes
	$(COMPOSE) down

.PHONY: clean
clean: ## Stop the stack and delete all data volumes
	$(COMPOSE) down --volumes

.PHONY: logs
logs: ## Follow logs for every service
	$(COMPOSE) logs --follow

.PHONY: build
build: build-backend build-frontend ## Build everything

.PHONY: build-backend
build-backend: ## Build the .NET solution
	dotnet build CloudFilm.sln --configuration Release

.PHONY: build-frontend
build-frontend: ## Build the portal
	pnpm build

.PHONY: test
test: ## Run the .NET test suites
	dotnet test CloudFilm.sln --configuration Release

.PHONY: lint
lint: ## Lint and type-check the TypeScript workspace
	pnpm lint
	pnpm typecheck

.PHONY: format
format: ## Format the TypeScript workspace
	pnpm format

.PHONY: verify
verify: lint build test ## Everything CI runs
