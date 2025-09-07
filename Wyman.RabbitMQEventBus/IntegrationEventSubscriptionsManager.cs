namespace Wyman.RabbitMQEventBus;

/// <summary>
/// 集成事件订阅管理器类，用于管理集成事件的订阅和取消订阅。
/// </summary>
internal class IntegrationEventSubscriptionsManager : IDisposable
{
    private readonly Dictionary<string, List<Type>> _eventHandlers = new();
    private readonly ReaderWriterLockSlim _readWriteLock = new(LockRecursionPolicy.NoRecursion);

    public bool IsEmpty
    {
        get
        {
            _readWriteLock.EnterReadLock();
            try { return _eventHandlers.Count == 0; }
            finally { _readWriteLock.ExitReadLock(); }
        }
    }

    public void AddSubscription(string eventName, Type handlerType)
    {
        _readWriteLock.EnterWriteLock();
        try
        {
            if (!_eventHandlers.TryGetValue(eventName, out var handlerTypes))
            {
                handlerTypes = [];
                _eventHandlers[eventName] = handlerTypes;
            }
            if (handlerTypes.Contains(handlerType))
            {
                throw new ArgumentException($"Handler Type {handlerType.Name} already registered for '{eventName}'", nameof(handlerType));
            }
            handlerTypes.Add(handlerType);
        }
        finally
        {
            _readWriteLock.ExitWriteLock();
        }
    }

    public void RemoveSubscription(string eventName, Type handlerType)
    {
        _readWriteLock.EnterWriteLock();
        try
        {
            if (_eventHandlers.TryGetValue(eventName, out var handlers))
            {
                handlers.Remove(handlerType);
                if (handlers.Count == 0)
                {
                    _eventHandlers.Remove(eventName);
                }
            }
        }
        finally
        {
            _readWriteLock.ExitWriteLock();
        }
    }

    public List<Type> GetHandlersForEvent(string eventName)
    {
        _readWriteLock.EnterReadLock();
        try { return _eventHandlers.TryGetValue(eventName, out var handlers) ? handlers : []; }
        finally { _readWriteLock.ExitReadLock(); }
    }

    public bool HasSubscriptionsForEvent(string eventName)
    {
        _readWriteLock.EnterReadLock();
        try { return _eventHandlers.ContainsKey(eventName); }
        finally { _readWriteLock.ExitReadLock(); }
    }

    public void Dispose()
    {
        _readWriteLock.Dispose();
    }
}

