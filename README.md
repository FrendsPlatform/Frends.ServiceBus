- [Frends.ServiceBus](#frendsservicebus)
   - [Installing](#installing)
   - [Building](#building)
   - [Contributing](#contributing)
   - [Documentation](#documentation)
     - [ServiceBus.Send](#servicebussend) 
     - [ServiceBus.Read](#servicebusread)
   - [License](#license)
   
# Frends.ServiceBus
FRENDS Service Bus tasks.

## Installing
You can install the task via FRENDS UI Task view, by searching for packages. You can also download the latest NuGet package from https://www.myget.org/feed/frends/package/nuget/Frends.ServiceBus and import it manually via the Task view.

## Building

Clone a copy of the repo

`git clone https://github.com/FrendsPlatform/Frends.ServiceBus.git`

Restore dependencies

`nuget restore frends.servicebus`

Rebuild the project

Run Tests with nunit3. Tests can be found under

`Frends.ServiceBus.Tests\bin\Release\Frends.ServiceBus.Tests.dll`

Create a nuget package

`nuget pack nuspec/Frends.ServiceBus.nuspec`

## Contributing
When contributing to this repository, please first discuss the change you wish to make via issue, email, or any other method with the owners of this repository before making a change.

1. Fork the repo on GitHub
2. Clone the project to your own machine
3. Commit changes to your own branch
4. Push your work back up to your fork
5. Submit a Pull request so that we can review your changes

NOTE: Be sure to merge the latest from "upstream" before making a pull request!

## Documentation
### ServiceBus.Send
Send a message to the service bus

#### Input

| Property         | Type              | Description                                                 |
| ---------------- | ----------------- | ----------------------------------------------------------- |
| Data             | string            | The message contents                                        |
| QueueOrTopicName | string            | The name of the queue or topic which to send the message to |
| ConnectionString | string            | The connection string to access the service bus             |
| Properties       | MessageProperty[] | Custom properties for the message                           |

#### Options

| Property                           | Type                            | Description                                                                                                                                        |
| ---------------------------------- | ------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------- |
| BodySerializationType              | Enum{String, ByteArray, Stream} | How the Data will be serialized inside of the message                                                                                              |
| ContentType                        | string                          | The content type header for the message, the charset will be used when serializing the data, e.g. plain/text; charset=UTF-8                        |
| MessageId                          | string                          | The message id for the message, used for detecting duplicates by the service bus, if left empty a Guid will be generated as the value              |
| SessionId                          | string                          | The session id for the message, can be used for filtering messages according to the session and ensuring order of delivery                         |
| CorrelationId                      | string                          | The correlation id for the message, can be used when filtering messages for subscriptions                                                          |
| ReplyTo                            | string                          | The reply to field for the message, can be used to specify the name of the queue where the receiver of the message should reply to                 |
| ReplyToSessionId                   | string                          | The reply to session id field for the message, can be used to specify the session id for a reply message                                           |
| TimeToLiveSeconds                  | int                             | The time in seconds the message is kept in the queue before being discarded or deadlettered                                                        |
| ScheduledEnqueueTimeUtc            | DateTime                        | The UTC time when the message should be added to the queue, can be used to delay the delivery of a message                                         |
| CreateQueueOrTopicIfItDoesNotExist | bool                            | Should the queue or topic be created if it does not already exist. Slows the execution down a bit. Elements created by this are automatically deleted once they're idle for 7 days |
| UseCachedConnection                | bool                            | Should the service bus connection (MessagingFactory) be cached. This speeds up the execution as creating a new connection is slow by keeping the connection open in the background, a single namespace is limited to 1000 concurrent connections |
| TimeoutSeconds                     | int                             | Service bus operation timeout value in seconds                                                                                                     |

#### Result
object

| Property/Method     | Type             | Description                          |
| ------------------- | ---------------- | ------------------------------------ |
| MessageId           | string           | The message id of the sent message   |
| SessionId           | string           | The session id of the sent message   |
| ContentType         | string           | The content type of the sent message |


### ServiceBus.Read
Read a message from the service bus

#### Input 

| Property         | Type                      | Description                                                         |
| ---------------- | ------------------------- | ------------------------------------------------------------------- |
| ConnectionString | string                    | The connection string to access the service bus                     |
| SourceType       | Enum{Queue, Subscription} | Is the source a queue or a subscription                             |
| QueueOrTopicName | string                    | The name of the queue or topic which to send the message to         |
| SubscriptionName | string                    | The name of the subscription if reading from a topic's subscription |

#### Options

| Property                                  | Type                                     | Description                                                                                                                                        |
| ----------------------------------------- | ---------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------- |
| TimeoutSeconds                            | int                                      | Service bus operation timeout value in seconds                                                                                                     |
| UseCachedConnection                       | bool                                     | Should the service bus connection (MessagingFactory) be cached. This speeds up the execution as creating a new connection is slow by keeping the connection open in the background, a single namespace is limited to 1000 concurrent connections |
| BodySerializationType                     | Enum{String, ByteArray, Stream}          | How the Data will be serialized inside of the message                                                                                              |
| DefaultEncoding                           | Enum{UTF8, UTF32, ASCII, Unicode, Other} | The encoding to use if the charset is not set in the content type                                                                                  |
| EncodingName                              | string                                   | The name of the default encoding if Other is chosen                                                                                                |
| CreateQueueOrSubscriptionIfItDoesNotExist | bool                                     | Should the queue or topic+subscription be created if they do not already exist. Slows the execution down a bit. Elements created by this are automatically deleted once they're idle for 7 days                                     |

#### Result
object

| Property/Method          | Type                      | Description                                          |
| ------------------------ | ------------------------- | ---------------------------------------------------- |
| ReceivedMessage          | bool                      | Was a message received from the service bus          |
| Content                  | string                    | The contents of the body of the received message     |
| ContentType              | string                    | The content type of the received message             |
| SessionId                | string                    | The session id of the received message               |
| MessageId                | string                    | The message id of the received message               |
| CorrelationId            | string                    | The correlation id of the received message           |
| DeliveryCount            | int                       | The delivery count of the received message           |
| Enqueued sequence number | long                      | The enqueued sequence number of the received message |
| SequenceNumber           | long                      | The sequence number of the received message          |
| Label                    | string                    | The label of the received message                    |
| Properties               | Dictionary<string,object> | The received message's custom properties             |
| ReplyTo                  | string                    | The reply to header of the received message          |
| Size                     | long                      | The size of the received message                     |
| State                    | string                    | The state of the received message                    |
| To                       | string                    | The to header value of the received message          |
| ScheduledEnqueueTimeUtc | DateTime   | The time when the message was scheduled to be queued|

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details
