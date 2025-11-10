using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Shared;

namespace ClientWebApp.Services
{
    public class TaskClientService : ITaskClientService, IDisposable
    {
        private TcpClient? _client;
        private NetworkStream? _stream;
        private readonly string _clientId = Guid.NewGuid().ToString();
        private readonly string _clientName = $"WebClient-{Environment.MachineName}";
        private readonly List<TaskType> _capabilities = Enum.GetValues<TaskType>().ToList();
        private readonly List<string> _connectionLogs = new List<string>();
        private TaskMessage? _currentTask;
        private bool _isProcessingTask = false;
        private readonly Random _random = new Random();

        public bool IsConnected => _client?.Connected == true;
        public TaskMessage? CurrentTask => _currentTask;
        public string ClientStatus => _isProcessingTask ? "Processing Task" : IsConnected ? "Connected & Ready" : "Disconnected";
        public List<string> ConnectionLogs => _connectionLogs.ToList(); // Return copy

        public async Task ConnectAsync()
        {
            try
            {
                if (IsConnected)
                {
                    AddLog("Already connected to server");
                    return;
                }

                _client = new TcpClient();
                AddLog("Attempting to connect to server...");
                
                await _client.ConnectAsync("127.0.0.1", 12345);
                _stream = _client.GetStream();
                
                AddLog("Connected successfully!");
                
                // Register with server
                await RegisterWithServerAsync();
            }
            catch (Exception ex)
            {
                AddLog($"Connection failed: {ex.Message}");
                _client?.Close();
                _client = null;
                _stream = null;
                throw;
            }
        }

        public async Task DisconnectAsync()
        {
            try
            {
                _client?.Close();
                _client = null;
                _stream = null;
                _currentTask = null;
                _isProcessingTask = false;
                AddLog("Disconnected from server");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                AddLog($"Error during disconnect: {ex.Message}");
            }
        }

        public async Task<TaskMessage?> RequestTaskAsync()
        {
            if (!IsConnected)
            {
                AddLog("Not connected to server. Please connect first.");
                return null;
            }

            if (_currentTask != null)
            {
                AddLog("Already have a current task. Complete it first.");
                return _currentTask;
            }

            try
            {
                AddLog("Requesting task from server...");
                
                // Send task request message
                var taskRequest = new TaskRequestMessage { ClientId = _clientId };
                string jsonRequest = JsonSerializer.Serialize(taskRequest);
                byte[] requestData = Encoding.UTF8.GetBytes(jsonRequest);
                await _stream!.WriteAsync(requestData, 0, requestData.Length);
                
                // Listen for response
                byte[] buffer = new byte[4096];
                int bytesRead = await _stream!.ReadAsync(buffer, 0, buffer.Length);
                
                if (bytesRead == 0)
                {
                    AddLog("Server disconnected");
                    await DisconnectAsync();
                    return null;
                }

                string jsonMessage = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                
                // Try to parse response
                var baseMessage = JsonSerializer.Deserialize<BaseMessage>(jsonMessage);
                
                if (baseMessage?.Type == MessageType.Task)
                {
                    var taskWrapper = JsonSerializer.Deserialize<TaskWrapper>(jsonMessage);
                    if (taskWrapper?.Task != null)
                    {
                        _currentTask = taskWrapper.Task;
                        AddLog($"Received Task: {_currentTask.TaskId} - {_currentTask.Type} with data '{_currentTask.Data}'");
                        return _currentTask;
                    }
                }
                else if (baseMessage?.Type == MessageType.NoTaskAvailable)
                {
                    var noTaskMessage = JsonSerializer.Deserialize<NoTaskAvailableMessage>(jsonMessage);
                    AddLog($"No tasks available: {noTaskMessage?.Message}");
                    return null;
                }
                else
                {
                    // Try legacy format for backward compatibility
                    var legacyTask = JsonSerializer.Deserialize<TaskMessage>(jsonMessage);
                    if (legacyTask != null && !string.IsNullOrEmpty(legacyTask.TaskId))
                    {
                        _currentTask = legacyTask;
                        AddLog($"Received Legacy Task: {_currentTask.TaskId} - {_currentTask.Type} with data '{_currentTask.Data}'");
                        return _currentTask;
                    }
                }

                AddLog("Received unexpected response from server");
                return null;
            }
            catch (Exception ex)
            {
                AddLog($"Error requesting task: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> CompleteTaskAsync(string result)
        {
            return await CompleteTaskAsync(result, true);
        }

        public async Task<bool> CompleteTaskAsync(string result, bool success)
        {
            if (_currentTask == null)
            {
                AddLog("No current task to complete");
                return false;
            }

            if (!IsConnected)
            {
                AddLog("Not connected to server");
                return false;
            }

            try
            {
                _isProcessingTask = true;
                
                // Simulate processing time
                AddLog($"Processing task {_currentTask.TaskId}...");
                await Task.Delay(1000);

                string processedResult = result;
                
                // If no custom result provided, execute the task automatically
                if (string.IsNullOrWhiteSpace(result))
                {
                    processedResult = ExecuteTask(_currentTask);
                }

                var resultMessage = new ResultMessage
                {
                    TaskId = _currentTask.TaskId,
                    Success = success,
                    ResultData = processedResult
                };

                // Send result back to server wrapped in ResultWrapper
                var resultWrapper = new ResultWrapper { Result = resultMessage };
                string jsonResult = JsonSerializer.Serialize(resultWrapper);
                byte[] data = Encoding.UTF8.GetBytes(jsonResult);
                
                await _stream!.WriteAsync(data, 0, data.Length);
                
                AddLog($"Task {_currentTask.TaskId} completed and sent to server. Result: {processedResult}");
                
                _currentTask = null;
                _isProcessingTask = false;
                return true;
            }
            catch (Exception ex)
            {
                AddLog($"Error completing task: {ex.Message}");
                _isProcessingTask = false;
                return false;
            }
        }

        private async Task RegisterWithServerAsync()
        {
            try
            {
                var registerMessage = new RegisterMessage
                {
                    ClientId = _clientId,
                    ClientName = _clientName,
                    Capabilities = _capabilities,
                    Version = "2.0.0"
                };

                string jsonRegister = JsonSerializer.Serialize(registerMessage);
                byte[] data = Encoding.UTF8.GetBytes(jsonRegister);

                await _stream!.WriteAsync(data, 0, data.Length);
                AddLog($"Sent registration to server with capabilities: {string.Join(", ", _capabilities)}");
            }
            catch (Exception ex)
            {
                AddLog($"Registration failed: {ex.Message}");
                throw;
            }
        }

        private string ExecuteTask(TaskMessage task)
        {
            try
            {
                // Simulate random task failures (5% failure rate for testing)
                if (_random.Next(1, 21) == 1) // 5% chance
                {
                    throw new Exception("Simulated random task failure");
                }

                switch (task.Type)
                {
                    case TaskType.CheckPrime:
                        int num = int.Parse(task.Data);
                        bool isPrime = IsPrime(num);
                        return $"{num} is {(isPrime ? "prime" : "not prime")}";
                        
                    case TaskType.HashText:
                        string hash = HashString(task.Data);
                        return $"Hash of '{task.Data}': {hash}";
                        
                    default:
                        throw new Exception($"Unknown task type: {task.Type}");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Task execution failed: {ex.Message}");
            }
        }

        private bool IsPrime(int number)
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

        private string HashString(string input)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
                StringBuilder builder = new StringBuilder();
                
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                
                return builder.ToString().Substring(0, 16) + "...";
            }
        }

        private void AddLog(string message)
        {
            var logEntry = $"[{DateTime.Now:HH:mm:ss}] {message}";
            _connectionLogs.Add(logEntry);
            
            // Keep only last 50 log entries
            if (_connectionLogs.Count > 50)
            {
                _connectionLogs.RemoveAt(0);
            }
        }

        public void ClearLogs()
        {
            _connectionLogs.Clear();
            AddLog("Logs cleared");
        }

        public void Dispose()
        {
            DisconnectAsync().Wait();
        }
    }
}