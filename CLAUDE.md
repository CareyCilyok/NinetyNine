# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

NinetyNine is a desktop scorekeeper application for the pool game "Ninety-Nine" (or '99'), built with .NET 8 and Avalonia UI. The application uses a clean architecture pattern with distinct layers for presentation, business logic, data access, and web services.

## Architecture

The solution follows a multi-project architecture:

- **Model**: Core domain entities (Game, Player, Frame, Venue, TableSize)
- **Presentation**: Avalonia UI layer with MVVM pattern using ReactiveUI and Aura.UI components
- **Repository**: Data access layer with Entity Framework Core contexts (LocalContext, EnterpriseContext)
- **Services**: ASP.NET Core Web API with controllers for Games, Players, and Venues
- **App**: Main Avalonia application entry point

## Technology Stack

- **.NET 8** - Target framework
- **Avalonia UI 0.10.13** - Cross-platform UI framework with desktop support
- **ReactiveUI** - MVVM framework for reactive programming
- **Aura.UI 0.1.4.2** - UI component library for enhanced controls and FluentTheme
- **Entity Framework Core** - ORM for data access (Repository: 3.0.1, Services: 6.0.3)
- **ASP.NET Core** - Web API framework with versioning support
- **AutoFixture 4.17.0** - Test data generation
- **Swashbuckle 6.2.3** - API documentation

## Development Commands

### Building the Solution
```bash
dotnet build NinetyNine.sln
```

### Running the Desktop Application
```bash
dotnet run --project App/Application.csproj
```

### Running the Web API
```bash
dotnet run --project Services/Services.csproj
```
The API launches at https://localhost:7070 with Swagger documentation.

### Running Tests
No test projects currently exist in the solution. AutoFixture is available in the Services project for test data generation when tests are added.

## Project Structure Guidelines

### Model Layer
- Contains domain entities with proper XML documentation
- Uses System.Text.Json for serialization
- Follows MIT license header convention

### Presentation Layer
- MVVM pattern with ViewModels inheriting from ViewModelBase
- Uses ReactiveUI for property notifications
- Avalonia XAML for UI definitions
- Aura.UI components for enhanced styling
- SVG icons located in Assets/svg/

### Repository Layer
- Entity Framework contexts extend NinetyNineContext base class
- Supports multiple database contexts (Local, Enterprise)
- LocalContext uses in-memory database, EnterpriseContext uses SQL Server
- MongoDB prototype available in mongo-prototype/
- Note: Version inconsistency (Repository uses EF Core 3.0.1, Services uses 6.0.3)

### Services Layer
- ASP.NET Core Web API with versioning (v0.0) support
- Controllers follow RESTful conventions with proper HTTP verbs
- Uses in-memory database for development
- Swagger documentation enabled at https://localhost:7070
- Service pattern with interfaces: IGameService, IStatisticsService, IVenueService
- Event-driven architecture for game state changes

## Key Patterns

### ViewModels
- Inherit from ViewModelBase (which extends ReactiveUI's ReactiveObject)
- Implement ICardControlTemplate for card-based UI components
- Use ReactiveUI's RaiseAndSetIfChanged for property binding
- Support reactive commands and async operations

### Data Access
- Context classes inherit from NinetyNineContext base class
- Support for both local (in-memory) and enterprise (SQL Server) database configurations
- Entity Framework Code First approach with integrated security
- Multiple context pattern for different deployment scenarios

### Service Architecture
- Comprehensive service interfaces with full async support
- Event-driven communication between services (CurrentGameChanged, FrameCompleted, GameCompleted)
- Built-in validation for game states and scoring logic
- Statistics engine with leaderboards and performance tracking

### Game Logic
The application implements the official Ninety-Nine pool game rules:
- 9 frames per game, maximum 11 points per frame (99 total)
- Break bonus and ball count scoring system
- Support for various table sizes and venue tracking

## File Naming Conventions
- C# files use PascalCase
- XAML files match their code-behind counterparts
- ViewModels end with "ViewModel" suffix
- Pages end with "Page" suffix
- Controls end with "Control" suffix

## Development Notes

### Known Issues
- **Test Projects**: Test projects added but need model property alignment to run successfully
- **Avalonia Version**: Using older Avalonia 0.10.13, should upgrade to latest stable for better .NET 8 support

### Build Configuration
- **Trimming**: Uses `TrimMode=copyused` for Avalonia applications
- **Debug Tools**: Avalonia.Diagnostics only included in Debug builds
- **Platform Support**: Configured for Windows, Linux (X11), and macOS