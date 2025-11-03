using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Shared;
using Server.Persistence;
using TaskStatus = Server.Persistence.PersistenceTaskStatus;

namespace Server
{
    public class ClientHandler : IDisposable
    {
        public string Id { get; }
        public ClientStatus Status { get; private set; }
        public DateTime LastHeartbeatTime { get; private set; }
        
        private TcpClient _client;
        private NetworkStream _stream;
        private ConcurrentDictionary<string, ClientHandler> _clientHandlers;
        private ConcurrentQueue<TaskMessage> _taskQueue;
        private ConcurrentQueue<TaskMessage> _deadLetterQueue;
        private ITaskPersistence _taskPersistence;
        private TaskMessage? _currentTask; // Currently assigned task
        private int _maxRetryCount;
        private Action<string> _log; // Helper function for logging
        
        public ClientHandler(TcpClient client, ConcurrentDictionary<string, ClientHandler> clientHandlers, 
            ConcurrentQueue<TaskMessage> taskQueue, ConcurrentQueue<TaskMessage> deadLetterQueue, 
            ITaskPersistence taskPersistence, int maxRetryCount, Action<string> log)
        {
            Id = Guid.NewGuid().ToString();
            Status = ClientStatus.Idle; // New client, idle
            LastHeartbeatTime = DateTime.Now; // Initialize heartbeat time
            _client = client;
            _stream = client.GetStream();
            _clientHandlers = clientHandlers;
            _taskQueue = taskQueue;
            _deadLetterQueue = deadLetterQueue;
            _taskPersistence = taskPersistence;
            _maxRetryCount = maxRetryCount;
            _currentTask = null;
            _log = log;
        }

        // Main loop: Continuously listen for results from Client
        public async Task StartListeningAsync()
        {
            try
            {
                byte[] buffer = new byte[4096]; // Increased buffer size
                while (_client.Connected)
                {
                    int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0)
                    {
                        // Client disconnected cleanly
                        _log($"Client {Id} disconnected.");
                        break; 
                    }

                    string jsonMessage = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    
                    // Try to determine message type by checking the JSON content
                    await ProcessIncomingMessage(jsonMessage);
                }
            }
            catch (IOException)
            {
                _log($"Client {Id} disconnected abruptly (IO).");
            }
            catch (Exception e)
            {
                _log($"Error with Client {Id}: {e.Message}");
            }
            finally
            {
                // Handle task retry/dead-letter queue if client was processing a task
                if (_currentTask != null)
                {
                    _log($"[RE-QUEUE] Task {_currentTask.TaskId} failed due to Client {Id} death. Processing retry...");
                    
                    _currentTask.RetryCount++;
                    _currentTask.LastRetryAt = DateTime.Now;

                    if (_currentTask.RetryCount < _maxRetryCount)
                    {
                        _taskQueue.Enqueue(_currentTask); // Return to main queue for retry
                        await _taskPersistence.SaveTaskAsync(_currentTask, TaskStatus.Pending);
                        _log($"[RE-QUEUE] Task {_currentTask.TaskId} requeued for retry #{_currentTask.RetryCount}");
                    }
                    else
                    {
                        _deadLetterQueue.Enqueue(_currentTask); // Move to dead-letter queue
                        await _taskPersistence.SaveTaskAsync(_currentTask, TaskStatus.DeadLetter);
                        _log($"[DEAD-LETTER] Task {_currentTask.TaskId} exceeded max retries ({_maxRetryCount}). Moved to dead-letter queue.");
                        
                        // Log to file for audit trail
                        await LogDeadLetterTaskAsync(_currentTask);
                    }
                    
                    _currentTask = null;
                }

                // Cleanup
                Status = ClientStatus.Busy; // Prevent dispatcher from assigning more tasks
                _client.Close();
                _clientHandlers.TryRemove(Id, out _); // Remove from management list
                _log($"Removed Client {Id}. Total clients: {_clientHandlers.Count}");
            }
        }

        // Process incoming messages (task results or heartbeat)
        private async Task ProcessIncomingMessage(string jsonMessage)
        {
            try
            {
                // First, try to parse as BaseMessage to get the type
                var baseMessage = JsonSerializer.Deserialize<BaseMessage>(jsonMessage);
                
                if (baseMessage == null) return;

                switch (baseMessage.Type)
                {
                    case MessageType.Result:
                        var resultWrapper = JsonSerializer.Deserialize<ResultWrapper>(jsonMessage);
                        if (resultWrapper?.Result != null)
                        {
                            _log($"[Result] Task {resultWrapper.Result.TaskId} from Client {Id}: {resultWrapper.Result.ResultData}");
                            
                            // Task completed - update persistence
                            if (_currentTask != null)
                            {
                                var status = resultWrapper.Result.Success ? TaskStatus.Completed : TaskStatus.Failed;
                                await _taskPersistence.SaveTaskAsync(_currentTask, status);
                            }
                            
                            // Task completed successfully - clear current task
                            _currentTask = null;
                            
                            // IMPORTANT: Mark client as idle again
                            Status = ClientStatus.Idle;
                        }
                        break;

                    case MessageType.PingRequest:
                        var pingMessage = JsonSerializer.Deserialize<PingMessage>(jsonMessage);
                        if (pingMessage != null)
                        {
                            // Update heartbeat time
                            LastHeartbeatTime = DateTime.Now;
                            
                            // Send ping response
                            await SendPingResponse();
                            _log($"[Heartbeat] Received ping from Client {Id}");
                        }
                        break;

                    default:
                        // Try legacy format (direct ResultMessage for backward compatibility)
                        var legacyResult = JsonSerializer.Deserialize<ResultMessage>(jsonMessage);
                        if (legacyResult != null && !string.IsNullOrEmpty(legacyResult.TaskId))
                        {
                            _log($"[Result] Task {legacyResult.TaskId} from Client {Id}: {legacyResult.ResultData}");
                            
                            // Task completed - update persistence
                            if (_currentTask != null)
                            {
                                var status = legacyResult.Success ? TaskStatus.Completed : TaskStatus.Failed;
                                await _taskPersistence.SaveTaskAsync(_currentTask, status);
                            }
                            
                            // Task completed successfully - clear current task
                            _currentTask = null;
                            
                            Status = ClientStatus.Idle;
                        }
                        break;
                }
            }
            catch (JsonException ex)
            {
                _log($"[Error] Failed to parse message from Client {Id}: {ex.Message}");
            }
        }

        // Send ping response to client
        private async Task<bool> SendPingResponse()
        {
            try
            {
                var pongMessage = new PongMessage();
                string jsonPong = JsonSerializer.Serialize(pongMessage);
                byte[] data = Encoding.UTF8.GetBytes(jsonPong);
                
                await _stream.WriteAsync(data, 0, data.Length);
                return true;
            }
            catch (Exception e)
            {
                _log($"[Error] Failed to send ping response to Client {Id}: {e.Message}");
                return false;
            }
        }

        // Check if client is alive based on heartbeat
        public bool IsAlive(TimeSpan heartbeatTimeout)
        {
            return DateTime.Now - LastHeartbeatTime <= heartbeatTimeout;
        }

        // Send task to this client
        public async Task<bool> SendTaskAsync(TaskMessage task)
        {
            try
            {
                Status = ClientStatus.Busy; // Mark as busy
                _currentTask = task; // Store current task for retry handling
                
                // Update task status in persistence
                await _taskPersistence.SaveTaskAsync(task, TaskStatus.InProgress);
                
                // Wrap task in TaskWrapper
                var taskWrapper = new TaskWrapper { Task = task };
                string jsonTask = JsonSerializer.Serialize(taskWrapper);
                byte[] data = Encoding.UTF8.GetBytes(jsonTask);
                
                await _stream.WriteAsync(data, 0, data.Length);
                
                var retryInfo = task.RetryCount > 0 ? $" (Retry #{task.RetryCount})" : "";
                _log($"[Task Assigned] Assigned Task {task.TaskId} to Client {Id}{retryInfo}");
                return true;
            }
            catch (Exception e)
            {
                _log($"[ASSIGNMENT ERROR] Cannot send task to Client {Id}: {e.Message}");
                
                // Handle task retry for send failure
                if (_currentTask != null)
                {
                    _currentTask.RetryCount++;
                    _currentTask.LastRetryAt = DateTime.Now;

                    if (_currentTask.RetryCount < _maxRetryCount)
                    {
                        _taskQueue.Enqueue(_currentTask);
                        await _taskPersistence.SaveTaskAsync(_currentTask, TaskStatus.Pending);
                        _log($"[RE-QUEUE] Task {_currentTask.TaskId} requeued due to send failure. Retry #{_currentTask.RetryCount}");
                    }
                    else
                    {
                        _deadLetterQueue.Enqueue(_currentTask);
                        await _taskPersistence.SaveTaskAsync(_currentTask, TaskStatus.DeadLetter);
                        _log($"[DEAD-LETTER] Task {_currentTask.TaskId} moved to dead-letter queue after send failure.");
                        await LogDeadLetterTaskAsync(_currentTask);
                    }
                    
                    _currentTask = null;
                }
                
                // Cleanup client
                _client.Close();
                _clientHandlers.TryRemove(Id, out _);
                _log($"Removed Client {Id} due to task sending error. Total clients: {_clientHandlers.Count}");
                return false;
            }
        }

        // Log dead-letter tasks to file for audit trail
        private async Task LogDeadLetterTaskAsync(TaskMessage task)
        {
            try
            {
                var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] DEAD-LETTER: TaskId={task.TaskId}, " +
                              $"Type={task.Type}, Data={task.Data}, RetryCount={task.RetryCount}, " +
                              $"CreatedAt={task.CreatedAt:yyyy-MM-dd HH:mm:ss}, " +
                              $"LastRetryAt={task.LastRetryAt:yyyy-MM-dd HH:mm:ss}, " +
                              $"ClientId={Id}" + Environment.NewLine;

                await File.AppendAllTextAsync("dead-letter-queue.log", logEntry);
            }
            catch (Exception e)
            {
                _log($"[Error] Failed to log dead-letter task {task.TaskId}: {e.Message}");
            }
        }

        // Get current task info (for monitoring)
        public string GetCurrentTaskInfo()
        {
            if (_currentTask == null)
                return "No active task";
            
            return $"TaskId: {_currentTask.TaskId}, Type: {_currentTask.Type}, RetryCount: {_currentTask.RetryCount}";
        }

        // IDisposable implementation
        public void Dispose()
        {
            try
            {
                Status = ClientStatus.Busy; // Prevent new task assignments
                _client?.Close();
                _stream?.Dispose();
                _clientHandlers.TryRemove(Id, out _);
            }
            catch (Exception e)
            {
                _log($"[Dispose] Error disposing Client {Id}: {e.Message}");
            }
        }
    }
}