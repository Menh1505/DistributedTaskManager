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
            
            // Infinite loop, auto reconnect
            while (true) 
            {
                TcpClient client = new TcpClient();
                try
                {
                    // 1. Connect
                    Console.WriteLine($"Attempting to connect to Server {serverIp}:{port}...");
                    await client.ConnectAsync(serverIp, port);
                    Console.WriteLine("=> Connected successfully. Ready to work!");

                    // 2. Run processing loop (until connection drops)
                    await ProcessServerTasksAsync(client);
                }
                catch (SocketException)
                {
                    Console.WriteLine("Cannot connect to Server. Retrying in 5 seconds...");
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error: {e.Message}. Retrying in 5 seconds...");
                }
                finally
                {
                    client.Close();
                    await Task.Delay(5000); // Wait 5s before retrying connection
                }
            }
        }

        // Main loop: Receive task -> Process -> Send result
        static async Task ProcessServerTasksAsync(TcpClient client)
        {
            NetworkStream stream = client.GetStream();
            byte[] buffer = new byte[4096];

            while (client.Connected)
            {
                // 1. Wait to receive task
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead == 0)
                {
                    Console.WriteLine("Server disconnected.");
                    break;
                }

                string jsonTask = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                TaskMessage? task = JsonSerializer.Deserialize<TaskMessage>(jsonTask);

                if (task == null) continue;
                
                Console.WriteLine($"[Task Received] Task {task.TaskId}: {task.Type} with data '{task.Data}'");

                // 2. Execute task
                ResultMessage result = ExecuteTask(task);

                // 3. Send result back to Server
                string jsonResult = JsonSerializer.Serialize(result);
                byte[] data = Encoding.UTF8.GetBytes(jsonResult);
                await stream.WriteAsync(data, 0, data.Length);
                Console.WriteLine($"[Result Sent] Completed Task {task.TaskId}.");
            }
        }

        // Task execution function (logic)
        static ResultMessage ExecuteTask(TaskMessage task)
        {
            var result = new ResultMessage { TaskId = task.TaskId, Success = true };
            
            try
            {
                // Simulate time-consuming work
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
                        throw new Exception("Unknown task type");
                }
            }
            catch (Exception e)
            {
                result.Success = false;
                result.ResultData = $"EXECUTION ERROR: {e.Message}";
            }
            
            return result;
        }

        // --- Business logic functions ---
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
                return builder.ToString().Substring(0, 16) + "..."; // Shortened for readability
            }
        }
    }
}