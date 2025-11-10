using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Shared;
using Server.Persistence;
using TaskStatus = Server.Persistence.PersistenceTaskStatus;

namespace Server
{
    class Program
    {
        // 1. Task queue (thread-safe)
        private static ConcurrentQueue<TaskMessage> _taskQueue = new ConcurrentQueue<TaskMessage>();
        
        // 2. Client management list (thread-safe)
        private static ConcurrentDictionary<string, ClientHandler> _clientHandlers = new ConcurrentDictionary<string, ClientHandler>();

        // 3. Dead-letter queue for failed tasks (thread-safe)
        private static ConcurrentQueue<TaskMessage> _deadLetterQueue = new ConcurrentQueue<TaskMessage>();

        // 4. Persistence layer for durable storage
        private static ITaskPersistence _taskPersistence = null!;

        private static int _taskCounter = 0; // For generating task IDs
        private const int MAX_RETRY_COUNT = 3; // Maximum retry attempts

        static async Task Main(string[] args)
        {
            // Initialize persistence layer
            await InitializePersistenceAsync(args);

            // Restore queues from persistent storage
            await RestoreQueuesAsync();

            // Run 4 background tasks (background threads)
            // _ = TaskProducerAsync();      // REMOVED: No longer auto-create tasks
            _ = TaskDispatcherAsync();    // Task 1: Continuously dispatch tasks
            _ = HeartbeatMonitorAsync();  // Task 2: Monitor client heartbeats
            _ = DeadLetterMonitorAsync(); // Task 3: Monitor dead-letter queue
            _ = PersistenceCleanupAsync(); // Task 4: Cleanup old completed tasks

            // Start console interface for manual task creation
            StartConsoleInterface();

            // Task 5 (Main): Listen for client connections
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
                    var clientHandler = new ClientHandler(client, _clientHandlers, _taskQueue, _deadLetterQueue, _taskPersistence, MAX_RETRY_COUNT, Log);
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

        // Background loop 1: Task dispatcher (with capability-based routing)
        static async Task TaskDispatcherAsync()
        {
            Log("[Dispatcher] Starting intelligent task dispatching...");
            while (true)
            {
                if (!_taskQueue.IsEmpty)
                {
                    // Peek at the next task to check its type
                    if (_taskQueue.TryPeek(out TaskMessage? nextTask))
                    {
                        // Find an idle client that can handle this task type
                        var capableClient = _clientHandlers.Values.FirstOrDefault(c => 
                            c.Status == ClientStatus.Idle && 
                            c.IsRegistered && 
                            c.CanHandleTask(nextTask.Type));

                        if (capableClient != null)
                        {
                            // Dequeue the task and assign it
                            if (_taskQueue.TryDequeue(out TaskMessage? task))
                            {
                                await capableClient.SendTaskAsync(task);
                                Log($"[Dispatcher] Assigned {task.Type} task {task.TaskId} to capable client {capableClient.ClientName}");
                            }
                        }
                        else
                        {
                            // No capable client available, check if any client can handle this task type
                            var hasCapableClients = _clientHandlers.Values.Any(c => 
                                c.IsRegistered && c.CanHandleTask(nextTask.Type));

                            if (!hasCapableClients)
                            {
                                // No client can handle this task type, move to dead-letter queue
                                if (_taskQueue.TryDequeue(out TaskMessage? unhandleableTask))
                                {
                                    _deadLetterQueue.Enqueue(unhandleableTask);
                                    Log($"[Dispatcher] No capable clients for {unhandleableTask.Type} task {unhandleableTask.TaskId}. Moved to dead-letter queue.");
                                }
                            }
                        }
                    }
                }
                
                // Avoid CPU exhaustion
                await Task.Delay(100); 
            }
        }

        // Manual task creation methods (replaces automatic producer)
        public static void CreateTask(TaskType type, string data)
        {
            int num = _taskCounter++;
            var task = new TaskMessage
            {
                TaskId = $"Task-{num}",
                Type = type,
                Data = data,
                CreatedAt = DateTime.Now
            };

            _taskQueue.Enqueue(task);
            
            // Persist task to storage (async fire-and-forget)
            _ = Task.Run(async () => {
                try
                {
                    await _taskPersistence.SaveTaskAsync(task, TaskStatus.Pending);
                }
                catch (Exception e)
                {
                    Log($"[TaskCreation] Error persisting task {task.TaskId}: {e.Message}");
                }
            });
            
            Log($"[TaskCreation] Created Task {task.TaskId} ({type}): {data}. Queue size: {_taskQueue.Count}");
        }

        public static void CreateMultipleTasks(TaskType type, string[] dataArray)
        {
            foreach (string data in dataArray)
            {
                CreateTask(type, data);
            }
            Log($"[TaskCreation] Created {dataArray.Length} tasks of type {type}");
        }

        // Console interface for manual task creation
        public static void StartConsoleInterface()
        {
            _ = Task.Run(async () =>
            {
                Log("[Console] Manual task creation interface started. Type 'help' for commands.");
                
                while (true)
                {
                    try
                    {
                        Console.Write("TaskManager> ");
                        var input = Console.ReadLine();
                        
                        if (string.IsNullOrWhiteSpace(input))
                            continue;

                        var parts = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        var command = parts[0].ToLower();

                        switch (command)
                        {
                            case "help":
                                ShowHelp();
                                break;
                                
                            case "create":
                                HandleCreateCommand(parts);
                                break;
                                
                            case "status":
                                ShowStatus();
                                break;
                                
                            case "stats":
                                LogStatistics();
                                break;
                                
                            case "clients":
                                ShowClients();
                                break;
                                
                            case "queue":
                                ShowQueue();
                                break;
                                
                            case "clear-deadletter":
                                ClearDeadLetterQueue();
                                break;
                                
                            case "reprocess-deadletter":
                                ReprocessDeadLetterTasks();
                                break;
                                
                            case "exit":
                                Log("[Console] Shutting down server...");
                                Environment.Exit(0);
                                break;
                                
                            default:
                                Log($"[Console] Unknown command: {command}. Type 'help' for available commands.");
                                break;
                        }
                    }
                    catch (Exception e)
                    {
                        Log($"[Console] Error processing command: {e.Message}");
                    }
                }
            });
        }

        private static void ShowHelp()
        {
            Log("[Console] Available commands:");
            Log("  create prime <number>          - Create a prime check task");
            Log("  create hash <text>             - Create a hash text task");
            Log("  create batch prime <start> <end> - Create multiple prime tasks");
            Log("  create batch hash <count>      - Create multiple hash tasks");
            Log("  status                         - Show system status");
            Log("  stats                          - Show detailed statistics");
            Log("  clients                        - Show connected clients");
            Log("  queue                          - Show task queue status");
            Log("  clear-deadletter               - Clear dead-letter queue");
            Log("  reprocess-deadletter           - Reprocess dead-letter tasks");
            Log("  exit                           - Shutdown server");
        }

        private static void HandleCreateCommand(string[] parts)
        {
            if (parts.Length < 3)
            {
                Log("[Console] Invalid create command. Usage: create <type> <data> or create batch <type> <params>");
                return;
            }

            var type = parts[1].ToLower();
            
            if (type == "batch")
            {
                HandleBatchCreateCommand(parts);
                return;
            }

            var taskType = type switch
            {
                "prime" => TaskType.CheckPrime,
                "hash" => TaskType.HashText,
                _ => (TaskType?)null
            };

            if (!taskType.HasValue)
            {
                Log("[Console] Invalid task type. Use 'prime' or 'hash'.");
                return;
            }

            var data = string.Join(" ", parts.Skip(2));
            CreateTask(taskType.Value, data);
        }

        private static void HandleBatchCreateCommand(string[] parts)
        {
            if (parts.Length < 4)
            {
                Log("[Console] Invalid batch create command.");
                return;
            }

            var type = parts[2].ToLower();
            var taskType = type switch
            {
                "prime" => TaskType.CheckPrime,
                "hash" => TaskType.HashText,
                _ => (TaskType?)null
            };

            if (!taskType.HasValue)
            {
                Log("[Console] Invalid task type for batch. Use 'prime' or 'hash'.");
                return;
            }

            if (taskType == TaskType.CheckPrime && parts.Length >= 5)
            {
                if (int.TryParse(parts[3], out int start) && int.TryParse(parts[4], out int end))
                {
                    var data = new List<string>();
                    for (int i = start; i <= end; i++)
                    {
                        data.Add(i.ToString());
                    }
                    CreateMultipleTasks(taskType.Value, data.ToArray());
                }
                else
                {
                    Log("[Console] Invalid range for prime batch. Use: create batch prime <start> <end>");
                }
            }
            else if (taskType == TaskType.HashText)
            {
                if (int.TryParse(parts[3], out int count))
                {
                    var data = new List<string>();
                    for (int i = 0; i < count; i++)
                    {
                        data.Add($"Hash text #{i + 1} - {DateTime.Now:HH:mm:ss.fff}");
                    }
                    CreateMultipleTasks(taskType.Value, data.ToArray());
                }
                else
                {
                    Log("[Console] Invalid count for hash batch. Use: create batch hash <count>");
                }
            }
        }

        private static void ShowStatus()
        {
            Log($"[Console] System Status:");
            Log($"  Task Queue: {_taskQueue.Count} pending");
            Log($"  Dead-Letter Queue: {_deadLetterQueue.Count} failed");
            Log($"  Connected Clients: {_clientHandlers.Count}");
            Log($"  Registered Clients: {_clientHandlers.Values.Count(c => c.IsRegistered)}");
            Log($"  Idle Clients: {_clientHandlers.Values.Count(c => c.Status == ClientStatus.Idle)}");
            Log($"  Busy Clients: {_clientHandlers.Values.Count(c => c.Status == ClientStatus.Busy)}");
        }

        private static void ShowClients()
        {
            Log($"[Console] Connected Clients ({_clientHandlers.Count}):");
            foreach (var client in _clientHandlers.Values)
            {
                var status = client.IsRegistered ? $"{client.Status}" : "Unregistered";
                var capabilities = client.IsRegistered ? string.Join(",", client.GetCapabilities()) : "N/A";
                Log($"  {client.Id}: {client.ClientName} - Status: {status} - Capabilities: [{capabilities}]");
            }
        }

        private static void ShowQueue()
        {
            Log($"[Console] Task Queue Status:");
            Log($"  Pending Tasks: {_taskQueue.Count}");
            Log($"  Dead-Letter Tasks: {_deadLetterQueue.Count}");
            
            if (_taskQueue.Count > 0)
            {
                Log("  Next 5 pending tasks:");
                var tasks = _taskQueue.ToArray().Take(5);
                foreach (var task in tasks)
                {
                    Log($"    {task.TaskId}: {task.Type} - {task.Data}");
                }
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

        // Background loop 4: Dead-letter queue monitor
        static async Task DeadLetterMonitorAsync()
        {
            Log("[DeadLetterMonitor] Starting dead-letter queue monitoring...");
            int lastReportedCount = 0;
            
            while (true)
            {
                var currentCount = _deadLetterQueue.Count;
                
                // Report dead-letter queue size changes
                if (currentCount != lastReportedCount)
                {
                    Log($"[DeadLetterMonitor] Dead-letter queue size: {currentCount} tasks");
                    lastReportedCount = currentCount;
                }
                
                // Periodic statistics report (every 5 minutes)
                if (DateTime.Now.Minute % 5 == 0 && DateTime.Now.Second < 30)
                {
                    LogStatistics();
                }
                
                // Check every 30 seconds
                await Task.Delay(30000);
            }
        }

        // Log system statistics
        static void LogStatistics()
        {
            var activeClients = _clientHandlers.Count;
            var registeredClients = _clientHandlers.Values.Count(c => c.IsRegistered);
            var idleClients = _clientHandlers.Values.Count(c => c.Status == ClientStatus.Idle);
            var busyClients = _clientHandlers.Values.Count(c => c.Status == ClientStatus.Busy);
            var pendingTasks = _taskQueue.Count;
            var deadLetterTasks = _deadLetterQueue.Count;
            
            Log($"[Statistics] Active Clients: {activeClients} (Registered: {registeredClients}, Idle: {idleClients}, Busy: {busyClients})");
            Log($"[Statistics] Pending Tasks: {pendingTasks}, Dead-Letter: {deadLetterTasks}");
            
            // Log client capabilities
            foreach (var client in _clientHandlers.Values.Where(c => c.IsRegistered))
            {
                var statusInfo = client.Status == ClientStatus.Busy ? client.GetCurrentTaskInfo() : "Idle";
                Log($"[Statistics] {client.GetCapabilitiesInfo()} - Status: {statusInfo}");
            }

            // Log unregistered clients
            var unregisteredClients = _clientHandlers.Values.Where(c => !c.IsRegistered).ToList();
            if (unregisteredClients.Any())
            {
                Log($"[Statistics] Unregistered clients: {unregisteredClients.Count}");
            }

            // Log capability distribution
            var capabilityStats = new Dictionary<TaskType, int>();
            foreach (var taskType in Enum.GetValues<TaskType>())
            {
                var clientCount = _clientHandlers.Values.Count(c => c.IsRegistered && c.CanHandleTask(taskType));
                capabilityStats[taskType] = clientCount;
            }
            
            var capabilityInfo = string.Join(", ", capabilityStats.Select(kvp => $"{kvp.Key}: {kvp.Value}"));
            Log($"[Statistics] Capability distribution: {capabilityInfo}");
        }

        // Reprocess all dead-letter tasks (admin function)
        public static int ReprocessDeadLetterTasks()
        {
            int reprocessedCount = 0;
            var tasksToReprocess = new List<TaskMessage>();
            
            // Collect all dead-letter tasks
            while (_deadLetterQueue.TryDequeue(out TaskMessage? task))
            {
                if (task != null)
                {
                    task.RetryCount = 0; // Reset retry count
                    task.LastRetryAt = null;
                    tasksToReprocess.Add(task);
                }
            }
            
            // Re-enqueue to main task queue
            foreach (var task in tasksToReprocess)
            {
                _taskQueue.Enqueue(task);
                reprocessedCount++;
            }
            
            Log($"[Admin] Reprocessed {reprocessedCount} tasks from dead-letter queue");
            return reprocessedCount;
        }

        // Clear dead-letter queue (admin function)
        public static int ClearDeadLetterQueue()
        {
            int clearedCount = 0;
            while (_deadLetterQueue.TryDequeue(out _))
            {
                clearedCount++;
            }
            
            Log($"[Admin] Cleared {clearedCount} tasks from dead-letter queue");
            return clearedCount;
        }

        // Get dead-letter queue statistics
        public static void GetDeadLetterStatistics()
        {
            Log($"[Statistics] Dead-letter queue size: {_deadLetterQueue.Count}");
            Log($"[Statistics] Main task queue size: {_taskQueue.Count}");
            Log($"[Statistics] Active clients: {_clientHandlers.Count}");
        }

        // Initialize persistence layer based on command line arguments
        static async Task InitializePersistenceAsync(string[] args)
        {
            var useFileStorage = args.Contains("--file-storage");
            
            if (useFileStorage)
            {
                Log("[Persistence] Using file-based persistence");
                _taskPersistence = new FileTaskPersistence();
            }
            else
            {
                Log("[Persistence] Using LiteDB persistence");
                _taskPersistence = new LiteDbTaskPersistence();
            }

            await _taskPersistence.InitializeAsync();
        }

        // Restore queues from persistent storage on startup
        static async Task RestoreQueuesAsync()
        {
            Log("[Persistence] Restoring queues from persistent storage...");
            
            // Restore pending tasks
            var pendingTasks = await _taskPersistence.LoadPendingTasksAsync();
            foreach (var task in pendingTasks)
            {
                _taskQueue.Enqueue(task);
            }
            
            // Restore dead-letter tasks
            var deadLetterTasks = await _taskPersistence.LoadDeadLetterTasksAsync();
            foreach (var task in deadLetterTasks)
            {
                _deadLetterQueue.Enqueue(task);
            }

            Log($"[Persistence] Restored {pendingTasks.Count} pending tasks and {deadLetterTasks.Count} dead-letter tasks");
            
            // Update task counter based on existing tasks
            if (pendingTasks.Any() || deadLetterTasks.Any())
            {
                var allTasks = pendingTasks.Concat(deadLetterTasks);
                var maxTaskNumber = allTasks
                    .Where(t => t.TaskId.StartsWith("Task-"))
                    .Select(t => 
                    {
                        var parts = t.TaskId.Split('-');
                        return parts.Length > 1 && int.TryParse(parts[1], out int num) ? num : 0;
                    })
                    .DefaultIfEmpty(0)
                    .Max();
                
                _taskCounter = maxTaskNumber + 1;
                Log($"[Persistence] Reset task counter to {_taskCounter}");
            }
        }

        // Background loop 5: Cleanup old tasks periodically
        static async Task PersistenceCleanupAsync()
        {
            Log("[PersistenceCleanup] Starting periodic cleanup of old tasks...");
            
            while (true)
            {
                try
                {
                    // Cleanup tasks older than 7 days
                    var cutoffDate = DateTime.Now.AddDays(-7);
                    await _taskPersistence.CleanupOldTasksAsync(cutoffDate);
                    
                    // Log persistence statistics
                    var stats = await _taskPersistence.GetStatisticsAsync();
                    Log($"[PersistenceStats] Total: {stats.TotalTasks}, Pending: {stats.PendingTasks}, " +
                        $"Completed: {stats.CompletedTasks}, Dead-Letter: {stats.DeadLetterTasks}");
                }
                catch (Exception e)
                {
                    Log($"[PersistenceCleanup] Error during cleanup: {e.Message}");
                }
                
                // Run cleanup every hour
                await Task.Delay(TimeSpan.FromHours(1));
            }
        }

        // Helper log (to distinguish output)
        static void Log(string message)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
        }
    }
}