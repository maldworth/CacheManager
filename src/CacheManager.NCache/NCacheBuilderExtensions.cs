using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Alachisoft.NCache.Web.Caching;
using CacheManager.Core;
using static CacheManager.Core.Utility.Guard;

namespace CacheManager.NCache
{
    /// <summary>
    /// Extensions for the configuration builder specific to System.Runtime.Caching cache handle.
    /// </summary>
    public static class NCacheBuilderExtensions
    {
        /// <summary>
        /// Adds a <see cref="MemoryCacheHandle{TCacheValue}" /> using a <see cref="System.Runtime.Caching.MemoryCache"/>.
        /// The name of the cache instance will be 'default'.
        /// </summary>
        /// <param name="part">The builder part.</param>
        /// <param name="cacheId">Set this to true if this cache handle should be the source of the backplane.
        /// This setting will be ignored if no backplane is configured.</param>
        /// <returns>
        /// The builder part.
        /// </returns>
        /// <returns>The builder part.</returns>
        public static ConfigurationBuilderCacheHandlePart WithNCacheHandle(this ConfigurationBuilderCachePart part, string cacheId, bool isBackplaneSource = false)
            => part?.WithNCacheHandle(Alachisoft.NCache.Web.Caching.NCache.InitializeCache(cacheId), isBackplaneSource);

        public static ConfigurationBuilderCacheHandlePart WithNCacheHandle(this ConfigurationBuilderCachePart part, Cache cache, bool isBackplaneSource = false)
            => part?.WithHandle(typeof(NCacheHandle<>), cache.ToString(), isBackplaneSource, cache);

        public static ConfigurationBuilderCachePart WithNCacheBackplane(this ConfigurationBuilderCachePart part, string cacheId)
        {
            NotNull(part, nameof(part));

            return part.WithBackplane(typeof(NCacheBackplane), cacheId, Alachisoft.NCache.Web.Caching.NCache.InitializeCache(cacheId));
        }

        public static ConfigurationBuilderCachePart WithNCacheBackplane(this ConfigurationBuilderCachePart part, Cache cache)
        {
            NotNull(part, nameof(part));

            return part.WithBackplane(typeof(NCacheBackplane), cache.ToString(), cache);
        }
    }
}
