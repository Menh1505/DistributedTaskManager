using Shared;

namespace ClientWebApp.Models
{
    public class TaskViewModel
    {
        public bool IsConnected { get; set; }
        public string ClientStatus { get; set; } = string.Empty;
        public TaskMessage? CurrentTask { get; set; }
        public List<string> ConnectionLogs { get; set; } = new List<string>();
        public string CustomResult { get; set; } = string.Empty;
        public bool TaskSuccess { get; set; } = true;
    }
}