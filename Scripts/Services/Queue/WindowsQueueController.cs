using System;
using ExtDebugLogger;
using WindowsSystem.Log;

namespace WindowsSystem
{
    public class WindowsQueueController
    {
        private readonly IWindowsService _windowsService;
        private WindowsQueue _mainQueue;

        public WindowsQueueController(IWindowsService windowsService)
        {
            _windowsService = windowsService;
            _mainQueue = new WindowsQueue(windowsService);
        }

        public void Next() => _mainQueue?.Next();

        public void RunQueue()
        {
            if (_mainQueue == null)
                _mainQueue = new WindowsQueue(_windowsService);
            if (_mainQueue.IsRunning)
                return;

            _mainQueue.OnQueueFinished += OnQueueFinished;

            if (_mainQueue.Run())
            {
                Logger.Log($"Windows queue is run!", WSLogTag.WindowsQueueController);
            }
            else
            {
                Logger.Error($"Windows queue run is aborted!", WSLogTag.WindowsQueueController);
            }
        }

        public void AddWindowsGroupInQueue(WindowsGroupBase windowsGroupBase)
        {
            foreach (Type windowType in windowsGroupBase.WindowTypes)
                AddWindowByTypeInQueue(windowType);
        }

        public void AddWindowByTypeInQueue(Type type)
        {
            if (_mainQueue.AddWindowByTypeIn(type))
            {
                Logger.Log($"A new window '{type.ToString().Split('.')[^1]}' has been successfully added to the window queue.", WSLogTag.WindowsQueueController);
            }
            else
            {
                Logger.Error($"Couldn't add window '{type.ToString().Split('.')[^1]}' was not added to the queue because the window does not exist in the context of the scene.", WSLogTag.WindowsQueueController);
            }
        }

        public void AddWindowInQueue(IWindowBase window) => 
            WindowInQueue(new QueueWindowItem(window));

        public void AddWindowInQueue(IPooledWindow window, int index, bool removeAfterExtract, bool returnToDefault = true) => 
            WindowInQueue(new (window, index, removeAfterExtract, returnToDefault));

        private void WindowInQueue(in QueueWindowItem queueWindowItem)
        {
            if (_mainQueue.AddWindowItemIn(in queueWindowItem))
                Logger.Log(
                    $"A new window '{queueWindowItem.WindowBase.GetType().ToString().Split('.')[^1]}' has been successfully added to the window queue.",
                    WSLogTag.WindowsQueueController);
            else
                Logger.Warn(
                    $"Couldn't add window '{queueWindowItem.WindowBase.GetType().ToString().Split('.')[^1]}' was not added to the queue because it already exists.",
                    WSLogTag.WindowsQueueController);
        }

        private void OnQueueFinished()
        {
            _mainQueue.OnQueueFinished -= OnQueueFinished;
            Logger.Log($"Windows queue is finished!", WSLogTag.WindowsQueueController);
        }
    }
}