using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace Wyman.RabbitMQEventBus;

/// <summary>
/// RabbitMQ事件总线实现类，用于处理事件的发布和订阅。
/// </summary>
internal class RabbitMQEventBus : IEventBus, IAsyncDisposable
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly RabbitMQConnection _connection;
    private readonly IntegrationEventSubscriptionsManager _subscriptionsManager;
    private readonly ILogger<RabbitMQEventBus> _logger;
    private readonly RabbitMqOption _options;
    private readonly SemaphoreSlim _consumerSemaphore = new(1, 1);
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private IChannel? _consumerChannel;
    private readonly string _queueName;
    private bool _isStartBasicConsumer = false;
    private bool _disposed = false;

    private bool _isOpenConsumerChannel => _consumerChannel != null && !_consumerChannel.IsClosed;

    public RabbitMQEventBus(IServiceScopeFactory serviceScopeFactory, IConnectionFactory connectionFactory, string queueName, ILoggerFactory loggerFactory, RabbitMqOption options)
    {
        _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
        _connection = new RabbitMQConnection(connectionFactory, loggerFactory);
        _subscriptionsManager = new IntegrationEventSubscriptionsManager();
        _queueName = queueName;
        _logger = loggerFactory.CreateLogger<RabbitMQEventBus>() ?? throw new ArgumentNullException(nameof(loggerFactory));
        _options = options ?? throw new ArgumentNullException(nameof(options));

        ValidateOptions();
    }

    public async Task PublishAsync(string eventName, object? eventData)
    {
        ThrowIfDisposed();
        ThrowIfNull(eventName);

        try
        {
            using var channel = await CreateChannelAsync();
            await channel.ExchangeDeclareAsync(_options.ExchangeName, "direct", true, false);

            var body = eventData == null
                ? []
                : JsonSerializer.SerializeToUtf8Bytes(eventData, eventData.GetType(), new JsonSerializerOptions
                {
                    WriteIndented = false
                });

            await channel.BasicPublishAsync(_options.ExchangeName, eventName, true, body);
            _logger.LogDebug("Event '{EventName}' published successfully", eventName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish event '{EventName}'", eventName);
            throw;
        }
    }

    public async Task SubscribeAsync(string eventName, Type handlerType)
    {
        ThrowIfDisposed();
        ThrowIfNull(eventName);
        CheckHandlerType(handlerType);

        await _consumerSemaphore.WaitAsync(_cancellationTokenSource.Token);
        try
        {
            await EnsureConsumerChannelAsync();

            if (!_subscriptionsManager.HasSubscriptionsForEvent(eventName))
            {
                await _consumerChannel!.QueueBindAsync(_queueName, _options.ExchangeName, eventName);
                _logger.LogInformation("Queue '{QueueName}' bound to exchange '{ExchangeName}' with routing key '{EventName}'", _queueName, _options.ExchangeName, eventName);
            }

            _subscriptionsManager.AddSubscription(eventName, handlerType);
            _logger.LogDebug("Handler '{HandlerType}' subscribed to event '{EventName}'", handlerType.Name, eventName);

            if (!_isStartBasicConsumer)
            {
                await StartBasicConsumerAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to subscribe to event '{EventName}' with handler '{HandlerType}'", eventName, handlerType.Name);
            throw;
        }
        finally
        {
            _consumerSemaphore.Release();
        }
    }

    public async Task UnsubscribeAsync(string eventName, Type handlerType)
    {
        ThrowIfDisposed();
        ThrowIfNull(eventName);
        CheckHandlerType(handlerType);

        await _consumerSemaphore.WaitAsync(_cancellationTokenSource.Token);
        try
        {
            _subscriptionsManager.RemoveSubscription(eventName, handlerType);
            _logger.LogDebug("Handler '{HandlerType}' unsubscribed from event '{EventName}'", handlerType.Name, eventName);

            if (!_subscriptionsManager.HasSubscriptionsForEvent(eventName))
            {
                await EnsureConsumerChannelAsync();
                await _consumerChannel!.QueueUnbindAsync(_queueName, _options.ExchangeName, eventName);
                _logger.LogInformation("Queue '{QueueName}' unbound from exchange '{ExchangeName}' with routing key '{EventName}'", _queueName, _options.ExchangeName, eventName);

                if (_subscriptionsManager.IsEmpty)
                {
                    await StopBasicConsumerAsync();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unsubscribe from event '{EventName}' with handler '{HandlerType}'", eventName, handlerType.Name);
            throw;
        }
        finally
        {
            _consumerSemaphore.Release();
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
            _consumerChannel?.Dispose();
            await _connection.DisposeAsync();
            _subscriptionsManager.Dispose();
            _cancellationTokenSource.Dispose();
            _consumerSemaphore.Dispose();

            _disposed = true;
        }
    }

    #region Private Methode

    private void ValidateOptions()
    {
        ThrowIfNull(_options.HostName);
        ThrowIfNull(_options.ExchangeName);
        if (_options.PrefetchCount <= 0) throw new ArgumentException("PrefetchCount must be greater than 0.", nameof(_options.PrefetchCount));
    }

    private void ThrowIfNull(string argumentName)
    {
        if (string.IsNullOrWhiteSpace(argumentName)) throw new ArgumentNullException("Argument cannot be null or empty.", nameof(argumentName));
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(RabbitMQEventBus));
    }

    private void CheckHandlerType(Type handlerType)
    {
        if (handlerType == null) throw new ArgumentNullException(nameof(handlerType));

        if (!typeof(IIntegrationEventHandler).IsAssignableFrom(handlerType)) throw new ArgumentException($"Handler type '{handlerType.FullName}' must implement '{nameof(IIntegrationEventHandler)}'.", nameof(handlerType));
    }

    private async Task<IChannel> CreateChannelAsync()
    {
        if (!_connection.IsConnected) await _connection.TryConnectionAsync();

        var channel = await _connection.CreateChannelAsync();
        return channel;
    }

    private async Task EnsureConsumerChannelAsync()
    {
        if (_isOpenConsumerChannel) return;

        if (_isOpenConsumerChannel) return;

        _consumerChannel = await CreateChannelAsync();
        await _consumerChannel.ExchangeDeclareAsync(_options.ExchangeName, "direct", true, false);
        await _consumerChannel.QueueDeclareAsync(_queueName, _options.QueueDurable, false, false, null);

        _consumerChannel.CallbackExceptionAsync += Consumer_CallbackExceptionAsync;
        _consumerChannel.ChannelShutdownAsync += Consumer_ChannelShutdownAsync;
    }

    private async Task StartBasicConsumerAsync()
    {
        try
        {
            await _consumerChannel!.BasicQosAsync(0, _options.PrefetchCount, false);
            var consumer = new AsyncEventingBasicConsumer(_consumerChannel);
            consumer.ReceivedAsync += Consumer_ReceivedAsync;
            await _consumerChannel.BasicConsumeAsync(_queueName, false, consumer);
            _isStartBasicConsumer = true;
            _logger.LogInformation("Basic consumer started for queue '{QueueName}'", _queueName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start basic consumer for queue '{QueueName}'", _queueName);
            throw;
        }
    }

    private async Task Consumer_ReceivedAsync(object sender, BasicDeliverEventArgs eventArgs)
    {
        var eventName = eventArgs.RoutingKey;
        var eventData = Encoding.UTF8.GetString(eventArgs.Body.Span);

        _logger.LogDebug($"Received event '{eventName}' with delivery tag {eventArgs.DeliveryTag}");

        try
        {
            await HandleEventAsync(eventName, eventData);

            if (_isOpenConsumerChannel)
            {
                await _consumerChannel!.BasicAckAsync(eventArgs.DeliveryTag, false);
                _logger.LogDebug($"Event '{eventName}' processed successfully, acknowledged");
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning($"Event '{eventName}' processing timed out");
            if (_isOpenConsumerChannel)
            {
                await _consumerChannel!.BasicNackAsync(eventArgs.DeliveryTag, false, _options.RequeueOnFailure);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error handling event '{eventName}' with delivery tag {eventArgs.DeliveryTag}");
            if (_isOpenConsumerChannel)
            {
                await _consumerChannel!.BasicNackAsync(eventArgs.DeliveryTag, false, _options.RequeueOnFailure);
            }
        }
    }

    private async Task HandleEventAsync(string eventName, string eventData)
    {
        if (!_subscriptionsManager.HasSubscriptionsForEvent(eventName))
        {
            string? entryAsm = Assembly.GetEntryAssembly()?.GetName().Name;
            _logger.LogWarning($"No subscribers for event '{eventName}' in assembly '{entryAsm}'");
            return;
        }

        var handlers = _subscriptionsManager.GetHandlersForEvent(eventName);
        var tasks = new List<Task>();

        foreach (var handler in handlers)
        {
            tasks.Add(HandleEventWithHandlerAsync(eventName, eventData, handler));
        }

        await Task.WhenAll(tasks);
    }

    private async Task HandleEventWithHandlerAsync(string eventName, string eventData, Type handler)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        try
        {
            var handlerInstance = scope.ServiceProvider.GetRequiredService(handler) as IIntegrationEventHandler;
            if (handlerInstance == null)
            {
                throw new InvalidOperationException($"Handler {handler.Name} is not registered as IIntegrationEventHandler.");
            }

            await handlerInstance.HandleAsync(eventName, eventData);
            _logger.LogDebug($"Event '{eventName}' handled successfully by '{handler.Name}'");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Handler '{handler.Name}' failed to process event '{eventName}'");
            throw;
        }
    }

    private async Task StopBasicConsumerAsync()
    {
        if (_consumerChannel == null) return;

        _consumerChannel.CallbackExceptionAsync -= Consumer_CallbackExceptionAsync;
        _consumerChannel.ChannelShutdownAsync -= Consumer_ChannelShutdownAsync;

        await _consumerChannel.CloseAsync();
        _consumerChannel = null;

        _logger.LogInformation("Basic consumer stopped for queue '{QueueName}'", _queueName);
    }

    private Task Consumer_CallbackExceptionAsync(object sender, CallbackExceptionEventArgs @event)
    {
        _logger.LogError(@event.Exception, "Channel callback exception occurred");
        return Task.CompletedTask;
    }

    private Task Consumer_ChannelShutdownAsync(object sender, ShutdownEventArgs @event)
    {
        _logger.LogWarning("Consumer channel shutdown: {ShutdownInitiator}, {ShutdownReason}", @event.Initiator, @event.ReplyText);
        return Task.CompletedTask;
    }

    #endregion
}

