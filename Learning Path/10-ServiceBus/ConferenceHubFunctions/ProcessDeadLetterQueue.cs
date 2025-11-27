using System.Text;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace ConferenceHubFunctions
{
    public class ProcessDeadLetterQueue
    {
        private readonly ILogger<ProcessDeadLetterQueue> _logger;

        public ProcessDeadLetterQueue(ILogger<ProcessDeadLetterQueue> logger)
        {
            _logger = logger;
        }

        [Function("ProcessDeadLetterQueue")]
        public async Task Run(
            [ServiceBusTrigger("registration-queue/$DeadLetterQueue", Connection = "ServiceBusConnectionString")] ServiceBusReceivedMessage message,
            ServiceBusMessageActions messageActions)
        {
            _logger.LogWarning("Processing dead letter message: {MessageId}", message.MessageId);
            _logger.LogWarning("Dead letter reason: {Reason}", message.DeadLetterReason);
            _logger.LogWarning("Dead letter description: {Description}", message.DeadLetterErrorDescription);

            var messageBody = Encoding.UTF8.GetString(message.Body);
            _logger.LogWarning("Message body: {Body}", messageBody);

            // TODO: Log to monitoring system
            // TODO: Send alert to operations team
            // TODO: Store for manual review

            await messageActions.CompleteMessageAsync(message);
        }
    }
}
