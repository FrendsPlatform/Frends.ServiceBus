using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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
        /// Send data to the Service Bus, don't wait for a reply. See https://github.com/FrendsPlatform/Frends.ServiceBus
        /// </summary>
        /// <returns>Object: {MessageId, SessionId, ContentType}</returns>
        public static async Task<SendResult> Send([PropertyTab]SendInput input, [PropertyTab]SendOptions options, CancellationToken cancellationToken = new CancellationToken())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (options.CreateQueueOrTopicIfItDoesNotExist)
            {
                switch (options.DestinationType)
                {
                    case QueueOrTopic.Queue:
                        await EnsureQueueExists(input.QueueOrTopicName, input.ConnectionString).ConfigureAwait(false);
                        break;
                    case QueueOrTopic.Topic:
                        await EnsureTopicExists(input.QueueOrTopicName, input.ConnectionString).ConfigureAwait(false);
                        break;
                    default:
                        throw new Exception($"Unexpected destination type: {options.DestinationType}");
                }
            }

            return await DoQueueSendOperation(input.ConnectionString, input.QueueOrTopicName, TimeSpan.FromSeconds(options.TimeoutSeconds), options.UseCachedConnection,
                async client =>
                {
                    var result = new SendResult();

                    var body = CreateBody(input.Data, options.ContentType, options.BodySerializationType);

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

        private static Task EnsureQueueExists(string queueName, string connectionString)
        {
            return EnsureQueueExists(queueName, connectionString, false);
        }

        private static async Task EnsureQueueExists(string queueName, string connectionString, bool requiresSession)
        {
            var manager = NamespaceManager.CreateFromConnectionString(connectionString);

            if (!await manager.QueueExistsAsync(queueName).ConfigureAwait(false))
            {
                var queueDescription = new QueueDescription(queueName)
                {
                    EnableBatchedOperations = true,
                    MaxSizeInMegabytes = 5 * 1024,
                    AutoDeleteOnIdle = TimeSpan.FromDays(7),
                    RequiresSession = requiresSession
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

        private static object CreateBody(string data, string contentType, BodySerializationType bodySerializationType)
        {
            if (data == null)
            {
                return null;
            }

            var contentTypeString = contentType;

            var encoding = GetEncodingFromContentType(contentTypeString, Encoding.UTF8);


            switch (bodySerializationType)
            {
                case BodySerializationType.String:
                    return data;
                case BodySerializationType.ByteArray:
                    return encoding.GetBytes(data);
                case BodySerializationType.Stream:
                default:
                    var stream = new MemoryStream(encoding.GetBytes(data)) { Position = 0 };
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
        /// Read a message from the Service Bus. See https://github.com/FrendsPlatform/Frends.ServiceBus
        /// </summary>
        /// <param name="input">Input parameters</param>
        /// <param name="options">Option parameters</param>
        /// <param name="cancellationToken"></param>
        /// <returns>Object {ReceivedMessage(boolean), ContentType, SessionId, MessageId, CorrelationId, DeliveryCount, EnqueuedSequenceNumber, SequenceNumber, Label, Properties(dictionary), ReplyTo, ReplyToSessionId, Size, State, To, Content}</returns>
        public static async Task<ReadResult> Read([PropertyTab]ReadInput input, [PropertyTab]ReadOptions options,
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
                        Content = await ReadMessageBody(msg, options.BodySerializationType, options.DefaultEncoding, options.EncodingName).ConfigureAwait(false)
                    };
                    return result;
                }).ConfigureAwait(false);
        }

        private static async Task<string> ReadMessageBody(
            BrokeredMessage msg, BodySerializationType bodySerializationType, MessageEncoding defaultEncoding, string encodingName)
        {
            // Body is a string
            if (bodySerializationType == BodySerializationType.String)
            {
                return msg.GetBody<string>();
            }

            Encoding encoding = GetEncodingFromContentType(msg.ContentType, GetEncoding(defaultEncoding, encodingName));

            // Body is a byte array
            if (bodySerializationType == BodySerializationType.ByteArray)
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

        static async public Task<ReadResult> SendAndWaitForResponse(SendAndWaitForResponseInput input, SendAndWaitForResponseOptions options, CancellationToken token = new CancellationToken())
        {
            // Prepare queues
            if (options.CreateQueues)
            {
                await EnsureQueueExists(input.Queue, input.ConnectionString, false);
                await EnsureQueueExists(input.ReplyToQueue, input.ConnectionString, true);
            }

            QueueClient requestClient = MessagingFactory.CreateFromConnectionString(input.ConnectionString).CreateQueueClient(input.Queue);
            QueueClient responseClient = MessagingFactory.CreateFromConnectionString(input.ConnectionString).CreateQueueClient(input.ReplyToQueue);
            MessageSession session = responseClient.AcceptMessageSession(input.SessionID);

            // Create and send the message
            var body = CreateBody(input.Data, options.ContentType, options.BodySerializationType);
            var bodyStream = body as Stream;
            var message = bodyStream != null ? new BrokeredMessage(bodyStream, true) : new BrokeredMessage(body);
            message.SessionId = string.IsNullOrWhiteSpace(input.SessionID) ? null : input.SessionID;
            message.ReplyToSessionId = string.IsNullOrWhiteSpace(input.SessionID) ? null : input.SessionID;
            message.MessageId = string.IsNullOrEmpty(options.MessageId) ? Guid.NewGuid().ToString() : options.MessageId;
            message.CorrelationId = options.CorrelationId;
            message.ContentType = options.ContentType;
            message.ReplyTo = input.ReplyToQueue;

            requestClient.Send(message);

            // The following two variables will be used to track if the response was already received
            ReadResult readResult = null;
            Exception error = null;

            // Start asynchronous receive operation 
            var result = session.BeginReceive(TimeSpan.FromSeconds(60), async (IAsyncResult res) =>
            {
                // We must wrap everything with a try/catch since otherwice 
                // an exception in separate thread can cause FRENDS agent to crash.
                // We will any exception in the main task thread.
                try
                {
                    var sess = res.AsyncState as MessageSession;
                    if (sess == null) return;
                    var msg = session.EndReceive(res);
                    if (msg == null)
                    {
                        return;
                    }

                    readResult = new ReadResult
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
                        Content = await ReadMessageBody(msg, options.BodySerializationType, options.DefaultEncoding, options.EncodingName)
                    };
                }
                catch (Exception ex)
                {
                    error = ex;
                }
            }, session);

            // We wait for the timeout amount of time. If either error or serviceBusResult 
            // variable are not null then that means that a result was received or an error
            // has occurred - thus no need to wait anymore.
            var currentWaitTimeMs = 0;
            while (error == null && readResult == null && currentWaitTimeMs <= (options.TimeoutSeconds * 1000))
            {
                Thread.Sleep(50);
                currentWaitTimeMs += 50;
            }

            // Clean up
            requestClient.Close();
            responseClient.Close();
            session.Close();

            if (error != null)
            {
                throw error;
            }

            if (readResult != null)
            {
                return readResult;
            }
            else
            {
                throw new TimeoutException("Did not get a reponse in session during the specified timeout time");
            }
        }
    }



}
