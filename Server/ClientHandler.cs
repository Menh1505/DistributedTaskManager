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
        private Action<string> _log; // Helper function for logging
        
        public ClientHandler(TcpClient client, ConcurrentDictionary<string, ClientHandler> clientHandlers, Action<string> log)
        {
            Id = Guid.NewGuid().ToString();
            Status = ClientStatus.Idle; // New client, idle
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

                    string jsonResult = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    ResultMessage? result = JsonSerializer.Deserialize<ResultMessage>(jsonResult);

                    if (result != null)
                    {
                        _log($"[Result] Task {result.TaskId} from Client {Id}: {result.ResultData}");
                        
                        // IMPORTANT: Mark client as idle again
                        Status = ClientStatus.Idle; 
                    }
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

        // Send task to this client
        public async Task<bool> SendTaskAsync(TaskMessage task)
        {
            try
            {
                Status = ClientStatus.Busy; // Mark as busy
                string jsonTask = JsonSerializer.Serialize(task);
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
    }
}