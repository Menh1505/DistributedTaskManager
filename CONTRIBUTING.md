# Contributing to Distributed Task Manager

Chúng tôi rất hoan nghênh sự đóng góp từ cộng đồng! Tài liệu này sẽ hướng dẫn bạn cách đóng góp vào dự án.

## 🚀 Cách bắt đầu

### Prerequisites
- .NET 8.0 SDK
- Git
- VS Code hoặc Visual Studio
- Kiến thức cơ bản về C# và networking

### Setup Development Environment

1. **Fork repository**
   ```bash
   # Fork trên GitHub, sau đó clone
   git clone https://github.com/yourusername/DistributedTaskManager.git
   cd DistributedTaskManager
   ```

2. **Build và test**
   ```bash
   dotnet build
   dotnet test  # Khi có unit tests
   ```

3. **Chạy application**
   ```bash
   # Terminal 1
   cd Server && dotnet run
   
   # Terminal 2
   cd Client && dotnet run
   ```

## 📝 Development Guidelines

### Code Style
- Sử dụng **PascalCase** cho methods, properties, classes
- Sử dụng **camelCase** cho local variables
- Sử dụng **async/await** thay vì .Result hoặc .Wait()
- Luôn dispose resources (using statements)
- Exception handling đầy đủ

### Naming Conventions
```csharp
// ✅ Good
public class TaskDispatcher
{
    private readonly ConcurrentQueue<TaskMessage> _taskQueue;
    public async Task DispatchTaskAsync(TaskMessage task) { }
}

// ❌ Bad  
public class taskdispatcher
{
    private ConcurrentQueue<TaskMessage> taskqueue;
    public Task dispatchtask(TaskMessage task) { }
}
```

### Architecture Principles
- **Single Responsibility**: Mỗi class có một nhiệm vụ rõ ràng
- **Thread Safety**: Sử dụng Concurrent collections
- **Async Programming**: Non-blocking operations
- **Error Handling**: Graceful degradation
- **Resource Management**: Proper disposal

## 🎯 Types of Contributions

### 🐛 Bug Reports
Khi báo bug, bao gồm:
- OS và .NET version
- Steps to reproduce
- Expected vs actual behavior
- Logs/stack traces
- Code samples (nếu có)

### ✨ Feature Requests  
Khi đề xuất tính năng:
- Mô tả chi tiết use case
- Tại sao tính năng này cần thiết
- Đề xuất implementation approach
- Có thể impact gì đến existing code

### 🔧 Code Contributions

#### Branch Strategy
```bash
# Tạo feature branch từ main
git checkout main
git pull origin main
git checkout -b feature/task-priority-system

# Hoặc bugfix branch
git checkout -b bugfix/client-reconnection-issue
```

#### Commit Messages
Sử dụng conventional commits:
```bash
# Features
git commit -m "feat: add task priority system"
git commit -m "feat(server): implement load balancing"

# Bug fixes
git commit -m "fix: resolve client disconnection handling"
git commit -m "fix(client): prevent null reference exception"

# Documentation
git commit -m "docs: update README with deployment guide"

# Refactoring
git commit -m "refactor: extract task validation logic"
```

#### Pull Request Process
1. **Ensure code quality**
   - Code compiles without warnings
   - Follows style guidelines
   - Includes appropriate comments
   - No unused imports/variables

2. **Testing**
   - Test manually with server + multiple clients
   - Add unit tests for new features
   - Ensure existing functionality isn't broken

3. **Documentation**
   - Update README if needed
   - Add XML documentation for public APIs
   - Update CHANGELOG.md

4. **PR Description Template**
   ```markdown
   ## Changes
   - Brief description of changes
   
   ## Type of Change
   - [ ] Bug fix
   - [ ] New feature
   - [ ] Breaking change
   - [ ] Documentation update
   
   ## Testing
   - [ ] Tested with single client
   - [ ] Tested with multiple clients
   - [ ] Tested error scenarios
   
   ## Screenshots/Logs
   (If applicable)
   ```

## 🧪 Testing Guidelines

### Manual Testing Scenarios
1. **Single Client**: Server + 1 Client
2. **Multi Client**: Server + 3+ Clients
3. **Client Disconnection**: Ngắt kết nối đột ngột
4. **Server Restart**: Client handle server restart
5. **Network Issues**: Simulate network problems

### Performance Testing
- Monitor memory usage với nhiều clients
- Check CPU utilization
- Network bandwidth usage
- Task throughput metrics

## 📁 Project Structure Understanding

```
DistributedTaskManager/
├── .git/                    # Git repository
├── .gitignore              # Git ignore rules
├── README.md               # Main documentation
├── LICENSE                 # MIT license
├── CHANGELOG.md            # Version history
├── CONTRIBUTING.md         # This file
├── TaskManager.sln         # Solution file
├── Shared/                 # Shared library
│   ├── Message.cs          # Data models
│   └── Shared.csproj       # Project file
├── Server/                 # Server application
│   ├── Program.cs          # Main server logic
│   ├── ClientHandler.cs    # Per-client handling
│   └── Server.csproj       # Project file
└── Client/                 # Client application
    ├── Program.cs          # Client logic
    └── Client.csproj       # Project file
```

## 🎨 Feature Ideas

Nếu bạn muốn contribute nhưng chưa biết làm gì:

### 🥇 Easy (Good First Issues)
- Thêm logging framework (NLog/Serilog)
- Implement configuration file support
- Add more task types
- Improve error messages
- Add input validation

### 🥈 Medium
- Web dashboard cho monitoring
- Database integration
- Client health checks
- Task priority system
- Load balancing algorithms

### 🥉 Advanced
- Docker containerization
- REST API layer
- Microservices architecture
- Message queue integration (RabbitMQ)
- Kubernetes deployment

## 🤝 Community

### Communication
- **Issues**: Technical discussions, bug reports
- **Pull Requests**: Code reviews, implementation discussions
- **Discussions**: General questions, ideas

### Code Review Process
1. Automated checks (build, style)
2. Manual review by maintainers
3. Testing feedback
4. Approval và merge

## 📚 Resources

### Learning Materials
- [.NET Documentation](https://docs.microsoft.com/en-us/dotnet/)
- [C# Async Programming](https://docs.microsoft.com/en-us/dotnet/csharp/async)
- [TCP Programming in .NET](https://docs.microsoft.com/en-us/dotnet/api/system.net.sockets.tcpclient)
- [Concurrent Collections](https://docs.microsoft.com/en-us/dotnet/standard/collections/thread-safe/)

### Tools
- **IDE**: VS Code, Visual Studio, Rider
- **Profiling**: dotMemory, PerfView
- **Network**: Wireshark, netstat
- **Git**: GitKraken, SourceTree

## 🏆 Recognition

Contributors sẽ được ghi nhận trong:
- README.md contributors section
- CHANGELOG.md cho từng release
- GitHub contributors page

Cảm ơn bạn đã quan tâm đến dự án! 🙏