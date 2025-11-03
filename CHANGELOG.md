# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Initial implementation of Distributed Task Manager
- Multi-threading server architecture
- Concurrent task queue system
- Client-server TCP communication
- Task types: CheckPrime and HashText
- Real-time client status tracking
- Graceful client disconnect handling

### Features
- Multi-client support with individual threading
- Automatic task dispatching
- Task producer for demo purposes
- Thread-safe data structures
- Async/await patterns throughout

### Technical
- .NET 8.0 target framework
- Solution structure with 3 projects: Server, Client, Shared
- JSON serialization for message passing
- TCP networking with NetworkStream
- Proper resource disposal and exception handling

## [1.0.0] - 2025-11-03

### Added
- Initial release of Distributed Task Manager
- Core server-client architecture
- Basic task distribution system
- Documentation and setup guides