using System.Collections.Generic;

namespace Shared
{
    public enum ClientStatus
    {
        Idle, // Rảnh
        Busy  // Bận
    }
    
    // Enum định nghĩa loại task
    public enum TaskType
    {
        CheckPrime,
        HashText
    }

    // Lớp Server gửi cho Client
    public class TaskMessage
    {
        public string TaskId { get; set; } = string.Empty;
        public TaskType Type { get; set; }
        public string Data { get; set; } = string.Empty; // Dữ liệu đầu vào
    }

    // Lớp Client gửi về cho Server
    public class ResultMessage
    {
        public string TaskId { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string ResultData { get; set; } = string.Empty; // Kết quả hoặc thông báo lỗi
    }
}