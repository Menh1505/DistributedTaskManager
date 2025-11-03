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

            // Run 5 background tasks (background threads)
            _ = TaskProducerAsync();      // Task 1: Continuously create new tasks
            _ = TaskDispatcherAsync();    // Task 2: Continuously dispatch tasks
            _ = HeartbeatMonitorAsync();  // Task 3: Monitor client heartbeats
            _ = DeadLetterMonitorAsync(); // Task 4: Monitor dead-letter queue
            _ = PersistenceCleanupAsync(); // Task 5: Cleanup old completed tasks

            // Task 6 (Main): Listen for client connections
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
                
                // Persist task to storage
                await _taskPersistence.SaveTaskAsync(task, TaskStatus.Pending);
                
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
            var idleClients = _clientHandlers.Values.Count(c => c.Status == ClientStatus.Idle);
            var busyClients = _clientHandlers.Values.Count(c => c.Status == ClientStatus.Busy);
            var pendingTasks = _taskQueue.Count;
            var deadLetterTasks = _deadLetterQueue.Count;
            
            Log($"[Statistics] Active Clients: {activeClients} (Idle: {idleClients}, Busy: {busyClients})");
            Log($"[Statistics] Pending Tasks: {pendingTasks}, Dead-Letter: {deadLetterTasks}");
            
            // Log current tasks for busy clients
            foreach (var client in _clientHandlers.Values.Where(c => c.Status == ClientStatus.Busy))
            {
                Log($"[Statistics] Client {client.Id}: {client.GetCurrentTaskInfo()}");
            }
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