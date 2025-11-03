# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added ❤️ **Heartbeat System**
- **Heartbeat mechanism**: Client-server ping-pong system for health monitoring
- **Dead client detection**: Automatic detection and removal of unresponsive clients
- **Background monitoring**: Server thread that monitors client heartbeats every 5 seconds
- **Message-based protocol**: Enhanced communication with BaseMessage, TaskWrapper, ResultWrapper
- **Heartbeat timeouts**: 30-second timeout for client heartbeat detection
- **Client auto-ping**: Clients automatically send ping every 10 seconds
- **IDisposable ClientHandler**: Proper resource cleanup for disconnected clients

### Enhanced
- **Improved client management**: Better handling of client lifecycle
- **Protocol compatibility**: Backward compatibility with legacy message format
- **Error handling**: Enhanced error handling for network communications
- **Logging improvements**: Better heartbeat monitoring logs

### Technical
- Added MessageType enum (Task, Result, PingRequest, PingResponse)
- BaseMessage class as foundation for all messages
- PingMessage and PongMessage for heartbeat communication
- TaskWrapper and ResultWrapper for structured messaging
- ClientHandler implements IDisposable for proper cleanup
- HeartbeatMonitorAsync background task in server
- SendHeartbeatAsync background task in client
- LastHeartbeatTime tracking for each client
- IsAlive method for heartbeat validation

## [1.0.0] - 2025-11-03

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