using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Shared;

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
        private Action<string> _log; // Helper function for logging
        
        public ClientHandler(TcpClient client, ConcurrentDictionary<string, ClientHandler> clientHandlers, Action<string> log)
        {
            Id = Guid.NewGuid().ToString();
            Status = ClientStatus.Idle; // New client, idle
            LastHeartbeatTime = DateTime.Now; // Initialize heartbeat time
            _client = client;
            _stream = client.GetStream();
            _clientHandlers = clientHandlers;
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
                
                // Wrap task in TaskWrapper
                var taskWrapper = new TaskWrapper { Task = task };
                string jsonTask = JsonSerializer.Serialize(taskWrapper);
                byte[] data = Encoding.UTF8.GetBytes(jsonTask);
                
                await _stream.WriteAsync(data, 0, data.Length);
                _log($"[Task Assigned] Assigned Task {task.TaskId} to Client {Id}");
                return true;
            }
            catch (Exception e)
            {
                _log($"[ASSIGNMENT ERROR] Cannot send task to Client {Id}: {e.Message}");
                // If sending fails (e.g., client just dropped connection), cleanup immediately
                _client.Close();
                _clientHandlers.TryRemove(Id, out _);
                _log($"Removed Client {Id} due to task sending error. Total clients: {_clientHandlers.Count}");
                return false;
            }
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