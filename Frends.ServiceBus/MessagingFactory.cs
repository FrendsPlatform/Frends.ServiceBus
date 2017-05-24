using Microsoft.ServiceBus;
using Microsoft.ServiceBus.Messaging;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;

namespace Frends.ServiceBus
{
    /// <summary>
    /// Class handles clients for the service bus. Enables cached connections to the service bus.
    /// </summary>
    public sealed class ServiceBusMessagingFactory : IDisposable
    {
        private static readonly Lazy<ServiceBusMessagingFactory> instanceHolder = new Lazy<ServiceBusMessagingFactory>(() => new ServiceBusMessagingFactory());
        /// <summary>
        /// The ServiceBusMessagingFactory singleton instance
        /// </summary>
        public static ServiceBusMessagingFactory Instance
        {
            get { return instanceHolder.Value; }
        }

        private static readonly object factoryLock = new Object();
        private static readonly ConcurrentDictionary<string, MessagingFactory> _messagingFactories = new ConcurrentDictionary<string, MessagingFactory>();

        private ServiceBusMessagingFactory()
        {

        }


        /// <summary>
        /// Create message receiver for the given connection string and entity path
        /// </summary>
        /// <param name="connectionString">Connection string</param>
        /// <param name="path">Name of the queue</param>
        /// <param name="timeout">TimeoutSeconds</param>
        /// <returns></returns>
        public MessageReceiver GetMessageReceiver(string connectionString, string path, TimeSpan timeout)
        {
            return GetCachedMessagingFactory(connectionString, path, timeout).CreateMessageReceiver(path, ReceiveMode.ReceiveAndDelete);
        }

        /// <summary>
        /// Create a message sender for the given connection string and entity path
        /// </summary>
        /// <param name="connectionString"></param>
        /// <param name="path"></param>
        /// <param name="timeout"></param>
        /// <returns></returns>
        public MessageSender GetMessageSender(string connectionString, string path, TimeSpan timeout)
        {
            return GetCachedMessagingFactory(connectionString, path, timeout).CreateMessageSender(path);
        }

        private MessagingFactory GetCachedMessagingFactory(string connectionString, string queueName, TimeSpan timeout)
        {
            string key = timeout.TotalSeconds + "-" + connectionString;

            if (!_messagingFactories.ContainsKey(key))
            {
                lock (factoryLock) // TODO: change double check
                {
                    if (!_messagingFactories.ContainsKey(key))
                    {
                        _messagingFactories.TryAdd(key, CreateMessagingFactoryWithTimeout(connectionString, timeout));
                    }
                }
            }

            return _messagingFactories[key];
        }

        /// <summary>
        /// Create new client for servicebus connection. This method is slow!
        /// </summary>
        /// <param name="connectionString">Connection string</param>
        /// <param name="operationTimeoutForClients">Operation timeout for clients</param>
        /// <returns>Object that can handle messaging to the service bus</returns>
        internal static MessagingFactory CreateMessagingFactoryWithTimeout(string connectionString, TimeSpan operationTimeoutForClients)
        {
            var connBuilder = new ServiceBusConnectionStringBuilder(connectionString);
            var factorySettings = new MessagingFactorySettings
            {
                OperationTimeout = operationTimeoutForClients
            };

            if (connBuilder.SharedAccessKey != null)
            {
                factorySettings.TokenProvider = TokenProvider.CreateSharedAccessSignatureTokenProvider(connBuilder.SharedAccessKeyName, connBuilder.SharedAccessKey);
            }
            else if (connBuilder.SharedSecretIssuerName != null)
            {
                factorySettings.TokenProvider = TokenProvider.CreateSharedSecretTokenProvider(connBuilder.SharedSecretIssuerName, connBuilder.SharedSecretIssuerSecret);
            }
            else if (connBuilder.OAuthUsername != null)
            {
                if (!string.IsNullOrEmpty(connBuilder.OAuthDomain))
                {
                    TokenProvider.CreateOAuthTokenProvider(connBuilder.StsEndpoints,
                        new NetworkCredential(connBuilder.OAuthUsername,
                            connBuilder.OAuthPassword,
                            connBuilder.OAuthDomain));
                }
                else
                {
                    TokenProvider.CreateOAuthTokenProvider(connBuilder.StsEndpoints,
                        new NetworkCredential(connBuilder.OAuthUsername,
                            connBuilder.OAuthPassword));
                }
            }
            else if (connBuilder.StsEndpoints.Count > 0)
            {
                factorySettings.TokenProvider = TokenProvider.CreateWindowsTokenProvider(connBuilder.StsEndpoints);
            }


            factorySettings.EnableAdditionalClientTimeout = true;

            MessagingFactory messageFactory = MessagingFactory.Create(connBuilder.GetAbsoluteRuntimeEndpoints(), factorySettings);
            messageFactory.RetryPolicy = RetryPolicy.Default;

            return messageFactory;
        }


        #region IDisposable Support
        private bool _disposedValue = false; // To detect redundant calls

        private void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                var factoriesToClose = _messagingFactories.ToList();
                _messagingFactories.Clear();

                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                }
                foreach (var item in factoriesToClose)
                {
                    try
                    {
                        item.Value.Close();
                    }
                    catch (Exception) { }

                }
                
                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                _disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        /// <summary>Allows an object to try to free resources and perform other cleanup operations before it is reclaimed by garbage collection.</summary>
        ~ServiceBusMessagingFactory()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(false);
        }

        /// <summary>
        /// Dispose of the MessagingFactory and close all the cached connections
        /// </summary>
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
             GC.SuppressFinalize(this);
        }
        #endregion

    }
}
