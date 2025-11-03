using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Shared;
using TaskStatus = Server.Persistence.PersistenceTaskStatus;

namespace Server.Persistence
{
    public class FileTaskPersistence : ITaskPersistence
    {
        private readonly string _pendingTasksFile;
        private readonly string _completedTasksFile;
        private readonly string _deadLetterTasksFile;
        private readonly string _statisticsFile;
        private readonly SemaphoreSlim _semaphore;

        public FileTaskPersistence(string dataDirectory = "data")
        {
            if (!Directory.Exists(dataDirectory))
            {
                Directory.CreateDirectory(dataDirectory);
            }

            _pendingTasksFile = Path.Combine(dataDirectory, "tasks_pending.json");
            _completedTasksFile = Path.Combine(dataDirectory, "tasks_completed.json");
            _deadLetterTasksFile = Path.Combine(dataDirectory, "tasks_deadletter.json");
            _statisticsFile = Path.Combine(dataDirectory, "statistics.json");
            _semaphore = new SemaphoreSlim(1, 1);
        }

        public async Task InitializeAsync()
        {
            await _semaphore.WaitAsync();
            try
            {
                // Create files if they don't exist
                await EnsureFileExistsAsync(_pendingTasksFile, "[]");
                await EnsureFileExistsAsync(_completedTasksFile, "[]");
                await EnsureFileExistsAsync(_deadLetterTasksFile, "[]");
                
                var stats = await GetStatisticsAsync();
                Console.WriteLine($"[Persistence] File persistence initialized. Total tasks: {stats.TotalTasks}");
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private async Task EnsureFileExistsAsync(string filePath, string defaultContent)
        {
            if (!File.Exists(filePath))
            {
                await File.WriteAllTextAsync(filePath, defaultContent);
            }
        }

        public async Task SaveTaskAsync(TaskMessage task, TaskStatus status)
        {
            await _semaphore.WaitAsync();
            try
            {
                var persistedTask = PersistedTask.FromTaskMessage(task, status);
                
                switch (status)
                {
                    case TaskStatus.Pending:
                    case TaskStatus.InProgress:
                        await SaveTaskToFileAsync(_pendingTasksFile, persistedTask);
                        await RemoveTaskFromFileAsync(_completedTasksFile, task.TaskId);
                        await RemoveTaskFromFileAsync(_deadLetterTasksFile, task.TaskId);
                        break;
                    
                    case TaskStatus.Completed:
                        await SaveTaskToFileAsync(_completedTasksFile, persistedTask);
                        await RemoveTaskFromFileAsync(_pendingTasksFile, task.TaskId);
                        await RemoveTaskFromFileAsync(_deadLetterTasksFile, task.TaskId);
                        break;
                    
                    case TaskStatus.DeadLetter:
                        await SaveTaskToFileAsync(_deadLetterTasksFile, persistedTask);
                        await RemoveTaskFromFileAsync(_pendingTasksFile, task.TaskId);
                        await RemoveTaskFromFileAsync(_completedTasksFile, task.TaskId);
                        break;
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private async Task SaveTaskToFileAsync(string filePath, PersistedTask task)
        {
            var tasks = await LoadTasksFromFileAsync(filePath);
            
            // Remove existing task with same ID
            tasks.RemoveAll(t => t.TaskId == task.TaskId);
            
            // Add updated task
            tasks.Add(task);
            
            var json = JsonSerializer.Serialize(tasks, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePath, json);
        }

        private async Task RemoveTaskFromFileAsync(string filePath, string taskId)
        {
            var tasks = await LoadTasksFromFileAsync(filePath);
            var removed = tasks.RemoveAll(t => t.TaskId == taskId);
            
            if (removed > 0)
            {
                var json = JsonSerializer.Serialize(tasks, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(filePath, json);
            }
        }

        private async Task<List<PersistedTask>> LoadTasksFromFileAsync(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return new List<PersistedTask>();
            }

            try
            {
                var json = await File.ReadAllTextAsync(filePath);
                return JsonSerializer.Deserialize<List<PersistedTask>>(json) ?? new List<PersistedTask>();
            }
            catch (Exception)
            {
                // If file is corrupted, return empty list
                return new List<PersistedTask>();
            }
        }

        public async Task<List<TaskMessage>> LoadPendingTasksAsync()
        {
            await _semaphore.WaitAsync();
            try
            {
                var persistedTasks = await LoadTasksFromFileAsync(_pendingTasksFile);
                return persistedTasks
                    .OrderBy(x => x.CreatedAt)
                    .Select(x => x.ToTaskMessage())
                    .ToList();
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
                var persistedTasks = await LoadTasksFromFileAsync(_deadLetterTasksFile);
                return persistedTasks
                    .OrderBy(x => x.StatusUpdatedAt)
                    .Select(x => x.ToTaskMessage())
                    .ToList();
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task UpdateTaskStatusAsync(string taskId, TaskStatus status)
        {
            // For file persistence, we need to find the task and move it to the appropriate file
            var pendingTasks = await LoadTasksFromFileAsync(_pendingTasksFile);
            var completedTasks = await LoadTasksFromFileAsync(_completedTasksFile);
            var deadLetterTasks = await LoadTasksFromFileAsync(_deadLetterTasksFile);

            var task = pendingTasks.FirstOrDefault(t => t.TaskId == taskId) ??
                      completedTasks.FirstOrDefault(t => t.TaskId == taskId) ??
                      deadLetterTasks.FirstOrDefault(t => t.TaskId == taskId);

            if (task != null)
            {
                task.Status = status;
                task.StatusUpdatedAt = DateTime.Now;
                await SaveTaskAsync(task.ToTaskMessage(), status);
            }
        }

        public async Task DeleteTaskAsync(string taskId)
        {
            await _semaphore.WaitAsync();
            try
            {
                await RemoveTaskFromFileAsync(_pendingTasksFile, taskId);
                await RemoveTaskFromFileAsync(_completedTasksFile, taskId);
                await RemoveTaskFromFileAsync(_deadLetterTasksFile, taskId);
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
                var pendingTasks = await LoadTasksFromFileAsync(_pendingTasksFile);
                var completedTasks = await LoadTasksFromFileAsync(_completedTasksFile);
                var deadLetterTasks = await LoadTasksFromFileAsync(_deadLetterTasksFile);

                var stats = new TaskStatistics
                {
                    PendingTasks = pendingTasks.Count,
                    InProgressTasks = pendingTasks.Count(t => t.Status == TaskStatus.InProgress),
                    CompletedTasks = completedTasks.Count,
                    FailedTasks = completedTasks.Count(t => t.Status == TaskStatus.Failed),
                    DeadLetterTasks = deadLetterTasks.Count,
                    LastUpdated = DateTime.Now
                };

                stats.TotalTasks = stats.PendingTasks + stats.CompletedTasks + stats.DeadLetterTasks;

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
                var completedTasks = await LoadTasksFromFileAsync(_completedTasksFile);
                var oldTasks = completedTasks.Where(t => t.StatusUpdatedAt < cutoffDate).ToList();
                
                foreach (var oldTask in oldTasks)
                {
                    completedTasks.Remove(oldTask);
                }

                if (oldTasks.Any())
                {
                    var json = JsonSerializer.Serialize(completedTasks, new JsonSerializerOptions { WriteIndented = true });
                    await File.WriteAllTextAsync(_completedTasksFile, json);
                    Console.WriteLine($"[Persistence] Cleaned up {oldTasks.Count} old completed tasks");
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}