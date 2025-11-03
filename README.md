# Distributed Task Manager

A distributed task management system built with C# .NET 8, allowing servers to distribute tasks to multiple clients for parallel processing.

## 🏗️ Architecture

The project is divided into 3 main projects:

### 📁 Shared
- **Message.cs**: Defines message types and data models
- Contains enums and classes shared between Server and Client

### 🖥️ Server
- **Program.cs**: Main server with multi-threading
- **ClientHandler.cs**: Handles individual client connections
- Features:
  - Multi-threading to serve multiple clients simultaneously
  - Task queue with ConcurrentQueue
  - Automatic task dispatcher
  - Task producer (demo)

### 💻 Client
- **Program.cs**: Client connects and processes tasks
- Features:
  - TCP connection to server
  - Receive and process tasks (CheckPrime, HashText)
  - Send results back to server

## 🚀 How to Run

### System Requirements
- .NET 8.0 SDK
- OS: Windows, Linux, macOS

### Build Project
```bash
dotnet build
```

### Run Server
```bash
cd Server
dotnet run
```

### Run Client (different terminal)
```bash
cd Client
dotnet run
```

### Run Multiple Clients
Open additional terminals and run the above command to have multiple clients simultaneously.

## 📊 Features

### Server Features
- ✅ Multi-threading architecture
- ✅ Concurrent task queue
- ✅ Automatic task dispatching
- ✅ Multi-client support
- ✅ Real-time client status tracking
- ✅ Graceful client disconnect handling
- ✅ **Heartbeat monitoring system** ❤️
- ✅ Dead client detection and cleanup
- ✅ Message-based communication protocol
- ✅ **Task retry mechanism** 🔁
- ✅ **Dead-letter queue** for failed tasks
- ✅ Automatic task recovery and reprocessing

### Client Features
- ✅ TCP connection to server
- ✅ Task processing (Prime checking, Text hashing)
- ✅ Result reporting
- ✅ Auto-reconnection support
- ✅ **Automatic heartbeat ping** ❤️
- ✅ Message protocol compatibility

### Task Types
1. **CheckPrime**: Prime number checking
2. **HashText**: Generate SHA256 hash for text strings

### Message Types
1. **Task**: Task assignment from server to client
2. **Result**: Task result from client to server  
3. **PingRequest**: Heartbeat ping from client ❤️
4. **PingResponse**: Heartbeat pong from server ❤️

## 🔧 Configuration

### Network Settings
- **Port**: 12345
- **Protocol**: TCP
- **Address**: localhost (configurable)

### Performance Settings
- **Task generation interval**: 2 seconds
- **Dispatcher check interval**: 100ms
- **Buffer size**: 4096 bytes

### Heartbeat Settings ❤️
- **Client ping interval**: 10 seconds
- **Server heartbeat timeout**: 30 seconds
- **Heartbeat monitor check**: 5 seconds

### Task Retry Settings 🔁
- **Maximum retry attempts**: 3 times
- **Retry on client death**: Automatic
- **Dead-letter queue**: For failed tasks after max retries
- **Task failure simulation**: 10% random failure rate (testing)

## 📈 Performance

The system is designed to:
- Handle hundreds of clients simultaneously
- Distribute tasks efficiently
- Scale according to CPU core count
- Maintain minimal memory footprint
- **Detect and handle dead clients automatically** ❤️

## ❤️ Heartbeat Mechanism

### Problem Solved
Previously, the server only detected client disconnection when `ReadAsync` or `WriteAsync` failed. If a client process froze (still alive but unresponsive) or network connection dropped without proper TCP closure, the server would continue treating the client as "Idle" and assign tasks to a "dead" client.

### Solution Implementation

**Client Side:**
- Every 10 seconds, client sends a `PingRequest` message
- Continues sending heartbeats until connection is lost
- Each heartbeat includes client ID and timestamp

**Server Side:**
- When receiving `PingRequest`, server immediately responds with `PingResponse`
- Server updates `LastHeartbeatTime` for the client
- Background monitor thread runs every 5 seconds
- If `DateTime.Now - client.LastHeartbeatTime > 30 seconds`, client is considered dead
- Dead clients are automatically removed and connections closed

### Message Flow
```
Client                          Server
  |                               |
  |----> PingRequest ------------>|
  |                               | (Update LastHeartbeatTime)
  |<---- PingResponse <-----------|
  |                               |
  |                               | (Background Monitor)
  |                               | (Check: Now - LastHeartbeatTime > 30s?)
  |                               | (If yes: Remove client)
```

## 🔁 Task Retry & Dead-Letter Queue

### Problem Solved
When a client dies (detected by heartbeat timeout or IOException) while processing a task (`Status = Busy`), the task would be lost forever. This could result in important work being permanently lost.

### Solution Implementation

**Task Tracking:**
- Each `ClientHandler` stores its current task in `_currentTask`
- When task is assigned: `_currentTask = task`
- When result received: `_currentTask = null`

**Retry Logic:**
- When client dies unexpectedly, current task is automatically retried
- Task gets `RetryCount++` and `LastRetryAt` timestamp
- If `RetryCount < MAX_RETRY_COUNT` (3): task goes back to main queue
- If `RetryCount >= MAX_RETRY_COUNT`: task moves to dead-letter queue

**Dead-Letter Queue:**
- Persistent storage for tasks that failed all retry attempts
- Logged to `dead-letter-queue.log` file for audit trail
- Admin functions to reprocess or clear dead-letter tasks
- Monitoring and statistics reporting

### Task Lifecycle Flow
```
Task Created → Task Queue → Assigned to Client → Processing
                    ↑              ↓
                Retry Queue    Client Dies/Fails
                    ↑              ↓
            (if RetryCount < 3)  RetryCount++
                    ↑              ↓
                    ←──────────────┘
                                   ↓
                           (if RetryCount >= 3)
                                   ↓ 
                            Dead-Letter Queue
                                   ↓
                             Audit Log File
```

### Monitoring Features
- Real-time statistics every 5 minutes
- Dead-letter queue size monitoring
- Client task status tracking
- Retry attempt logging
- Admin functions for queue management

## 🧪 Testing

### Test with 1 Server + 3 Clients
```bash
# Terminal 1
cd Server && dotnet run

# Terminal 2
cd Client && dotnet run

# Terminal 3
cd Client && dotnet run

# Terminal 4
cd Client && dotnet run
```

Observe the logs to see the server distributing tasks to different clients.

## 🔧 Development

### Project Structure
```
DistributedTaskManager/
├── TaskManager.sln          # Solution file
├── Shared/                  # Shared library
│   ├── Message.cs
│   └── Shared.csproj
├── Server/                  # Server application
│   ├── Program.cs
│   ├── ClientHandler.cs
│   └── Server.csproj
└── Client/                  # Client application
    ├── Program.cs
    └── Client.csproj
```

### Dependencies
- **Target Framework**: .NET 8.0
- **External packages**: System.Text.Json (built-in)

### Code Style
- Async/await patterns
- Thread-safe collections
- Proper resource disposal
- Exception handling

## 🤝 Contributing

1. Fork the repository
2. Create feature branch (`git checkout -b feature/amazing-feature`)
3. Commit changes (`git commit -m 'Add amazing feature'`)
4. Push to branch (`git push origin feature/amazing-feature`)
5. Open Pull Request

## 📝 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 👥 Authors

- **Menhy Thien** - Initial work

## 🔮 Future Enhancements

- [ ] Web-based monitoring dashboard
- [ ] Database integration for task persistence
- [ ] Load balancing algorithms
- [ ] Task priority system
- [ ] Client health monitoring
- [ ] Docker containerization
- [ ] REST API interface
- [ ] Task result caching
- [ ] Configuration management
- [ ] Logging framework integration

## 📚 Architecture Diagram

```
┌─────────────┐     ┌─────────────────────────────────┐     ┌─────────────┐
│   Client 1  │────▶│            Server               │◀────│   Client 2  │
└─────────────┘     │                                 │     └─────────────┘
                    │  ┌─────────────────────────────┐ │
┌─────────────┐     │  │     Task Producer Thread   │ │     ┌─────────────┐
│   Client 3  │────▶│  └─────────────────────────────┘ │◀────│   Client N  │
└─────────────┘     │  ┌─────────────────────────────┐ │     └─────────────┘
                    │  │   Task Dispatcher Thread   │ │
                    │  └─────────────────────────────┘ │
                    │  ┌─────────────────────────────┐ │
                    │  │   Client Listener Thread   │ │
                    │  └─────────────────────────────┘ │
                    │  ┌─────────────────────────────┐ │
                    │  │     Concurrent Task Queue  │ │
                    │  └─────────────────────────────┘ │
                    └─────────────────────────────────┘
```