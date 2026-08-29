using System.Threading.Channels;

namespace InternLink.Web.Services.Vectors;

public enum JobIndexOperation
{
    Upsert = 0,
    Delete = 1
}

public sealed record JobIndexCommand(Guid JobId, JobIndexOperation Operation);

public interface IJobIndexQueue
{
    /// <summary>Non-blocking by design: a web request must never wait on an embedding call.</summary>
    bool TryEnqueue(JobIndexCommand command);

    IAsyncEnumerable<JobIndexCommand> ReadAllAsync(CancellationToken ct);
}

public class JobIndexQueue : IJobIndexQueue
{
    private readonly Channel<JobIndexCommand> _channel;
    private readonly ILogger<JobIndexQueue> _logger;

    public JobIndexQueue(ILogger<JobIndexQueue> logger)
    {
        _logger = logger;
        _channel = Channel.CreateBounded<JobIndexCommand>(new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
            SingleWriter = false
        });
    }

    public bool TryEnqueue(JobIndexCommand command)
    {
        if (_channel.Writer.TryWrite(command))
        {
            return true;
        }

        // The reconcile pass (ReindexAll) is the safety net for anything dropped here.
        _logger.LogWarning(
            "Job index queue is full; dropped {Operation} for job {JobId}. Run Admin ReindexAll to reconcile.",
            command.Operation,
            command.JobId);
        return false;
    }

    public IAsyncEnumerable<JobIndexCommand> ReadAllAsync(CancellationToken ct) =>
        _channel.Reader.ReadAllAsync(ct);
}
