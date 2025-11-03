using System.Collections.Generic;

namespace Shared
{
    public enum ClientStatus
    {
        Idle, // Available
        Busy  // Working
    }

    // Enum defining task types
    public enum TaskType
    {
        CheckPrime,
        HashText
    }

    // Class sent from Server to Client
    public class TaskMessage
    {
        public string TaskId { get; set; } = string.Empty;
        public TaskType Type { get; set; }
        public string Data { get; set; } = string.Empty; // Input data
    }

    // Class sent from Client back to Server
    public class ResultMessage
    {
        public string TaskId { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string ResultData { get; set; } = string.Empty; // Result or error message
    }
}