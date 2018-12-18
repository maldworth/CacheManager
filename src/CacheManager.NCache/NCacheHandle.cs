using System;
using System.Collections.Generic;
using System.Linq;
using Alachisoft.NCache.Runtime;
using Alachisoft.NCache.Web.Caching;
using Alachisoft.NCache.Runtime.Events;
using CacheManager.Core;
using CacheManager.Core.Internal;
using CacheManager.Core.Logging;
using static CacheManager.Core.Utility.Guard;

namespace CacheManager.NCache
{
    /// <summary>
    /// Cache handle implementation for NCache.
    /// </summary>
    /// <typeparam name="TCacheValue">The type of the cache value.</typeparam>
    [RequiresSerializer]
    public class NCacheHandle<TCacheValue> : BaseCacheHandle<TCacheValue>
    {
        private static readonly TimeSpan MinimumExpirationTimeout = TimeSpan.FromMilliseconds(1);
        private Cache _cache { get; set; }
        private readonly ICacheManagerConfiguration _managerConfiguration;
        private bool _disposed = false;
        /// <summary>
        /// Initializes a new instance of the <see cref="NCacheHandle{TCacheValue}"/> class.
        /// </summary>
        /// <param name="cache">The NCache instance.</param>
        /// <param name="managerConfiguration">The manager configuration.</param>
        /// <param name="configuration">The cache handle configuration.</param>
        /// <param name="loggerFactory">The logger factory.</param>
        /// <param name="serializer">The serializer.</param>
        public NCacheHandle(Cache cache, ICacheManagerConfiguration managerConfiguration, CacheHandleConfiguration configuration, ILoggerFactory loggerFactory, ICacheSerializer serializer)
            : base(managerConfiguration, configuration)
        {
            NotNull(loggerFactory, nameof(loggerFactory));
            NotNull(managerConfiguration, nameof(managerConfiguration));
            NotNull(configuration, nameof(configuration));
            EnsureNotNull(serializer, "A serializer is required for the redis cache handle");

            Logger = loggerFactory.CreateLogger(this);
            _managerConfiguration = managerConfiguration;

            _cache = cache;
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting
        /// unmanaged resources.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                _cache?.Dispose();
                _cache = null;
            }

            _disposed = true;
            base.Dispose();
        }

        /// <summary>
        /// Gets the number of items the cache handle currently maintains.
        /// </summary>
        /// <value>The count.</value>
        /// <exception cref="System.InvalidOperationException">No active master found.</exception>
        public override int Count => (int)_cache.Count;

        /// <summary>
        /// Gets the logger.
        /// </summary>
        /// <value>The logger instance.</value>
        protected override ILogger Logger { get; }

        /// <summary>
        /// 
        /// </summary>
        public override void Clear()
        {
            _cache.Clear();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="region"></param>
        public override void ClearRegion(string region)
        {
            _cache.RemoveGroupData(region, null);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public override bool Exists(string key)
        {
            return _cache.Contains(key);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="region"></param>
        /// <returns></returns>
        public override bool Exists(string key, string region)
        {
            return _cache.GetGroupData(region, null).Contains(key);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        protected override bool AddInternalPrepared(CacheItem<TCacheValue> item)
        {
            var fullKey = GetKey(item);

            if (_cache.Contains(fullKey))
            {
                return false;
            }

            var nCacheItem = GetNCacheItem(item);

            //nCacheItem.SetCacheDataNotification(OnCacheDataModification, EventType.ItemAdded | EventType.ItemUpdated | EventType.ItemRemoved, EventDataFilter.Metadata);
            try
            {
                _cache.Add(fullKey, nCacheItem);
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Couldn't Add Key {fullKey}");
                return false;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        protected override CacheItem<TCacheValue> GetCacheItemInternal(string key) => GetCacheItemInternal(key, null);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="region"></param>
        /// <returns></returns>
        protected override CacheItem<TCacheValue> GetCacheItemInternal(string key, string region)
        {
            var fullKey = GetKey(key, region);
            var item = _cache.Get(fullKey) as CacheItem<TCacheValue>;

            // Make sure we are subscribed to this one
            //_cache.RegisterCacheNotification(fullKey, OnCacheDataModification, EventType.ItemAdded | EventType.ItemUpdated | EventType.ItemRemoved, EventDataFilter.DataWithMetadata);

            // maybe the item is already expired because MemoryCache implements a default interval
            // of 20 seconds! to check for expired items on each store, we do it on access to also
            // reflect smaller time frames especially for sliding expiration...
            // cache.Get eventually triggers eviction callback, but just in case...
            //if (item.IsExpired)
            //{
            //    RemoveInternal(item.Key, item.Region);
            //    TriggerCacheSpecificRemove(item.Key, item.Region, CacheItemRemovedReason.Expired, item.Value);
            //    return null;
            //}

            //if (item.ExpirationMode == ExpirationMode.Sliding)
            //{
            //    // because we don't use UpdateCallback because of some multithreading issues lets
            //    // try to simply reset the item by setting it again.
            //    // item = this.GetItemExpiration(item); // done via base cache handle
            //    item.ExpirationTimeout
            //    Cache.Insert(fullKey, item);
            //}

            return item;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="item"></param>
        protected override void PutInternalPrepared(CacheItem<TCacheValue> item)
        {
            var fullkey = GetKey(item);
            var nCacheItem = GetNCacheItem(item);
            _cache.Insert(fullkey, nCacheItem);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        protected override bool RemoveInternal(string key) => RemoveInternal(key, null);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="region"></param>
        /// <returns></returns>
        protected override bool RemoveInternal(string key, string region)
        {
            var fullKey = GetKey(key, region);

            var obj = _cache.Remove(fullKey);

            return obj != null;
        }

        private string GetKey(CacheItem<TCacheValue> item) => GetKey(item?.Key, item?.Region);

        private string GetKey(string key, string region = null)
        {
            NotNullOrWhiteSpace(key, nameof(key));

            // key without region
            // key
            // key with region
            // <region>@<keystring>

            var fullKey = key;

            if (!string.IsNullOrWhiteSpace(region))
            {
                fullKey = string.Concat(region, "@", key);
            }

            return fullKey;
        }

        private CacheItem GetNCacheItem(CacheItem<TCacheValue> item)
        {
            var cacheItem = new CacheItem(item)
            {
                Priority = CacheItemPriority.Default,
            };

            if (item.ExpirationMode == ExpirationMode.Absolute)
            {
                cacheItem.AbsoluteExpiration = DateTime.UtcNow.Add(item.ExpirationTimeout);
            }

            if (item.ExpirationMode == ExpirationMode.Sliding)
            {
                cacheItem.SlidingExpiration = item.ExpirationTimeout;
            }

            //cacheItem.LastModifiedTime = DateTime.UtcNow;
            if (!string.IsNullOrWhiteSpace(item.Region))
                cacheItem.Group = item.Region;

            return cacheItem;
        }

        //private void OnCacheDataModification(string key, CacheEventArg args)
        //{
        //    switch (args.EventType)
        //    {
        //        case EventType.ItemUpdated:
        //            // 'key' has been updated in the cache
        //            // Get the updated product
        //            if (args.Item != null)
        //            {
        //                //var updatedProduct = args.Item.Value as TCacheValue;
        //                // Perform operations
        //            }
        //            break;

        //        case EventType.ItemRemoved:
        //            Core.Internal.CacheItemRemovedReason removedReason;
        //            switch (args.CacheItemRemovedReason)
        //            {
        //                case Alachisoft.NCache.Web.Caching.CacheItemRemovedReason.Expired:
        //                    removedReason = Core.Internal.CacheItemRemovedReason.Expired;
        //                    break;
        //                case Alachisoft.NCache.Web.Caching.CacheItemRemovedReason.Removed:
        //                    removedReason = Core.Internal.CacheItemRemovedReason.ExternalDelete;
        //                    break;
        //                case Alachisoft.NCache.Web.Caching.CacheItemRemovedReason.Underused:
        //                    removedReason = Core.Internal.CacheItemRemovedReason.Evicted;
        //                    break;
        //                default:
        //                    throw new Exception($"Removed for unknown reason {args.CacheItemRemovedReason}");
        //            }
        //            var item = args.Item.Value as CacheItem<TCacheValue>;
        //            // 'key' has been removed from the cache
                    
        //            TriggerCacheSpecificRemove(key, null, removedReason, item.Value);
        //            break;
        //    }
        //}

        //private static void ValidateExpirationTimeout(CacheItem<TCacheValue> item)
        //{
        //    if ((item.ExpirationMode == ExpirationMode.Absolute || item.ExpirationMode == ExpirationMode.Sliding) && item.ExpirationTimeout < MinimumExpirationTimeout)
        //    {
        //        throw new ArgumentException("Timeout lower than one millisecond is not supported.", nameof(item.ExpirationTimeout));
        //    }
        //}
    }
}
