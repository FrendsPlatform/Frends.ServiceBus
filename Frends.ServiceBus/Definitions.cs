using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Frends.ServiceBus
{
    /// <summary>
    /// The encoding to use with the message contents
    /// </summary>
    public enum MessageEncoding
    {
        /// <summary>
        /// UTF-8
        /// </summary>
        UTF8,
        /// <summary>
        /// UTF-32
        /// </summary>
        UTF32,
        /// <summary>
        /// ASCII
        /// </summary>
        ASCII,
        /// <summary>
        /// Unicode
        /// </summary>
        Unicode,
        /// <summary>
        /// Other, specified below
        /// </summary>
        Other
    }

    /// <summary>
    /// How the body of the message should be serialized
    /// </summary>
    public enum BodySerializationType
    {
        /// <summary>
        /// As a stream
        /// </summary>
        Stream,
        /// <summary>
        /// As a byte array
        /// </summary>
        ByteArray,
        /// <summary>
        /// As a string
        /// </summary>
        String
    }

    /// <summary>
    /// Input parameters for the Frends.ServiceBus.Send task
    /// </summary>
    public class SendInput
    {
        /// <summary>
        /// Data to send
        /// </summary>
        public string Data { get; set; }

        /// <summary>
        /// Name of the queue or topic
        /// </summary>
        [DefaultValue("\"my-queue\"")]
        public string QueueOrTopicName { get; set; }

        /// <summary>
        /// ServiceBus connection string
        /// </summary>
        [DefaultValue("\"Endpoint=sb://[namespace].servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=[secret]\"")]
        public string ConnectionString { get; set; }

        /// <summary>
        /// Custom properties for message
        /// </summary>
        public MessageProperty[] Properties { get; set; }
    }

    /// <summary>
    /// A single custom property for a service bus message
    /// </summary>
    public class MessageProperty
    {
        /// <summary>
        /// The name of the service bus message custom property
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// The value of the service bus message custom property
        /// </summary>
        public object Value { get; set; }
    }

    /// <summary>
    /// Option parameters for the Frends.ServiceBus.Send task
    /// </summary>
    public class SendOptions
    {
        /// <summary>
        /// How should the body of the message be serialized
        /// </summary>
        [DefaultValue(BodySerializationType.Stream)]
        public BodySerializationType BodySerializationType { get; set; }

        /// <summary>
        /// Content type of the service bus message, e.g. text/plain; charset=UTF-8
        /// </summary>
        [DefaultValue("\"text/plain; charset=UTF-8\"")]
        public string ContentType { get; set; }

        /// <summary>
        /// Message id, can be used to detect duplicate messages. A new guid is generated as the value if left empty or null
        /// </summary>
        [DefaultValue("")]
        public string MessageId { get; set; }

        /// <summary>
        /// Message's session id, if set messages can be filtered according to the session id. Also the messages are handled on the same service bus broker ensuring delivery order.
        /// </summary>
        public string SessionId { get; set; }

        /// <summary>
        /// The correlation id for the message, can be used when filtering messages for subscriptions
        /// </summary>
        public string CorrelationId { get; set; }
        /// <summary>
        /// The reply to field for the message, can be used to specify the name
        /// of the queue where the receiver of the message should reply to
        /// </summary>
        public string ReplyTo { get; set; }

        /// <summary>
        /// The reply to session id field for the message, can be used to specify the session id for a reply message
        /// </summary>
        public string ReplyToSessionId { get; set; }

        /// <summary>
        /// The intended recipient of the message
        /// </summary>
        [DefaultValue("")]
        public string To { get; set; }

        /// <summary>
        /// The time in seconds the message is kept in the queue before being discarded or deadlettered
        /// </summary>
        public long? TimeToLiveSeconds { get; set; }

        /// <summary>
        /// The UTC time when the message should be added to the queue, can be used to delay the delivery of a message
        /// </summary>
        [DefaultValue("DateTime.MinValue")]
        public DateTime ScheduledEnqueueTimeUtc { get; set; }

        /// <summary>
        /// Should the queue or topic be created if it does not already exist. Elements created by this are automatically deleted once they're idle for 7 days
        /// </summary>
        [DefaultValue(false)]
        public bool CreateQueueOrTopicIfItDoesNotExist { get; set; }

        /// <summary>
        /// Is the destination a queue or a topic, used when creating a non-existant destination
        /// </summary>
        [UIHint(nameof(CreateQueueOrTopicIfItDoesNotExist), "", true)]
        [DefaultValue(QueueOrTopic.Queue)]
        public QueueOrTopic DestinationType { get; set; }

        /// <summary>
        /// Should the service bus connection (MessagingFactory) be cached. This speeds up the execution as creating a new connection is slow by keeping the connection open in the background, a single namespace is limited to 1000 concurrent connections
        /// </summary>
        [DefaultValue(true)]
        public bool UseCachedConnection { get; set; }

        /// <summary>
        /// Timeout in seconds
        /// </summary>
        [DefaultValue(60)]
        public long TimeoutSeconds { get; set; }
    }

    /// <summary>
    /// Return object for the Frends.ServiceBus.Send task
    /// </summary>
    public class SendResult
    {
        /// <summary>
        /// The message id used for the message
        /// </summary>
        public string MessageId { get; set; }
        /// <summary>
        /// The session id used for the message
        /// </summary>
        public string SessionId { get; set; }
        /// <summary>
        /// The content type used for the message
        /// </summary>
        public string ContentType { get; set; }
    }

    /// <summary>
    /// Is the message destination a queue or a topic
    /// </summary>
    public enum QueueOrTopic
    {
        /// <summary>
        /// A queue
        /// </summary>
        Queue,
        /// <summary>
        /// A topic
        /// </summary>
        Topic
    }

    /// <summary>
    /// Is the message source a queue or a subscription
    /// </summary>
    public enum QueueOrSubscription
    {
        /// <summary>
        /// A queue
        /// </summary>
        Queue,
        /// <summary>
        /// A subscription
        /// </summary>
        Subscription
    }

    /// <summary>
    /// Input for Frends.ServiceBus.Read
    /// </summary>
    public class ReadInput
    {
        /// <summary>
        /// The name of the queue or topic
        /// </summary>
        public string QueueOrTopicName { get; set; }
        /// <summary>
        /// Is the message source a queue or a subscription
        /// </summary>
        [DefaultValue(QueueOrSubscription.Queue)]
        public QueueOrSubscription SourceType { get; set; }
        /// <summary>
        /// The name of the subscription
        /// </summary>
        [UIHint(nameof(SourceType), "", QueueOrSubscription.Subscription)]
        public string SubscriptionName { get; set; }
        /// <summary>
        /// ServiceBus connection string
        /// </summary>
        [DefaultValue("\"Endpoint=sb://[namespace].servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=[secret]\"")]
        public string ConnectionString { get; set; }



    }

    /// <summary>
    /// Options for the Frends.ServiceBus.Read task
    /// </summary>
    public class ReadOptions
    {
        /// <summary>
        /// TimeoutSeconds in seconds
        /// </summary>
        [DefaultValue("60")]
        public int TimeoutSeconds { get; set; }
        /// <summary>
        /// Should the service bus connection (MessagingFactory) be cached. This speeds up the execution as creating a new connection is slow by keeping the connection open in the background, a single namespace is limited to 1000 concurrent connections
        /// </summary>
        [DefaultValue(true)]
        public bool UseCachedConnection { get; set; }
        /// <summary>
        /// How the body is expected to be serialized
        /// </summary>
        [DefaultValue(BodySerializationType.Stream)]
        public BodySerializationType BodySerializationType { get; set; }
        /// <summary>
        /// What encoding the message contents is expected to be in
        /// </summary>
        [DefaultValue(MessageEncoding.UTF8)]
        public MessageEncoding DefaultEncoding { get; set; }
        /// <summary>
        /// The name of the encoding for the message contents
        /// </summary>
        [DefaultValue("\"UTF-8\"")]
        [UIHint(nameof(DefaultEncoding), "", MessageEncoding.Other)]
        public string EncodingName { get; set; }
        /// <summary>
        /// Should the existence of the message source be checked and created if it does not exist. Elements created by this are automatically deleted once they're idle for 7 days
        /// </summary>
        [DefaultValue(false)]
        public bool CreateQueueOrSubscriptionIfItDoesNotExist { get; set; }
    }

    /// <summary>
    /// The result object for the Frends.ServiceBus.Read task
    /// </summary>
    public class ReadResult
    {
        /// <summary>
        /// Did the service bus provide a message
        /// </summary>
        public bool ReceivedMessage { get; set; }
        /// <summary>
        /// The body contents of the message
        /// </summary>
        public string Content { get; set; }
        /// <summary>
        /// The content type header of the received message
        /// </summary>
        public string ContentType { get; set; }
        /// <summary>
        /// The session id of the received message
        /// </summary>
        public string SessionId { get; set; }
        /// <summary>
        /// The message id of the received message
        /// </summary>
        public string MessageId { get; set; }
        /// <summary>
        /// The correlation id of the received message
        /// </summary>
        public string CorrelationId { get; set; }
        /// <summary>
        /// The delivery count of the received message
        /// </summary>
        public int DeliveryCount { get; set; }
        /// <summary>
        /// The enqueued sequence number of the received message
        /// </summary>
        public long EnqueuedSequenceNumber { get; set; }
        /// <summary>
        /// The sequence number of the received message
        /// </summary>
        public long SequenceNumber { get; set; }

        /// <summary>
        /// The label of the received message
        /// </summary>
        public string Label { get; set; }
        /// <summary>
        /// The custom properties of the received message
        /// </summary>
        public Dictionary<string, object> Properties { get; set; }
        /// <summary>
        /// The reply to address of the received message
        /// </summary>
        public string ReplyTo { get; set; }
        /// <summary>
        /// The reply to session id of the received message
        /// </summary>
        public string ReplyToSessionId { get; set; }
        /// <summary>
        /// The size of the received message
        /// </summary>
        public long Size { get; set; }
        /// <summary>
        /// The state of the received message
        /// </summary>
        public string State { get; set; }
        /// <summary>
        /// The to field of the received message
        /// </summary>
        public string To { get; set; }
        /// <summary>
        /// The time when the message was scheduled to be queued
        /// </summary>
        public DateTime ScheduledEnqueueTimeUtc { get; set; }
    }
}
