# Task Manager Web Client

## Mô tả
Giao diện web ASP.NET Core cho phép quản lý tasks thủ công thay vì tự động như client console. Người dùng có thể:

- Kết nối/ngắt kết nối với server bằng cách nhấn nút
- Yêu cầu nhận task từ server bằng cách nhấn nút  
- Hoàn thành task tự động hoặc với kết quả tùy chỉnh
- Theo dõi logs kết nối real-time

## Cách chạy

### 1. Chạy Server trước
```bash
# Terminal 1 - Start Server
cd Server
dotnet run
```

### 2. Chạy Web Client
```bash
# Terminal 2 - Start Web Client
./start-webclient.sh

# Hoặc chạy trực tiếp:
cd ClientWebApp
dotnet run --urls "http://localhost:5000"
```

### 3. Sử dụng giao diện web
1. Mở trình duyệt và truy cập: `http://localhost:5000`
2. Nhấn **"Connect to Server"** để kết nối
3. Nhấn **"Request New Task"** để nhận task từ server
4. Nhấn **"Auto Complete Task"** để hoàn thành task tự động
5. Hoặc nhập kết quả tùy chỉnh và nhấn **"Complete (Success/Fail)"**

## Tính năng chính

### Connection Control
- **Connect**: Kết nối đến server (127.0.0.1:12345)
- **Disconnect**: Ngắt kết nối khỏi server
- Hiển thị trạng thái kết nối real-time

### Task Control  
- **Request New Task**: Yêu cầu task mới từ server
- **Auto Complete**: Thực hiện task tự động (CheckPrime/HashText)
- **Custom Complete**: Nhập kết quả tùy chỉnh và đánh dấu Success/Fail

### Monitoring
- Hiển thị thông tin task hiện tại (ID, Type, Data, Created Time)
- Connection logs với timestamp
- Auto-refresh functionality
- Clear logs option

## API Endpoints

### Web Controllers
- `GET /Task` - Trang chính
- `POST /Task/Connect` - Kết nối server  
- `POST /Task/Disconnect` - Ngắt kết nối
- `POST /Task/RequestTask` - Yêu cầu task
- `POST /Task/CompleteTask` - Hoàn thành task
- `POST /Task/ClearLogs` - Xóa logs
- `GET /Task/GetStatus` - API status (JSON)

## Kiến trúc

### Services
- **ITaskClientService**: Interface cho task client operations
- **TaskClientService**: Implementation với TCP connection management

### Controllers  
- **TaskController**: Handles web requests và task operations

### Models
- **TaskViewModel**: View model cho task management UI

### Views
- **Task/Index.cshtml**: Main UI với Bootstrap styling

## Dependencies
- ASP.NET Core 8.0
- Bootstrap 5 (UI framework)
- Bootstrap Icons (Icon set)
- Shared library (Task/Message definitions)

## Khác biệt so với Console Client
1. **Manual Control**: User chủ động điều khiển thay vì tự động
2. **Web Interface**: Giao diện web thân thiện thay vì console
3. **Real-time Monitoring**: Logs và status updates trực quan
4. **Custom Results**: Cho phép nhập kết quả tùy chỉnh
5. **Connection Management**: Điều khiển kết nối manual