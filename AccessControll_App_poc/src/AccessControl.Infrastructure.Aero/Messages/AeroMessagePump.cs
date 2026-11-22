using System.Threading.Channels;
using HID.Aero.ScpdNet.Wrapper;
using Microsoft.Extensions.Logging;

namespace AccessControl.Infrastructure.Aero.Messages;

/// <summary>
/// Runs the driver's message loop on a dedicated thread. The SDK requires GetMessage to be called continuously
/// or connections drop and transactions stop (SDK §2, HID samples). Replies are copied into a bounded queue;
/// when the queue is full the loop waits (the driver keeps buffering), so nothing is dropped.
/// </summary>
public sealed partial class AeroMessagePump(TimeProvider timeProvider, ILogger<AeroMessagePump> logger) : IDisposable
{
    public const int QueueCapacity = 10_000;
    private static readonly TimeSpan IdleDelay = TimeSpan.FromMilliseconds(20);
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(5);

    private readonly Channel<AeroReply> _replies = Channel.CreateBounded<AeroReply>(
        new BoundedChannelOptions(QueueCapacity)
        {
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.Wait,
        });

    private readonly CancellationTokenSource _stopping = new();
    private Thread? _thread;

    public ChannelReader<AeroReply> Replies => _replies.Reader;

    public void Start()
    {
        if (_thread is not null)
        {
            return;
        }

        _thread = new Thread(Run) { IsBackground = true, Name = "AeroMessagePump" };
        _thread.Start();
        LogStarted(logger);
    }

    public void Stop()
    {
        if (_thread is null)
        {
            return;
        }

        _stopping.Cancel();
        if (!_thread.Join(StopTimeout))
        {
            LogStopTimedOut(logger, StopTimeout.TotalSeconds);
        }

        _replies.Writer.TryComplete();
        _thread = null;
        LogStopped(logger);
    }

    public void Dispose()
    {
        Stop();
        _stopping.Dispose();
    }

    private void Run()
    {
        var token = _stopping.Token;
        while (!token.IsCancellationRequested)
        {
            try
            {
                var message = new SCPReplyMessage();
                if (message.GetMessage())
                {
                    Enqueue(AeroReplyMapper.Map(message, timeProvider.GetUtcNow()), token);

                    // Drain without sleeping while the driver has more replies.
                    continue;
                }
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                // A malformed reply must not stop the loop: without it every controller goes offline.
                LogReadFailed(logger, exception);
            }

            token.WaitHandle.WaitOne(IdleDelay);
        }
    }

    private void Enqueue(AeroReply reply, CancellationToken token)
    {
        while (!_replies.Writer.TryWrite(reply))
        {
            if (token.IsCancellationRequested)
            {
                return;
            }

            token.WaitHandle.WaitOne(IdleDelay);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Aero message pump started.")]
    private static partial void LogStarted(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Aero message pump stopped.")]
    private static partial void LogStopped(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Aero message pump did not stop within {Seconds}s.")]
    private static partial void LogStopTimedOut(ILogger logger, double seconds);

    [LoggerMessage(Level = LogLevel.Error, Message = "Reading a reply from the HID Aero driver failed; the message loop continues.")]
    private static partial void LogReadFailed(ILogger logger, Exception exception);
}
