using System;
using System.IO;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Shared;

namespace Client
{
    class Program
    {
        static async Task Main(string[] args)
        {
            string serverIp = "127.0.0.1";
            int port = 12345;
            
            // Vòng lặp vĩnh viễn, tự động kết nối lại
            while (true) 
            {
                TcpClient client = new TcpClient();
                try
                {
                    // 1. Kết nối
                    Console.WriteLine($"Dang thu ket noi den Server {serverIp}:{port}...");
                    await client.ConnectAsync(serverIp, port);
                    Console.WriteLine("=> Da ket noi thanh cong. San sang nhan viec!");

                    // 2. Chạy vòng lặp xử lý (cho đến khi rớt mạng)
                    await ProcessServerTasksAsync(client);
                }
                catch (SocketException)
                {
                    Console.WriteLine("Khong the ket noi den Server. Thu lai sau 5 giay...");
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Loi: {e.Message}. Thu lai sau 5 giay...");
                }
                finally
                {
                    client.Close();
                    await Task.Delay(5000); // Chờ 5s trước khi thử kết nối lại
                }
            }
        }

        // Vòng lặp chính: Nhận task -> Làm -> Gửi kết quả
        static async Task ProcessServerTasksAsync(TcpClient client)
        {
            NetworkStream stream = client.GetStream();
            byte[] buffer = new byte[4096];

            while (client.Connected)
            {
                // 1. Chờ nhận nhiệm vụ
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead == 0)
                {
                    Console.WriteLine("Server da ngat ket noi.");
                    break;
                }

                string jsonTask = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                TaskMessage? task = JsonSerializer.Deserialize<TaskMessage>(jsonTask);

                if (task == null) continue;
                
                Console.WriteLine($"[Nhan viec] Task {task.TaskId}: {task.Type} voi data '{task.Data}'");

                // 2. Thực thi nhiệm vụ
                ResultMessage result = ExecuteTask(task);

                // 3. Gửi kết quả về Server
                string jsonResult = JsonSerializer.Serialize(result);
                byte[] data = Encoding.UTF8.GetBytes(jsonResult);
                await stream.WriteAsync(data, 0, data.Length);
                Console.WriteLine($"[Gui ket qua] Da hoan thanh Task {task.TaskId}.");
            }
        }

        // Hàm thực thi (logic)
        static ResultMessage ExecuteTask(TaskMessage task)
        {
            var result = new ResultMessage { TaskId = task.TaskId, Success = true };
            
            try
            {
                // Giả lập công việc tốn thời gian
                Task.Delay(random.Next(1000, 3000)).Wait(); 
                
                switch (task.Type)
                {
                    case TaskType.CheckPrime:
                        int num = int.Parse(task.Data);
                        result.ResultData = IsPrime(num).ToString();
                        break;
                    case TaskType.HashText:
                        result.ResultData = HashString(task.Data);
                        break;
                    default:
                        throw new Exception("Loai task khong biet");
                }
            }
            catch (Exception e)
            {
                result.Success = false;
                result.ResultData = $"LOI THUC THI: {e.Message}";
            }
            
            return result;
        }

        // --- Các hàm nghiệp vụ ---
        private static Random random = new Random();

        static bool IsPrime(int number)
        {
            if (number <= 1) return false;
            if (number == 2) return true;
            if (number % 2 == 0) return false;
            for (int i = 3; i <= Math.Sqrt(number); i += 2)
            {
                if (number % i == 0) return false;
            }
            return true;
        }

        static string HashString(string input)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString().Substring(0, 16) + "..."; // Rút gọn cho dễ nhìn
            }
        }
    }
}