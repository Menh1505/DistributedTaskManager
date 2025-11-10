using Microsoft.AspNetCore.Mvc;
using ClientWebApp.Services;
using ClientWebApp.Models;

namespace ClientWebApp.Controllers
{
    public class TaskController : Controller
    {
        private readonly ITaskClientService _taskClientService;

        public TaskController(ITaskClientService taskClientService)
        {
            _taskClientService = taskClientService;
        }

        public IActionResult Index()
        {
            var model = new TaskViewModel
            {
                IsConnected = _taskClientService.IsConnected,
                ClientStatus = _taskClientService.ClientStatus,
                CurrentTask = _taskClientService.CurrentTask,
                ConnectionLogs = _taskClientService.ConnectionLogs
            };

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Connect()
        {
            try
            {
                await _taskClientService.ConnectAsync();
                TempData["Message"] = "Connected to server successfully!";
                TempData["MessageType"] = "success";
            }
            catch (Exception ex)
            {
                TempData["Message"] = $"Connection failed: {ex.Message}";
                TempData["MessageType"] = "error";
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> Disconnect()
        {
            await _taskClientService.DisconnectAsync();
            TempData["Message"] = "Disconnected from server";
            TempData["MessageType"] = "info";
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> RequestTask()
        {
            try
            {
                var task = await _taskClientService.RequestTaskAsync();
                if (task != null)
                {
                    TempData["Message"] = $"Received task: {task.TaskId}";
                    TempData["MessageType"] = "success";
                }
                else
                {
                    TempData["Message"] = "No task received or connection issue";
                    TempData["MessageType"] = "warning";
                }
            }
            catch (Exception ex)
            {
                TempData["Message"] = $"Error requesting task: {ex.Message}";
                TempData["MessageType"] = "error";
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> CompleteTask(string customResult = "", bool success = true)
        {
            try
            {
                bool completed = await _taskClientService.CompleteTaskAsync(customResult, success);
                if (completed)
                {
                    TempData["Message"] = "Task completed and sent to server!";
                    TempData["MessageType"] = "success";
                }
                else
                {
                    TempData["Message"] = "Failed to complete task";
                    TempData["MessageType"] = "error";
                }
            }
            catch (Exception ex)
            {
                TempData["Message"] = $"Error completing task: {ex.Message}";
                TempData["MessageType"] = "error";
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        public IActionResult ClearLogs()
        {
            _taskClientService.ClearLogs();
            TempData["Message"] = "Logs cleared";
            TempData["MessageType"] = "info";
            return RedirectToAction("Index");
        }

        // API endpoint for AJAX updates
        [HttpGet]
        public JsonResult GetStatus()
        {
            return Json(new
            {
                isConnected = _taskClientService.IsConnected,
                clientStatus = _taskClientService.ClientStatus,
                currentTask = _taskClientService.CurrentTask,
                logs = _taskClientService.ConnectionLogs.TakeLast(10)
            });
        }
    }
}