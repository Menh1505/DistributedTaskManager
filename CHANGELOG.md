# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added 🔁 **Task Retry & Dead-Letter Queue System**
- **Task retry mechanism**: Automatic retry of failed tasks up to 3 attempts
- **Dead-letter queue**: Persistent storage for tasks that exceed max retry attempts
- **Current task tracking**: ClientHandler tracks assigned tasks for failure recovery
- **Task failure recovery**: Automatic requeuing when client dies during task processing
- **Audit logging**: Dead-letter tasks logged to `dead-letter-queue.log` file
- **Admin functions**: Reprocess or clear dead-letter queue programmatically
- **Failure statistics**: Real-time monitoring of task success/failure rates
- **Retry metadata**: Tasks include RetryCount, CreatedAt, LastRetryAt timestamps
- **Dead-letter monitoring**: Background thread monitoring dead-letter queue size
- **Task lifecycle tracking**: Complete task journey from creation to completion/failure

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
- **Task Retry System**: 
  - TaskMessage enhanced with RetryCount, CreatedAt, LastRetryAt fields
  - ConcurrentQueue<TaskMessage> _deadLetterQueue for failed tasks
  - ClientHandler._currentTask for tracking assigned tasks
  - LogDeadLetterTaskAsync for audit trail logging
  - DeadLetterMonitorAsync background monitoring thread
  - ReprocessDeadLetterTasks and ClearDeadLetterQueue admin functions
  - GetCurrentTaskInfo for runtime task monitoring
  - MAX_RETRY_COUNT configuration (default: 3)
  - Task failure simulation for testing (10% random failure)

- **Heartbeat System**:
  - MessageType enum (Task, Result, PingRequest, PingResponse)
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