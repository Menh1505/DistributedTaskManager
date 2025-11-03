# Contributing to Distributed Task Manager

We welcome contributions from the community! This document will guide you on how to contribute to the project.

## 🚀 Getting Started

### Prerequisites
- .NET 8.0 SDK
- Git
- VS Code or Visual Studio
- Basic knowledge of C# and networking

### Setup Development Environment

1. **Fork repository**
   ```bash
   # Fork on GitHub, then clone
   git clone https://github.com/yourusername/DistributedTaskManager.git
   cd DistributedTaskManager
   ```

2. **Build and test**
   ```bash
   dotnet build
   dotnet test  # When unit tests are available
   ```

3. **Run application**
   ```bash
   # Terminal 1
   cd Server && dotnet run
   
   # Terminal 2
   cd Client && dotnet run
   ```

## 📝 Development Guidelines

### Code Style
- Use **PascalCase** for methods, properties, classes
- Use **camelCase** for local variables
- Use **async/await** instead of .Result or .Wait()
- Always dispose resources (using statements)
- Comprehensive exception handling

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
- **Single Responsibility**: Each class has a clear purpose
- **Thread Safety**: Use concurrent collections
- **Async Programming**: Non-blocking operations
- **Error Handling**: Graceful degradation
- **Resource Management**: Proper disposal

## 🎯 Types of Contributions

### 🐛 Bug Reports
When reporting bugs, include:
- OS and .NET version
- Steps to reproduce
- Expected vs actual behavior
- Logs/stack traces
- Code samples (if applicable)

### ✨ Feature Requests  
When proposing features:
- Detailed use case description
- Why this feature is needed
- Suggested implementation approach
- Potential impact on existing code

### 🔧 Code Contributions

#### Branch Strategy
```bash
# Create feature branch from main
git checkout main
git pull origin main
git checkout -b feature/task-priority-system

# Or bugfix branch
git checkout -b bugfix/client-reconnection-issue
```

#### Commit Messages
Use conventional commits:
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
3. **Client Disconnection**: Abrupt disconnection
4. **Server Restart**: Client handle server restart
5. **Network Issues**: Simulate network problems

### Performance Testing
- Monitor memory usage with multiple clients
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

If you want to contribute but don't know what to do:

### 🥇 Easy (Good First Issues)
- Add logging framework (NLog/Serilog)
- Implement configuration file support
- Add more task types
- Improve error messages
- Add input validation

### 🥈 Medium
- Web dashboard for monitoring
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
4. Approval and merge

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

Contributors will be acknowledged in:
- README.md contributors section
- CHANGELOG.md for each release
- GitHub contributors page

Thank you for your interest in the project! 🙏