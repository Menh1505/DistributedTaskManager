using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Shared;

namespace Server
{
    class Program
    {
        // 1. Hàng đợi task (thread-safe)
        private static ConcurrentQueue<TaskMessage> _taskQueue = new ConcurrentQueue<TaskMessage>();
        
        // 2. Danh sách quản lý client (thread-safe)
        private static ConcurrentDictionary<string, ClientHandler> _clientHandlers = new ConcurrentDictionary<string, ClientHandler>();

        private static int _taskCounter = 0; // Để tạo task ID

        static async Task Main(string[] args)
        {
            // Chạy 2 Task nền (background threads)
            _ = TaskProducerAsync();      // Task 1: Liên tục tạo task mới
            _ = TaskDispatcherAsync();    // Task 2: Liên tục điều phối task

            // Task 3 (Main): Lắng nghe kết nối client
            await StartServerListenerAsync(); 
        }

        // Vòng lặp chính: Lắng nghe client mới
        static async Task StartServerListenerAsync()
        {
            TcpListener server = new TcpListener(IPAddress.Any, 12345);
            server.Start();
            Log("Server da khoi dong. Dang cho Clients...");

            while (true)
            {
                try
                {
                    TcpClient client = await server.AcceptTcpClientAsync();
                    
                    // Tạo một handler mới cho client
                    var clientHandler = new ClientHandler(client, _clientHandlers, Log);
                    _clientHandlers.TryAdd(clientHandler.Id, clientHandler);

                    Log($"Client {clientHandler.Id} da ket noi. Tong so clients: {_clientHandlers.Count}");
                    
                    // Khởi chạy vòng lặp lắng nghe cho client này (không await)
                    _ = clientHandler.StartListeningAsync();
                }
                catch (Exception e)
                {
                    Log($"Loi khi chap nhan client: {e.Message}");
                }
            }
        }

        // Vòng lặp (nền) 1: Bộ điều phối
        static async Task TaskDispatcherAsync()
        {
            Log("[Dispatcher] Bat dau dieu phoi task...");
            while (true)
            {
                if (!_taskQueue.IsEmpty)
                {
                    // Tìm client rảnh đầu tiên
                    var idleClient = _clientHandlers.Values.FirstOrDefault(c => c.Status == ClientStatus.Idle);

                    if (idleClient != null)
                    {
                        // Có client rảnh & có task
                        if (_taskQueue.TryDequeue(out TaskMessage? task))
                        {
                            await idleClient.SendTaskAsync(task);
                        }
                    }
                }
                
                // Tránh vắt kiệt CPU
                await Task.Delay(100); 
            }
        }

        // Vòng lặp (nền) 2: Bộ tạo task (Demo)
        static async Task TaskProducerAsync()
        {
            Log("[Producer] Bat dau tao task...");
            var random = new Random();
            while (true)
            {
                int num = _taskCounter++;
                var task = new TaskMessage
                {
                    TaskId = $"Task-{num}",
                    Type = num % 2 == 0 ? TaskType.CheckPrime : TaskType.HashText,
                    Data = num % 2 == 0 ? random.Next(1000, 50000).ToString() : $"Chuoi can hash: {num}"
                };

                _taskQueue.Enqueue(task);
                Log($"[Producer] Da them Task {task.TaskId} vao hang doi. ({_taskQueue.Count} tasks)");
                
                await Task.Delay(2000); // Tạo task mới mỗi 2 giây
            }
        }

        // Helper log (để phân biệt output)
        static void Log(string message)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
        }
    }
}