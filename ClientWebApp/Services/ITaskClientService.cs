using Shared;

namespace ClientWebApp.Services
{
    public interface ITaskClientService
    {
        bool IsConnected { get; }
        TaskMessage? CurrentTask { get; }
        string ClientStatus { get; }
        List<string> ConnectionLogs { get; }
        
        Task ConnectAsync();
        Task DisconnectAsync();
        Task<TaskMessage?> RequestTaskAsync();
        Task<bool> CompleteTaskAsync(string result);
        Task<bool> CompleteTaskAsync(string result, bool success);
        void ClearLogs();
    }
}