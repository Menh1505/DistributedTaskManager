using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Shared;

namespace Server
{
    public class ClientHandler
    {
        public string Id { get; }
        public ClientStatus Status { get; private set; }
        
        private TcpClient _client;
        private NetworkStream _stream;
        private ConcurrentDictionary<string, ClientHandler> _clientHandlers;
        private Action<string> _log; // Hàm helper để log
        
        public ClientHandler(TcpClient client, ConcurrentDictionary<string, ClientHandler> clientHandlers, Action<string> log)
        {
            Id = Guid.NewGuid().ToString();
            Status = ClientStatus.Idle; // Mới vào, rảnh
            _client = client;
            _stream = client.GetStream();
            _clientHandlers = clientHandlers;
            _log = log;
        }

        // Vòng lặp chính: Chạy liên tục để lắng nghe kết quả từ Client
        public async Task StartListeningAsync()
        {
            try
            {
                byte[] buffer = new byte[4096]; // Tăng buffer lên
                while (_client.Connected)
                {
                    int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0)
                    {
                        // Client ngắt kết nối (cleanly)
                        _log($"Client {Id} đã ngắt kết nối.");
                        break; 
                    }

                    string jsonResult = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    ResultMessage? result = JsonSerializer.Deserialize<ResultMessage>(jsonResult);

                    if (result != null)
                    {
                        _log($"[Ket qua] Task {result.TaskId} tu Client {Id}: {result.ResultData}");
                        
                        // QUAN TRỌNG: Đánh dấu Client rảnh trở lại
                        Status = ClientStatus.Idle; 
                    }
                }
            }
            catch (IOException)
            {
                _log($"Client {Id} ngat ket noi dot ngot (IO).");
            }
            catch (Exception e)
            {
                _log($"Loi voi Client {Id}: {e.Message}");
            }
            finally
            {
                // Dọn dẹp
                Status = ClientStatus.Busy; // Ngăn dispatcher giao thêm task
                _client.Close();
                _clientHandlers.TryRemove(Id, out _); // Xóa khỏi danh sách quản lý
                _log($"Da xoa Client {Id}. Tong so clients: {_clientHandlers.Count}");
            }
        }

        // Gửi task cho client này
        public async Task<bool> SendTaskAsync(TaskMessage task)
        {
            try
            {
                Status = ClientStatus.Busy; // Đánh dấu bận
                string jsonTask = JsonSerializer.Serialize(task);
                byte[] data = Encoding.UTF8.GetBytes(jsonTask);
                
                await _stream.WriteAsync(data, 0, data.Length);
                _log($"[Giao viec] Da giao Task {task.TaskId} cho Client {Id}");
                return true;
            }
            catch (Exception e)
            {
                _log($"[LOI GIAO VIEC] Khong the gui task cho Client {Id}: {e.Message}");
                // Nếu gửi lỗi (ví dụ client vừa rớt mạng), dọn dẹp ngay
                _client.Close();
                _clientHandlers.TryRemove(Id, out _);
                _log($"Da xoa Client {Id} do gui task loi. Tong so clients: {_clientHandlers.Count}");
                return false;
            }
        }
    }
}