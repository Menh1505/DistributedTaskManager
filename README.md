# Distributed Task Manager

Một hệ thống quản lý tác vụ phân tán được xây dựng bằng C# .NET 8, cho phép server phân phối các tác vụ tới nhiều client để xử lý song song.

## 🏗️ Kiến trúc

Dự án được chia thành 3 project chính:

### 📁 Shared
- **Message.cs**: Định nghĩa các message types và data models
- Chứa các enum và class dùng chung giữa Server và Client

### 🖥️ Server
- **Program.cs**: Server chính với multi-threading
- **ClientHandler.cs**: Xử lý từng client connection
- Tính năng:
  - Multi-threading để phục vụ nhiều client đồng thời
  - Task queue với ConcurrentQueue
  - Task dispatcher tự động
  - Task producer (demo)

### 💻 Client
- **Program.cs**: Client kết nối và xử lý tasks
- Tính năng:
  - Kết nối TCP tới server
  - Nhận và xử lý tasks (CheckPrime, HashText)
  - Gửi kết quả về server

## 🚀 Cách chạy

### Yêu cầu hệ thống
- .NET 8.0 SDK
- OS: Windows, Linux, macOS

### Build project
```bash
dotnet build
```

### Chạy Server
```bash
cd Server
dotnet run
```

### Chạy Client (terminal khác)
```bash
cd Client
dotnet run
```

### Chạy nhiều Client
Mở thêm terminal và chạy lệnh trên để có nhiều client đồng thời.

## 📊 Tính năng

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
1. **CheckPrime**: Kiểm tra số nguyên tố
2. **HashText**: Tạo hash SHA256 cho chuỗi text

## 🔧 Cấu hình

### Network Settings
- **Port**: 12345
- **Protocol**: TCP
- **Address**: localhost (có thể thay đổi)

### Performance Settings
- **Task generation interval**: 2 seconds
- **Dispatcher check interval**: 100ms
- **Buffer size**: 4096 bytes

## 📈 Performance

Hệ thống được thiết kế để:
- Xử lý hàng trăm client đồng thời
- Phân phối task hiệu quả
- Scale theo số lượng CPU cores
- Minimal memory footprint

## 🧪 Testing

### Test với 1 Server + 3 Clients
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

Quan sát log để thấy server phân phối task cho các client khác nhau.

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