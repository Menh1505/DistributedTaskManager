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

### Client Features
- ✅ TCP connection to server
- ✅ Task processing (Prime checking, Text hashing)
- ✅ Result reporting
- ✅ Auto-reconnection support

### Task Types
1. **CheckPrime**: Prime number checking
2. **HashText**: Generate SHA256 hash for text strings

## 🔧 Configuration

### Network Settings
- **Port**: 12345
- **Protocol**: TCP
- **Address**: localhost (configurable)

### Performance Settings
- **Task generation interval**: 2 seconds
- **Dispatcher check interval**: 100ms
- **Buffer size**: 4096 bytes

## 📈 Performance

The system is designed to:
- Handle hundreds of clients simultaneously
- Distribute tasks efficiently
- Scale according to CPU core count
- Maintain minimal memory footprint

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