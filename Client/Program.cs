using System;
using System.IO;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Shared;

namespace Client
{
    class Program
    {
        private static string _clientId = Guid.NewGuid().ToString();
        private static CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        
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

                    // 2. Start heartbeat sender in background
                    var heartbeatTask = SendHeartbeatAsync(client, _cancellationTokenSource.Token);
                    
                    // 3. Run processing loop (until connection drops)
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
                    _cancellationTokenSource.Cancel(); // Stop heartbeat
                    client.Close();
                    _cancellationTokenSource = new CancellationTokenSource(); // Reset for next connection
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

                string jsonMessage = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                
                // Process incoming message
                await ProcessIncomingMessage(jsonMessage, stream);
            }
        }

        // Process incoming messages from server
        static async Task ProcessIncomingMessage(string jsonMessage, NetworkStream stream)
        {
            try
            {
                // Try to parse as BaseMessage to get the type
                var baseMessage = JsonSerializer.Deserialize<BaseMessage>(jsonMessage);
                
                if (baseMessage == null) return;

                switch (baseMessage.Type)
                {
                    case MessageType.Task:
                        var taskWrapper = JsonSerializer.Deserialize<TaskWrapper>(jsonMessage);
                        if (taskWrapper?.Task != null)
                        {
                            var task = taskWrapper.Task;
                            Console.WriteLine($"[Task Received] Task {task.TaskId}: {task.Type} with data '{task.Data}'");

                            // Execute task
                            ResultMessage result = ExecuteTask(task);

                            // Send result back to Server wrapped in ResultWrapper
                            var resultWrapper = new ResultWrapper { Result = result };
                            string jsonResult = JsonSerializer.Serialize(resultWrapper);
                            byte[] data = Encoding.UTF8.GetBytes(jsonResult);
                            await stream.WriteAsync(data, 0, data.Length);
                            Console.WriteLine($"[Result Sent] Completed Task {task.TaskId}.");
                        }
                        break;

                    case MessageType.PingResponse:
                        var pongMessage = JsonSerializer.Deserialize<PongMessage>(jsonMessage);
                        if (pongMessage != null)
                        {
                            Console.WriteLine($"[Heartbeat] Received pong from server at {pongMessage.Timestamp:HH:mm:ss}");
                        }
                        break;

                    default:
                        // Try legacy format (direct TaskMessage for backward compatibility)
                        var legacyTask = JsonSerializer.Deserialize<TaskMessage>(jsonMessage);
                        if (legacyTask != null && !string.IsNullOrEmpty(legacyTask.TaskId))
                        {
                            Console.WriteLine($"[Task Received] Legacy Task {legacyTask.TaskId}: {legacyTask.Type} with data '{legacyTask.Data}'");

                            ResultMessage result = ExecuteTask(legacyTask);
                            string jsonResult = JsonSerializer.Serialize(result);
                            byte[] data = Encoding.UTF8.GetBytes(jsonResult);
                            await stream.WriteAsync(data, 0, data.Length);
                            Console.WriteLine($"[Result Sent] Completed Task {legacyTask.TaskId}.");
                        }
                        break;
                }
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"[Error] Failed to parse message from server: {ex.Message}");
            }
        }

        // Send heartbeat ping to server every 10 seconds
        static async Task SendHeartbeatAsync(TcpClient client, CancellationToken cancellationToken)
        {
            var stream = client.GetStream();
            
            try
            {
                while (!cancellationToken.IsCancellationRequested && client.Connected)
                {
                    var pingMessage = new PingMessage { ClientId = _clientId };
                    string jsonPing = JsonSerializer.Serialize(pingMessage);
                    byte[] data = Encoding.UTF8.GetBytes(jsonPing);
                    
                    await stream.WriteAsync(data, 0, data.Length, cancellationToken);
                    Console.WriteLine($"[Heartbeat] Sent ping to server");
                    
                    // Wait 10 seconds before next heartbeat
                    await Task.Delay(10000, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("[Heartbeat] Heartbeat cancelled");
            }
            catch (Exception e)
            {
                Console.WriteLine($"[Heartbeat Error] {e.Message}");
            }
        }

        // Task execution function (logic)
        static ResultMessage ExecuteTask(TaskMessage task)
        {
            var result = new ResultMessage { TaskId = task.TaskId, Success = true };
            
            try
            {
                // Simulate random task failures (10% failure rate for testing)
                if (random.Next(1, 11) == 1) // 10% chance
                {
                    throw new Exception("Simulated random task failure for testing");
                }

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

                // Add retry information to result for monitoring
                if (task.RetryCount > 0)
                {
                    result.ResultData += $" (Completed on retry #{task.RetryCount})";
                }
            }
            catch (Exception e)
            {
                result.Success = false;
                result.ResultData = $"EXECUTION ERROR: {e.Message}";
                Console.WriteLine($"[Task Failed] Task {task.TaskId} failed: {e.Message}");
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