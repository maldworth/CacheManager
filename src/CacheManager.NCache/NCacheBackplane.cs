using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Alachisoft.NCache.Web.Caching;
using Alachisoft.NCache.Runtime.Events;
using CacheManager.Core;
using CacheManager.Core.Internal;
using CacheManager.Core.Logging;
using static CacheManager.Core.Utility.Guard;
using Alachisoft.NCache.Runtime;
using Alachisoft.NCache.Runtime.Caching;

namespace CacheManager.NCache
{
    public sealed class NCacheBackplane : CacheBackplane
    {
        private readonly ILogger _logger;
        private readonly byte[] _identifier;

        private object _publishLock = new object();
        private bool _disposed = false;

        private Cache _cache;
        private ITopic _backplaneTopic;
        private ITopicSubscription _backplaneTopicSubscription;

        public NCacheBackplane(ICacheManagerConfiguration configuration, ILoggerFactory loggerFactory, Cache cache)
            : base(configuration)
        {
            NotNull(configuration, nameof(configuration));
            NotNull(loggerFactory, nameof(loggerFactory));

            _logger = loggerFactory.CreateLogger(this);

            _identifier = Encoding.UTF8.GetBytes(Guid.NewGuid().ToString());
            _cache = cache;

            // Register Filter of None for the Add and Removed
            _cache.RegisterCacheNotification(OnCacheDataModification, EventType.ItemAdded | EventType.ItemRemoved, EventDataFilter.None);
            _cache.CacheCleared += CacheCleared;

            // Because There's two types of updates,  Update or Put, we have to use the PubSub NCache to determine what kind of Update or Put to Trigger
            _backplaneTopic = _cache.MessagingService.GetTopic("cacheManagerBackplane") ?? _cache.MessagingService.CreateTopic("cacheManagerBackplane");
            _backplaneTopicSubscription = _backplaneTopic.CreateSubscription(MessageReceived);
        }

        protected override void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                _backplaneTopicSubscription?.UnSubscribe();
                _backplaneTopic?.Dispose();
                _cache?.Dispose();
                _backplaneTopicSubscription = null;
                _backplaneTopic = null;
                _cache = null;
            }

            _disposed = true;
            base.Dispose();
        }

        public override void NotifyChange(string key, CacheItemChangedEventAction action)
        {
            PublishMessage(BackplaneMessage.ForChanged(_identifier, key, action));
        }

        public override void NotifyChange(string key, string region, CacheItemChangedEventAction action)
        {
            PublishMessage(BackplaneMessage.ForChanged(_identifier, key, region, action));
        }

        public override void NotifyClear()
        {
            _logger.LogTrace("Skip Clear, in favor of NCache Event.");
        }

        public override void NotifyClearRegion(string region)
        {
            _logger.LogTrace("Skip ClearRegion[{0}], in favor of NCache Event.", region);
        }

        public override void NotifyRemove(string key)
        {
            _logger.LogTrace("Skip Remove[{0}], in favor of NCache Event.", key);
        }

        public override void NotifyRemove(string key, string region)
        {
            _logger.LogTrace("Skip Remove[{0},{1}], in favor of NCache Event.", key, region);
        }

        private void MessageReceived(object sender, MessageEventArgs args)
        {
            // Perform operations

            if (args.Message.Payload is byte[] messageData)
            {
                // Perform operations
                var messages = BackplaneMessage.Deserialize(messageData, _identifier);

                foreach (var message in messages)
                {
                    if(message.Action == BackplaneAction.Changed)
                    {
                        if (string.IsNullOrWhiteSpace(message.Region))
                        {
                            TriggerChanged(message.Key, message.ChangeAction);
                        }
                        else
                        {
                            TriggerChanged(message.Key, message.Region, message.ChangeAction);
                        }
                    }
                }
            }
            else
            {
                // Messaage failed to receive
            }
        }

        private void Publish(Message message)
        {
            _backplaneTopic.Publish(message, DeliveryOption.All);
        }

        private void PublishMessage(BackplaneMessage message)
        {
            if (message.Action != BackplaneAction.Changed) // All other backplane messages are handled through NCache's own internal event notification system
                return;

            lock (_publishLock)
            {
                if (message.ChangeAction == CacheItemChangedEventAction.Put || message.ChangeAction == CacheItemChangedEventAction.Update)
                {
                    var msg = new Message(BackplaneMessage.Serialize(message), TimeSpan.FromSeconds(5));
                    _backplaneTopic.Publish(msg, DeliveryOption.All);
                }
            }
        }



        private void CacheCleared()
        {
            TriggerCleared();
        }

        private void OnCacheDataModification(string key, CacheEventArg args)
        {
            var regionEndIndex = key.IndexOf('@');

            string region = null;

            if(regionEndIndex > 0 && regionEndIndex < (key.Length - 1)) // If the @ is at the beginning or the end of the string, then it is not the region
            {
                region = key.Substring(0, regionEndIndex);
                key = key.Substring(regionEndIndex + 1);
            }
            
            switch (args.EventType)
            {
                case EventType.ItemAdded:
                    if(region == null)
                        TriggerChanged(key, CacheItemChangedEventAction.Add);
                    else
                        TriggerChanged(key, region, CacheItemChangedEventAction.Add);
                    break;
                case EventType.ItemRemoved:
                    // 'key' has been removed from the cache

                    if (region == null)
                        TriggerRemoved(key);
                    else
                        TriggerRemoved(key, region);
                    break;
            }
        }
    }
}
