# TrackGenius Logging Architecture and Taxonomy

## Scope
This document defines the initial logging architecture and taxonomy for TrackGenius.

It covers:
- log domains
- category naming
- log level policy
- event naming
- required structured properties

This is the baseline for implementing `Microsoft.Extensions.Logging` as abstraction and `Serilog` as implementation.

## 1. Logging Domains

### 1.1 Communication Domain
Purpose: Technical serial communication diagnostics and protocol runtime behavior.

Category prefix:
- `TrackGenius.Communication`

Typical sources:
- `SerialPortWrapper`
- `CommunicateService`
- message parser integrations

Examples:
- serial port open/close/write/read operations
- protocol parse outcomes
- communication recoverable failures

### 1.2 User Behavior Domain
Purpose: User operations and behavior tracking in UI workflows.

Category prefix:
- `TrackGenius.UserBehavior`

Typical sources:
- ViewModels (`MainFormParamViewModel`, race/command interaction points)

Examples:
- user opens a port
- user selects protocol
- user starts race-related actions

### 1.3 Application Domain
Purpose: Application lifecycle and global error diagnostics.

Category prefix:
- `TrackGenius.App`

Typical sources:
- `App.xaml.cs`
- startup/shutdown paths
- unhandled exception handlers

Examples:
- app startup and exit
- unhandled UI exceptions
- unexpected non-recoverable failures

## 2. Category Naming Convention

Use the following convention for all logs:

`TrackGenius.<Domain>.<Component>`

Examples:
- `TrackGenius.Communication.SerialPortWrapper`
- `TrackGenius.Communication.CommunicateService`
- `TrackGenius.UserBehavior.MainFormParamViewModel`
- `TrackGenius.App.Startup`

## 3. Log Level Policy

### Communication
- `Debug`: byte-level, operation timing, state checks (high-volume diagnostics)
- `Information`: open/close success, command send success, parser accepted message
- `Warning`: recoverable communication issues
- `Error`: non-recoverable communication failures

### User Behavior
- `Information`: primary user actions and important workflow transitions
- `Warning`: invalid user operation context (optional)
- `Error`: action failure with user-impacting result

### Application
- `Information`: startup/shutdown, configuration milestones
- `Warning`: recoverable global issues
- `Error`/`Critical`: unhandled exceptions, fatal states

## 4. Event Naming Convention

Use stable event names in PascalCase and keep them implementation-agnostic.

Examples:
- `PortOpenRequested`
- `PortOpened`
- `PortOpenFailed`
- `PortClosed`
- `CommandSendRequested`
- `CommandSent`
- `MessageParsed`
- `MessageParseFailed`
- `UserOpenPortClicked`
- `ApplicationUnhandledException`

## 5. Required Structured Properties

All logs should include relevant structured fields instead of only interpolated text.

Core fields:
- `Operation` (e.g., `OpenPort`, `SendBytes`, `StartService`)
- `PortName`
- `ProtocolName`
- `Component`
- `SessionId` (app run correlation)

Error fields:
- `ExceptionType`
- `ErrorCode` (if available)
- `IsRecoverable`

User behavior fields:
- `ActionName`
- `ViewModel`
- `Result`

## 6. Privacy and Data Handling Rules

- Do not log sensitive personal data.
- Do not log raw payloads by default in production.
- If payload logging is needed for troubleshooting, keep it on `Debug` and behind explicit enablement.

## 7. Baseline Sink Separation (for upcoming implementation)

Planned logical outputs:
- communication logs -> `communication-*.log`
- user behavior logs -> `user-behavior-*.log`
- app and error logs -> `application-*.log`

Retention and rolling policy will be finalized during bootstrap configuration implementation.

## 8. Acceptance Criteria for This Task

Task 1 is complete when:
- logging domains are clearly defined
- category naming is standardized
- level policy is specified per domain
- event naming and structured field guidelines are documented
- privacy constraints are declared
