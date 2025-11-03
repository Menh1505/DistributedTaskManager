using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
        // 1. Task queue (thread-safe)
        private static ConcurrentQueue<TaskMessage> _taskQueue = new ConcurrentQueue<TaskMessage>();
        
        // 2. Client management list (thread-safe)
        private static ConcurrentDictionary<string, ClientHandler> _clientHandlers = new ConcurrentDictionary<string, ClientHandler>();

        private static int _taskCounter = 0; // For generating task IDs

        static async Task Main(string[] args)
        {
            // Run 3 background tasks (background threads)
            _ = TaskProducerAsync();      // Task 1: Continuously create new tasks
            _ = TaskDispatcherAsync();    // Task 2: Continuously dispatch tasks
            _ = HeartbeatMonitorAsync();  // Task 3: Monitor client heartbeats

            // Task 4 (Main): Listen for client connections
            await StartServerListenerAsync(); 
        }

        // Main loop: Listen for new clients
        static async Task StartServerListenerAsync()
        {
            TcpListener server = new TcpListener(IPAddress.Any, 12345);
            server.Start();
            Log("Server started. Waiting for clients...");

            while (true)
            {
                try
                {
                    TcpClient client = await server.AcceptTcpClientAsync();
                    
                    // Create a new handler for the client
                    var clientHandler = new ClientHandler(client, _clientHandlers, Log);
                    _clientHandlers.TryAdd(clientHandler.Id, clientHandler);

                    Log($"Client {clientHandler.Id} connected. Total clients: {_clientHandlers.Count}");
                    
                    // Start listening loop for this client (don't await)
                    _ = clientHandler.StartListeningAsync();
                }
                catch (Exception e)
                {
                    Log($"Error accepting client: {e.Message}");
                }
            }
        }

        // Background loop 1: Task dispatcher
        static async Task TaskDispatcherAsync()
        {
            Log("[Dispatcher] Starting task dispatching...");
            while (true)
            {
                if (!_taskQueue.IsEmpty)
                {
                    // Find the first idle client
                    var idleClient = _clientHandlers.Values.FirstOrDefault(c => c.Status == ClientStatus.Idle);

                    if (idleClient != null)
                    {
                        // There's an idle client & available task
                        if (_taskQueue.TryDequeue(out TaskMessage? task))
                        {
                            await idleClient.SendTaskAsync(task);
                        }
                    }
                }
                
                // Avoid CPU exhaustion
                await Task.Delay(100); 
            }
        }

        // Background loop 2: Task producer (Demo)
        static async Task TaskProducerAsync()
        {
            Log("[Producer] Starting task creation...");
            var random = new Random();
            while (true)
            {
                int num = _taskCounter++;
                var task = new TaskMessage
                {
                    TaskId = $"Task-{num}",
                    Type = num % 2 == 0 ? TaskType.CheckPrime : TaskType.HashText,
                    Data = num % 2 == 0 ? random.Next(1000, 50000).ToString() : $"String to hash: {num}"
                };

                _taskQueue.Enqueue(task);
                Log($"[Producer] Added Task {task.TaskId} to queue. ({_taskQueue.Count} tasks)");
                
                await Task.Delay(2000); // Create new task every 2 seconds
            }
        }

        // Background loop 3: Heartbeat monitor
        static async Task HeartbeatMonitorAsync()
        {
            Log("[HeartbeatMonitor] Starting client heartbeat monitoring...");
            var heartbeatTimeout = TimeSpan.FromSeconds(30); // 30 seconds timeout
            
            while (true)
            {
                var deadClients = new List<string>();
                
                // Check all clients for heartbeat timeout
                foreach (var kvp in _clientHandlers)
                {
                    var clientId = kvp.Key;
                    var clientHandler = kvp.Value;
                    
                    if (!clientHandler.IsAlive(heartbeatTimeout))
                    {
                        deadClients.Add(clientId);
                        Log($"[HeartbeatMonitor] Client {clientId} heartbeat timeout. Marking for removal.");
                    }
                }
                
                // Remove dead clients
                foreach (var deadClientId in deadClients)
                {
                    if (_clientHandlers.TryRemove(deadClientId, out var deadClient))
                    {
                        try
                        {
                            deadClient?.Dispose(); // Assuming we'll add IDisposable
                            Log($"[HeartbeatMonitor] Removed dead client {deadClientId}. Total clients: {_clientHandlers.Count}");
                        }
                        catch (Exception e)
                        {
                            Log($"[HeartbeatMonitor] Error disposing client {deadClientId}: {e.Message}");
                        }
                    }
                }
                
                // Check every 5 seconds
                await Task.Delay(5000);
            }
        }

        // Helper log (to distinguish output)
        static void Log(string message)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
        }
    }
}