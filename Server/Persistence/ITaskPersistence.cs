using Shared;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Server.Persistence
{
    public interface ITaskPersistence
    {
        Task SaveTaskAsync(TaskMessage task, PersistenceTaskStatus status);
        Task<List<TaskMessage>> LoadPendingTasksAsync();
        Task<List<TaskMessage>> LoadDeadLetterTasksAsync();
        Task UpdateTaskStatusAsync(string taskId, PersistenceTaskStatus status);
        Task DeleteTaskAsync(string taskId);
        Task<TaskStatistics> GetStatisticsAsync();
        Task InitializeAsync();
        Task CleanupOldTasksAsync(DateTime cutoffDate);
    }

    public enum PersistenceTaskStatus
    {
        Pending,
        InProgress,
        Completed,
        Failed,
        DeadLetter
    }

    public class TaskStatistics
    {
        public int TotalTasks { get; set; }
        public int PendingTasks { get; set; }
        public int InProgressTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int FailedTasks { get; set; }
        public int DeadLetterTasks { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.Now;
    }

    public class PersistedTask
    {
        public int Id { get; set; }
        public string TaskId { get; set; } = string.Empty;
        public TaskType Type { get; set; }
        public string Data { get; set; } = string.Empty;
        public int RetryCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastRetryAt { get; set; }
        public PersistenceTaskStatus Status { get; set; }
        public DateTime StatusUpdatedAt { get; set; } = DateTime.Now;
        public string? ClientId { get; set; }
        public string? ErrorMessage { get; set; }

        public TaskMessage ToTaskMessage()
        {
            return new TaskMessage
            {
                TaskId = this.TaskId,
                Type = this.Type,
                Data = this.Data,
                RetryCount = this.RetryCount,
                CreatedAt = this.CreatedAt,
                LastRetryAt = this.LastRetryAt
            };
        }

        public static PersistedTask FromTaskMessage(TaskMessage task, PersistenceTaskStatus status)
        {
            return new PersistedTask
            {
                TaskId = task.TaskId,
                Type = task.Type,
                Data = task.Data,
                RetryCount = task.RetryCount,
                CreatedAt = task.CreatedAt,
                LastRetryAt = task.LastRetryAt,
                Status = status,
                StatusUpdatedAt = DateTime.Now
            };
        }
    }
}