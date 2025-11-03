using LiteDB;
using Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TaskStatus = Server.Persistence.PersistenceTaskStatus;

namespace Server.Persistence
{
    public class LiteDbTaskPersistence : ITaskPersistence, IDisposable
    {
        private readonly LiteDatabase _database;
        private readonly ILiteCollection<PersistedTask> _tasksCollection;
        private readonly string _databasePath;
        private readonly SemaphoreSlim _semaphore;

        public LiteDbTaskPersistence(string databasePath = "tasks.db")
        {
            _databasePath = databasePath;
            _database = new LiteDatabase(_databasePath);
            _tasksCollection = _database.GetCollection<PersistedTask>("tasks");
            _semaphore = new SemaphoreSlim(1, 1);
            
            // Create indexes for better performance
            _tasksCollection.EnsureIndex(x => x.TaskId, true); // Unique index
            _tasksCollection.EnsureIndex(x => x.Status);
            _tasksCollection.EnsureIndex(x => x.CreatedAt);
            _tasksCollection.EnsureIndex(x => x.StatusUpdatedAt);
        }

        public async Task InitializeAsync()
        {
            await _semaphore.WaitAsync();
            try
            {
                // Database is already initialized in constructor
                var stats = await GetStatisticsAsync();
                Console.WriteLine($"[Persistence] Database initialized. Total tasks: {stats.TotalTasks}");
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task SaveTaskAsync(TaskMessage task, TaskStatus status)
        {
            await _semaphore.WaitAsync();
            try
            {
                var persistedTask = PersistedTask.FromTaskMessage(task, status);
                
                // Check if task already exists
                var existingTask = _tasksCollection.FindOne(x => x.TaskId == task.TaskId);
                if (existingTask != null)
                {
                    // Update existing task
                    existingTask.Status = status;
                    existingTask.RetryCount = task.RetryCount;
                    existingTask.LastRetryAt = task.LastRetryAt;
                    existingTask.StatusUpdatedAt = DateTime.Now;
                    _tasksCollection.Update(existingTask);
                }
                else
                {
                    // Insert new task
                    _tasksCollection.Insert(persistedTask);
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<List<TaskMessage>> LoadPendingTasksAsync()
        {
            await _semaphore.WaitAsync();
            try
            {
                var pendingTasks = _tasksCollection
                    .Find(x => x.Status == TaskStatus.Pending)
                    .OrderBy(x => x.CreatedAt)
                    .ToList();

                return pendingTasks.Select(x => x.ToTaskMessage()).ToList();
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<List<TaskMessage>> LoadDeadLetterTasksAsync()
        {
            await _semaphore.WaitAsync();
            try
            {
                var deadLetterTasks = _tasksCollection
                    .Find(x => x.Status == TaskStatus.DeadLetter)
                    .OrderBy(x => x.StatusUpdatedAt)
                    .ToList();

                return deadLetterTasks.Select(x => x.ToTaskMessage()).ToList();
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task UpdateTaskStatusAsync(string taskId, TaskStatus status)
        {
            await _semaphore.WaitAsync();
            try
            {
                var task = _tasksCollection.FindOne(x => x.TaskId == taskId);
                if (task != null)
                {
                    task.Status = status;
                    task.StatusUpdatedAt = DateTime.Now;
                    _tasksCollection.Update(task);
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task DeleteTaskAsync(string taskId)
        {
            await _semaphore.WaitAsync();
            try
            {
                _tasksCollection.DeleteMany(x => x.TaskId == taskId);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<TaskStatistics> GetStatisticsAsync()
        {
            await _semaphore.WaitAsync();
            try
            {
                var stats = new TaskStatistics
                {
                    TotalTasks = _tasksCollection.Count(),
                    PendingTasks = _tasksCollection.Count(x => x.Status == TaskStatus.Pending),
                    InProgressTasks = _tasksCollection.Count(x => x.Status == TaskStatus.InProgress),
                    CompletedTasks = _tasksCollection.Count(x => x.Status == TaskStatus.Completed),
                    FailedTasks = _tasksCollection.Count(x => x.Status == TaskStatus.Failed),
                    DeadLetterTasks = _tasksCollection.Count(x => x.Status == TaskStatus.DeadLetter),
                    LastUpdated = DateTime.Now
                };

                return stats;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task CleanupOldTasksAsync(DateTime cutoffDate)
        {
            await _semaphore.WaitAsync();
            try
            {
                // Delete completed tasks older than cutoff date
                var deletedCount = _tasksCollection.DeleteMany(x => 
                    x.Status == TaskStatus.Completed && 
                    x.StatusUpdatedAt < cutoffDate);

                Console.WriteLine($"[Persistence] Cleaned up {deletedCount} old completed tasks");
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public void Dispose()
        {
            _semaphore?.Dispose();
            _database?.Dispose();
        }
    }
}