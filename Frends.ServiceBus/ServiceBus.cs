using System;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Frends.Tasks.Attributes;
using Microsoft.ServiceBus;
using Microsoft.ServiceBus.Messaging;

namespace Frends.ServiceBus
{
    /// <summary>
    /// FRENDS ServiceBus tasks
    /// </summary>
    public class ServiceBus
    {
        private static async Task<ReadResult> DoReadOperation(string connectionString, string path, TimeSpan timeout, bool useCached, Func<MessageReceiver, Task<ReadResult>> operation)
        {
            MessagingFactory mfactory = null;
            MessageReceiver requestClient = null;
            try
            {
                if (useCached)
                {
                    requestClient = ServiceBusMessagingFactory.Instance.GetMessageReceiver(connectionString, path, timeout);
                }
                else
                {
                    mfactory = ServiceBusMessagingFactory.CreateMessagingFactoryWithTimeout(connectionString, timeout);
                    requestClient = mfactory.CreateMessageReceiver(path);
                }
                return await operation.Invoke(requestClient).ConfigureAwait(false);
            }
            finally
            {
                requestClient?.Close();
                mfactory?.Close();
            }
        }

        private static async Task<SendResult> DoQueueSendOperation(string connectionString, string path, TimeSpan timeout, bool useCached, Func<MessageSender, Task<SendResult>> operation)
        {
            MessagingFactory mfactory = null;
            MessageSender requestClient = null;
            try
            {
                if (useCached)
                {
                    requestClient = ServiceBusMessagingFactory.Instance.GetMessageSender(connectionString, path, timeout);
                }
                else
                {
                    mfactory = ServiceBusMessagingFactory.CreateMessagingFactoryWithTimeout(connectionString, timeout);
                    requestClient = mfactory.CreateMessageSender(path);
                }
                return await operation.Invoke(requestClient).ConfigureAwait(false);
            }
            finally
            {
                requestClient?.Close();
                mfactory?.Close();
            }
        }

        private static Encoding GetEncoding(MessageEncoding encodingChoice, string encodingName)
        {
            switch (encodingChoice)
            {
                case MessageEncoding.ASCII:
                    return Encoding.ASCII;
                case MessageEncoding.UTF8:
                    return Encoding.UTF8;
                case MessageEncoding.Unicode:
                    return Encoding.Unicode;
                case MessageEncoding.UTF32:
                    return Encoding.UTF32;
                case MessageEncoding.Other:
                default:
                    return Encoding.GetEncoding(encodingName);
            }
        }

        /// <summary>
        /// Send data to the Service Bus, don't wait for a reply. See https://bitbucket.org/hiqfinland/frends.servicebus
        /// </summary>
        /// <returns>Object: {MessageId, SessionId, ContentType}</returns>
        public static async Task<SendResult> Send([CustomDisplay(DisplayOption.Tab)]SendInput input, [CustomDisplay(DisplayOption.Tab)]SendOptions options, CancellationToken cancellationToken = new CancellationToken())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (options.CreateQueueOrTopicIfItDoesNotExist)
            {
                switch (options.DestinationType)
                {
                    case QueueOrTopic.Queue:
                        await EnsureQueueExists(input.QueueOrTopicName, input.ConnectionString);
                        break;
                    case QueueOrTopic.Topic:
                        await EnsureTopicExists(input.QueueOrTopicName, input.ConnectionString);
                        break;
                    default:
                        throw new Exception($"Unexpected destination type: {options.DestinationType}");
                }
            }

            return await DoQueueSendOperation(input.ConnectionString, input.QueueOrTopicName, TimeSpan.FromSeconds(options.TimeoutSeconds), options.UseCachedConnection,
                async client =>
                {
                    var result = new SendResult();

                    var body = CreateBody(input, options);

                    var bodyStream = body as Stream;
                    using (var message = bodyStream != null ? new BrokeredMessage(bodyStream, true) : new BrokeredMessage(body))
                    {
                        message.MessageId = string.IsNullOrEmpty(options.MessageId)
                        ? Guid.NewGuid().ToString()
                        : options.MessageId;
                        result.MessageId = message.MessageId;

                        message.SessionId = string.IsNullOrEmpty(options.SessionId) ? message.SessionId : options.SessionId;
                        result.SessionId = message.SessionId;

                        message.ContentType = options.ContentType;
                        result.ContentType = message.ContentType;


                        message.CorrelationId = options.CorrelationId;
                        message.ReplyToSessionId = options.ReplyToSessionId;
                        message.ReplyTo = options.ReplyTo;
                        message.To = options.To;
                        message.TimeToLive = options.TimeToLiveSeconds.HasValue
                            ? TimeSpan.FromSeconds(options.TimeToLiveSeconds.Value)
                            : TimeSpan.MaxValue;
                        message.ScheduledEnqueueTimeUtc = options.ScheduledEnqueueTimeUtc;

                        foreach (var property in input.Properties)
                        {
                            message.Properties.Add(property.Name, property.Value);
                        }

                        cancellationToken.ThrowIfCancellationRequested();

                        await client.SendAsync(message).ConfigureAwait(false);

                        return result;
                    }
                }).ConfigureAwait(false);
        }

        private static async Task EnsureQueueExists(string queueName, string connectionString)
        {
            var manager = NamespaceManager.CreateFromConnectionString(connectionString);

            if (!await manager.QueueExistsAsync(queueName).ConfigureAwait(false))
            {
                var queueDescription = new QueueDescription(queueName)
                {
                    EnableBatchedOperations = true,
                    MaxSizeInMegabytes = 5 * 1024,
                    AutoDeleteOnIdle = TimeSpan.FromDays(7)
                };
                await manager.CreateQueueAsync(queueDescription).ConfigureAwait(false);
            }
        }

        private static async Task EnsureTopicExists(string topicName, string connectionString, NamespaceManager namespaceManager = null)
        {
            var manager = namespaceManager ?? NamespaceManager.CreateFromConnectionString(connectionString);

            if (!await manager.TopicExistsAsync(topicName).ConfigureAwait(false))
            {
                var topicDescription = new TopicDescription(topicName)
                {
                    EnableBatchedOperations = true,
                    MaxSizeInMegabytes = 5 * 1024,
                    AutoDeleteOnIdle = TimeSpan.FromDays(7)
                };
                await manager.CreateTopicAsync(topicDescription).ConfigureAwait(false);
            }
        }

        private static async Task EnsureSubscriptionExists(string topicName, string subscriptionName, string connectionString)
        {
            var manager = NamespaceManager.CreateFromConnectionString(connectionString);

            await EnsureTopicExists(topicName, connectionString, manager).ConfigureAwait(false);

            if (!await manager.SubscriptionExistsAsync(topicName, subscriptionName).ConfigureAwait(false))
            {
                var subscriptionDescription = new SubscriptionDescription(topicName, subscriptionName)
                {
                    EnableBatchedOperations = true,
                    AutoDeleteOnIdle = TimeSpan.FromDays(7)
                };
                await manager.CreateSubscriptionAsync(subscriptionDescription).ConfigureAwait(false);
            }
        }

        private static object CreateBody(SendInput input, SendOptions options)
        {
            if (input.Data == null)
            {
                return null;
            }

            var contentTypeString = options.ContentType;

            var encoding = GetEncodingFromContentType(contentTypeString, Encoding.UTF8);


            switch (options.BodySerializationType)
            {
                case BodySerializationType.String:
                    return input.Data;
                case BodySerializationType.ByteArray:
                    return encoding.GetBytes(input.Data);
                case BodySerializationType.Stream:
                default:
                    var stream = new MemoryStream(encoding.GetBytes(input.Data)) { Position = 0 };
                    return stream;
            }
        }

        private static Encoding GetEncodingFromContentType(string contentTypeString, Encoding defaultEncoding)
        {
            Encoding encoding = defaultEncoding;
            if (!string.IsNullOrEmpty(contentTypeString))
            {
                var contentType = new ContentType(contentTypeString);
                if (!string.IsNullOrEmpty(contentType.CharSet))
                {
                    encoding = Encoding.GetEncoding(contentType.CharSet);
                }
            }
            return encoding;
        }

        /// <summary>
        /// Read a message from the Service Bus. See https://bitbucket.org/hiqfinland/frends.servicebus
        /// </summary>
        /// <param name="input">Input parameters</param>
        /// <param name="options">Option parameters</param>
        /// <param name="cancellationToken"></param>
        /// <returns>Object {ReceivedMessage(boolean), ContentType, SessionId, MessageId, CorrelationId, DeliveryCount, EnqueuedSequenceNumber, SequenceNumber, Label, Properties(dictionary), ReplyTo, ReplyToSessionId, Size, State, To, Content}</returns>
        public static async Task<ReadResult> Read([CustomDisplay(DisplayOption.Tab)]ReadInput input, [CustomDisplay(DisplayOption.Tab)]ReadOptions options,
            CancellationToken cancellationToken = new CancellationToken())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (options.CreateQueueOrSubscriptionIfItDoesNotExist)
            {
                switch (input.SourceType)
                {
                    case QueueOrSubscription.Queue:
                        await EnsureQueueExists(input.QueueOrTopicName, input.ConnectionString);
                        break;
                    case QueueOrSubscription.Subscription:
                        await EnsureSubscriptionExists(input.QueueOrTopicName, input.SubscriptionName, input.ConnectionString);
                        break;
                    default:
                        throw new Exception($"Unexpected destination type: {input.SourceType}");
                }
            }

            var path = input.QueueOrTopicName;
            if (input.SourceType == QueueOrSubscription.Subscription)
            {
                path = $"{input.QueueOrTopicName}/subscriptions/{input.SubscriptionName}";
            }

            return await DoReadOperation(input.ConnectionString, path, TimeSpan.FromSeconds(options.TimeoutSeconds),
                options.UseCachedConnection,
                async client =>
                {
                    var msg = await client.ReceiveAsync().ConfigureAwait(false);

                    if (msg == null)
                    {
                        return new ReadResult
                        {
                            ReceivedMessage = false
                        };
                    }

                    var result = new ReadResult
                    {
                        ReceivedMessage = true,
                        ContentType = msg.ContentType,
                        Properties = msg.Properties?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                        SessionId = msg.SessionId,
                        MessageId = msg.MessageId,
                        CorrelationId = msg.CorrelationId,
                        Label = msg.Label,
                        DeliveryCount = msg.DeliveryCount,
                        EnqueuedSequenceNumber = msg.EnqueuedSequenceNumber,
                        SequenceNumber = msg.SequenceNumber,
                        ReplyTo = msg.ReplyTo,
                        ReplyToSessionId = msg.ReplyToSessionId,
                        Size = msg.Size,
                        State = msg.State.ToString(),
                        To = msg.To,
                        ScheduledEnqueueTimeUtc = msg.ScheduledEnqueueTimeUtc,
                        Content = await ReadMessageBody(msg, options).ConfigureAwait(false)
                    };
                    return result;
                }).ConfigureAwait(false);
        }

        private static async Task<string> ReadMessageBody(BrokeredMessage msg, ReadOptions options)
        {
            // Body is a string
            if (options.BodySerializationType == BodySerializationType.String)
            {
                return msg.GetBody<string>();
            }

            Encoding encoding = GetEncodingFromContentType(msg.ContentType, GetEncoding(options.DefaultEncoding, options.EncodingName));

            // Body is a byte array
            if (options.BodySerializationType == BodySerializationType.ByteArray)
            {
                var messageBytes = msg.GetBody<byte[]>();
                return messageBytes == null ? null : encoding.GetString(messageBytes);
            }


            // Body is a stream
            using (var contentStream = msg.GetBody<Stream>())
            {
                if (contentStream == null)
                {
                    return null;
                }

                using (var reader = new StreamReader(contentStream, encoding))
                {
                    return await reader.ReadToEndAsync().ConfigureAwait(false);
                }
            }
        }
    }



}
