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

    // Enum for message types (including heartbeat)
    public enum MessageType
    {
        Task,
        Result,
        PingRequest,
        PingResponse,
        Register,
        RegisterResponse
    }

    // Class sent from Server to Client
    public class TaskMessage
    {
        public string TaskId { get; set; } = string.Empty;
        public TaskType Type { get; set; }
        public string Data { get; set; } = string.Empty; // Input data
        public int RetryCount { get; set; } = 0; // Number of retry attempts
        public DateTime CreatedAt { get; set; } = DateTime.Now; // Task creation time
        public DateTime? LastRetryAt { get; set; } // Last retry timestamp
    }

    // Class sent from Client back to Server
    public class ResultMessage
    {
        public string TaskId { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string ResultData { get; set; } = string.Empty; // Result or error message
    }

    // Base message class for all communication
    public class BaseMessage
    {
        public MessageType Type { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    // Heartbeat ping request message
    public class PingMessage : BaseMessage
    {
        public string ClientId { get; set; } = string.Empty;
        
        public PingMessage()
        {
            Type = MessageType.PingRequest;
        }
    }

    // Heartbeat ping response message
    public class PongMessage : BaseMessage
    {
        public string ServerId { get; set; } = "Server";
        
        public PongMessage()
        {
            Type = MessageType.PingResponse;
        }
    }

    // Wrapper for task messages
    public class TaskWrapper : BaseMessage
    {
        public TaskMessage Task { get; set; } = new TaskMessage();
        
        public TaskWrapper()
        {
            Type = MessageType.Task;
        }
    }

    // Wrapper for result messages
    public class ResultWrapper : BaseMessage
    {
        public ResultMessage Result { get; set; } = new ResultMessage();
        
        public ResultWrapper()
        {
            Type = MessageType.Result;
        }
    }

    // Client registration message
    public class RegisterMessage : BaseMessage
    {
        public string ClientId { get; set; } = string.Empty;
        public List<TaskType> Capabilities { get; set; } = new List<TaskType>();
        public string ClientName { get; set; } = string.Empty;
        public string Version { get; set; } = "1.0.0";
        
        public RegisterMessage()
        {
            Type = MessageType.Register;
        }
    }

    // Server registration response
    public class RegisterResponseMessage : BaseMessage
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string ServerId { get; set; } = "Server";
        public List<TaskType> AcceptedCapabilities { get; set; } = new List<TaskType>();
        
        public RegisterResponseMessage()
        {
            Type = MessageType.RegisterResponse;
        }
    }
}