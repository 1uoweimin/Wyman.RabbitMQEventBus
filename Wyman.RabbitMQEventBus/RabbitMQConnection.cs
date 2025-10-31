using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Wyman.RabbitMQEventBus;

/// <summary>
/// RabbitMQ连接类，用于管理RabbitMQ连接。
/// </summary>
internal class RabbitMQConnection : IAsyncDisposable
{
    private readonly IConnectionFactory _connectionFactory;
    private readonly ILogger<RabbitMQConnection> _logger;
    private readonly SemaphoreSlim _connectionSemaphore = new(1, 1);
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private IConnection? _connection;
    private int _reconnectAttempts = 0;
    private readonly int _maxReconnectAttempts = 10;
    private readonly Timer? _heartbeatTimer;
    private readonly TimeSpan _heartbeatInterval = TimeSpan.FromSeconds(30);
    private bool _disposed = false;

    public RabbitMQConnection(IConnectionFactory connectionFactory, ILoggerFactory loggerFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _logger = loggerFactory.CreateLogger<RabbitMQConnection>() ?? throw new ArgumentNullException(nameof(loggerFactory));

        // 启动心跳检测定时器
        _heartbeatTimer = new Timer(_ =>
        {
            if (_disposed || _connection == null || _connection.IsOpen) return;
            Task.Run(HandleConnectionFailureAsync);

        }, null, _heartbeatInterval, _heartbeatInterval);
    }

    public bool IsConnected => !_disposed && _connection != null && _connection.IsOpen;

    public async Task<bool> TryConnectionAsync()
    {
        if (_disposed) return false;

        await _connectionSemaphore.WaitAsync(_cancellationTokenSource.Token);
        try
        {
            if (IsConnected) return true;
            return await CreateConnectionInternalAsync();
        }
        finally
        {
            _connectionSemaphore.Release();
        }
    }

    public async Task<IChannel> CreateChannelAsync()
    {
        if (!await EnsureConnectionAsync())
        {
            throw new InvalidOperationException("No RabbitMQ connections are available to perform this action");
        }

        try
        {
            return await _connection!.CreateChannelAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create channel");
            throw;
        }
    }

    /// <summary>
    /// 强制重连（用于手动触发重连）
    /// </summary>
    public async Task ForceReconnectAsync()
    {
        if (_disposed) return;

        _logger.LogInformation("Force reconnection requested");
        _reconnectAttempts = 0; // 重置重连计数

        await _connectionSemaphore.WaitAsync(_cancellationTokenSource.Token);
        try
        {
            await ClearConnectionAsync();

            await CreateConnectionInternalAsync();
        }
        finally
        {
            _connectionSemaphore.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        try
        {
            await _cancellationTokenSource.CancelAsync();
        }
        finally
        {
            if (_heartbeatTimer != null)
            {
                await _heartbeatTimer.DisposeAsync();
            }
            if (_connection != null)
            {
                await ClearConnectionAsync();
            }
            _connectionSemaphore.Dispose();
            _cancellationTokenSource.Dispose();

            _disposed = true;
        }
    }

    #region Private Method

    private async Task ClearConnectionAsync()
    {
        if (_connection != null)
        {
            _connection.ConnectionShutdownAsync -= ConnectionShutdownAsync;
            _connection.CallbackExceptionAsync -= CallbackExceptionAsync;
            _connection.ConnectionBlockedAsync -= ConnectionBlockedAsync;
            _connection.ConnectionUnblockedAsync -= ConnectionUnblockedAsync;

            await _connection.DisposeAsync();
            _connection = null;

            _logger.LogInformation("Cleared RabbitMQ connection");
        }
    }

    private async Task<bool> EnsureConnectionAsync()
    {
        if (IsConnected) return true;

        if (_reconnectAttempts >= _maxReconnectAttempts)
        {
            _logger.LogError("Maximum reconnection attempts ({MaxAttempts}) reached", _maxReconnectAttempts);
            return false;
        }

        // 尝试重连
        if (await TryReconnectAsync()) return true;

        return false;
    }

    private async Task HandleConnectionFailureAsync()
    {
        if (_disposed) return;

        try
        {
            // 清理旧连接
            await ClearConnectionAsync();

            // 尝试重连
            if (_reconnectAttempts < _maxReconnectAttempts)
            {
                _logger.LogInformation("Attempting to reconnect after connection failure");
                await TryReconnectAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling connection failure");
        }
    }

    private async Task<bool> TryReconnectAsync()
    {
        if (_disposed || _reconnectAttempts >= _maxReconnectAttempts) return false;

        // 指数退避策略
        var delay = TimeSpan.FromSeconds(Math.Min(Math.Pow(2, _reconnectAttempts), 60));
        _logger.LogInformation("Waiting {Delay} before reconnection attempt {Attempt}", delay, _reconnectAttempts + 1);

        await Task.Delay(delay, _cancellationTokenSource.Token);

        return await CreateConnectionInternalAsync();
    }

    private async Task<bool> CreateConnectionInternalAsync()
    {
        try
        {
            _reconnectAttempts++;

            _logger.LogInformation("Attempting to connect to RabbitMQ (attempt {Attempt}/{MaxAttempts})", _reconnectAttempts, _maxReconnectAttempts);

            _connection = await _connectionFactory.CreateConnectionAsync();

            if (_connection.IsOpen)
            {
                _reconnectAttempts = 0;

                _connection.ConnectionShutdownAsync += ConnectionShutdownAsync;
                _connection.CallbackExceptionAsync += CallbackExceptionAsync;
                _connection.ConnectionBlockedAsync += ConnectionBlockedAsync;
                _connection.ConnectionUnblockedAsync += ConnectionUnblockedAsync;

                _logger.LogInformation("Successfully connected to RabbitMQ");
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to RabbitMQ (attempt {Attempt}/{MaxAttempts})", _reconnectAttempts, _maxReconnectAttempts);
        }

        return false;
    }

    private async Task ConnectionShutdownAsync(object sender, ShutdownEventArgs e)
    {
        if (_disposed) return;

        _logger.LogWarning("RabbitMQ connection shutdown: {ShutdownInitiator}, {ShutdownReason}", e.Initiator, e.ReplyText);

        // 只有在非主动关闭的情况下才尝试重连
        if (e.Initiator != ShutdownInitiator.Application)
        {
            await HandleConnectionFailureAsync();
        }
    }

    private async Task CallbackExceptionAsync(object sender, CallbackExceptionEventArgs e)
    {
        if (_disposed) return;

        _logger.LogError(e.Exception, "RabbitMQ connection callback exception");
        await HandleConnectionFailureAsync();
    }

    private Task ConnectionBlockedAsync(object sender, ConnectionBlockedEventArgs e)
    {
        if (!_disposed)
        {
            _logger.LogWarning("RabbitMQ connection blocked: {Reason}", e.Reason);
        }
        return Task.CompletedTask;
    }

    private Task ConnectionUnblockedAsync(object sender, AsyncEventArgs e)
    {
        if (!_disposed)
        {
            _logger.LogInformation("RabbitMQ connection unblocked");
        }
        return Task.CompletedTask;
    }

    #endregion
}

