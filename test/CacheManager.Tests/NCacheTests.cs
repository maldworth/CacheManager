using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Alachisoft.NCache.Runtime.Caching;
using Alachisoft.NCache.Web.Caching;
using CacheManager.Core;
using CacheManager.NCache;
using FluentAssertions;
using Xunit;

namespace CacheManager.Tests
{
    public class NCacheTests
    {
        //Alachisoft.NCache.Web.Caching.Cache _backplaneCache;
        //ICacheManager<object> _cache1;
        static string _cacheName = "myTestCache";
        long _messagesReceived = 0;

        //public NCacheTests()
        //{
        //    var config = new ConfigurationBuilder()
        //        .WithNCacheHandle(Alachisoft.NCache.Web.Caching.NCache.InitializeCache(_cacheName))
        //        .Build();

        //    _cache1 = new BaseCacheManager<object>(config);

        //    _backplaneCache = Alachisoft.NCache.Web.Caching.NCache.InitializeCache(_cacheName);
        //}

        //public void Dispose()
        //{
        //    Dispose(true);
        //    GC.SuppressFinalize(this);
        //}

        //protected virtual void Dispose(bool disposing)
        //{
        //    if (disposing)
        //    {
        //        _cache1?.Clear();
        //        _cache1?.Dispose();
        //        _cache1 = null;

        //        _cache2?.Clear();
        //        _cache2?.Dispose();
        //        _cache2 = null;
        //    }
        //}

        [Fact]
        [Trait("category", "NCache")]
        public void NCache_Should_CacheItems()
        {
            using (var cacheManager = CacheFactory.Build<object>(
                s => s
                    .WithNCacheHandle(Alachisoft.NCache.Web.Caching.NCache.InitializeCache(_cacheName))))
            {
                var myTextKey = "MyDescription";
                var myTextValue = "The interest calculation";

                var myAmountKey = "MyAmount";
                var myAmountValue = 100;

                var myModifierKey = "MyModifier";
                var myModifierValue = 0.15;

                cacheManager.Add(myTextKey, myTextValue);
                cacheManager.Add(myAmountKey, myAmountValue);
                cacheManager.Add(myModifierKey, myModifierValue);

                myTextValue.Should().Be(cacheManager.Get<string>(myTextKey));
                myAmountValue.Should().Be(cacheManager.Get<int>(myAmountKey));
                myModifierValue.Should().Be(cacheManager.Get<double>(myModifierKey));
            }
        }

        [Fact]
        [Trait("category", "NCache")]
        public async Task NCache_Should_Remove_WhenBackplaneRemoved()
        {
            using (var backplaneCache = Alachisoft.NCache.Web.Caching.NCache.InitializeCache(_cacheName))
            using (var cacheManager = CacheFactory.Build<int>(
                s => s
                    .WithNCacheBackplane(Alachisoft.NCache.Web.Caching.NCache.InitializeCache(_cacheName))
                    .WithNCacheHandle(Alachisoft.NCache.Web.Caching.NCache.InitializeCache(_cacheName), true)))
            {
                var myAmountKey = "MyAmount";
                var myAmountValue = 100;

                cacheManager.Add(myAmountKey, myAmountValue);

                backplaneCache.Remove(myAmountKey);

                await Task.Delay(2000);

                cacheManager.Exists(myAmountKey).Should().BeFalse();
            }
        }

        //[Fact]
        //[Trait("category", "NCache")]
        //public async Task NCache_Should_UpdateUsing_NCache_MultipleHandles()
        //{
        //    var config = new ConfigurationBuilder()
        //        .WithSystemRuntimeCacheHandle()
        //        .EnablePerformanceCounters()
        //        .EnableStatistics()
        //        .And
        //        .WithNCacheHandle(Alachisoft.NCache.Web.Caching.NCache.InitializeCache(_cacheName))
        //        .EnablePerformanceCounters()
        //        .EnableStatistics()
        //        .Build();

        //    var cache1 = new BaseCacheManager<int>(config);
        //    var cache2 = new BaseCacheManager<int>(config);

        //    var myAmountKey = "MyAmount";
        //    var myAmountValue = 100;

        //    cache1.Add(myAmountKey, myAmountValue);
        //    cache1.Get(myAmountKey);
        //    cache2.Get(myAmountKey);

        //    cache1.Remove(myAmountKey);

        //    Task.Delay(2000);

        //    cache2.Exists(myAmountKey).Should().BeFalse();
        //    var test = cache2.CacheHandles.Skip(1).First().Stats;
        //    cache2.CacheHandles.Skip(1).First().Stats.GetStatistic(Core.Internal.CacheStatsCounterType.RemoveCalls).Should().Be(1);
        //}

        [Fact]
        [Trait("category", "NCache")]
        public async Task NCache_Should_RemoveAllHandles_WithBackplane_WhenMultipleHandles()
        {
            using (var backplaneCache = Alachisoft.NCache.Web.Caching.NCache.InitializeCache(_cacheName))
            using (var cacheManager = CacheFactory.Build<int>(
                s => s
                    .WithDictionaryHandle()
                    .And
                    .WithNCacheBackplane(Alachisoft.NCache.Web.Caching.NCache.InitializeCache(_cacheName))
                    .WithNCacheHandle(Alachisoft.NCache.Web.Caching.NCache.InitializeCache(_cacheName), true)))
            {
                var myAmount = 100;

                cacheManager.Add(nameof(myAmount), myAmount);
                cacheManager.Get(nameof(myAmount)).Should().Be(myAmount); // Hydrates the Dictionary Cache as well

                cacheManager.Exists(nameof(myAmount)).Should().BeTrue();

                backplaneCache.Remove(nameof(myAmount));
                await Task.Delay(2000);

                cacheManager.Exists(nameof(myAmount)).Should().BeFalse();
            }
        }

        [Fact]
        [Trait("category", "NCache")]
        public async Task NCache_Should_Clear_WithBackplane_WhenMultipleHandles()
        {
            using (var backplaneCache = Alachisoft.NCache.Web.Caching.NCache.InitializeCache(_cacheName))
            using (var cacheManager = CacheFactory.Build<int>(
                s => s
                    .WithDictionaryHandle()
                    .And
                    .WithNCacheBackplane(Alachisoft.NCache.Web.Caching.NCache.InitializeCache(_cacheName))
                    .WithNCacheHandle(Alachisoft.NCache.Web.Caching.NCache.InitializeCache(_cacheName), true)))
            {
                var myAmount = 100;

                cacheManager.Add(nameof(myAmount), myAmount);
                cacheManager.Get(nameof(myAmount)); // Hydrates the Dictionary Cache as well

                cacheManager.Exists(nameof(myAmount)).Should().BeTrue();

                backplaneCache.Clear();
                await Task.Delay(1000);

                cacheManager.Exists(nameof(myAmount)).Should().BeFalse();
            }
        }

        [Fact]
        [Trait("category", "NCache")]
        public async Task NCache_Should_ClearRegion_WithBackplane_WhenMultipleHandles()
        {
            using (var backplaneCache = Alachisoft.NCache.Web.Caching.NCache.InitializeCache(_cacheName))
            using (var cacheManager = CacheFactory.Build<int>(
                s => s
                    .WithDictionaryHandle()
                    .And
                    .WithNCacheBackplane(Alachisoft.NCache.Web.Caching.NCache.InitializeCache(_cacheName))
                    .WithNCacheHandle(Alachisoft.NCache.Web.Caching.NCache.InitializeCache(_cacheName), true)))
            {
                var region = "awesome";
                var myAmount = 100;

                cacheManager.Add(nameof(myAmount), myAmount);
                cacheManager.Add(nameof(myAmount), myAmount, region);

                cacheManager.Get(nameof(myAmount)); // Hydrates the Dictionary Cache as well
                cacheManager.Get(nameof(myAmount), region); // Hydrates the Dictionary Cache as well

                cacheManager.Exists(nameof(myAmount)).Should().BeTrue();
                cacheManager.Exists(nameof(myAmount), region).Should().BeTrue();

                backplaneCache.RemoveGroupData(region, null);
                await Task.Delay(1000);

                cacheManager.Exists(nameof(myAmount)).Should().BeTrue();
                cacheManager.Exists(nameof(myAmount), region).Should().BeFalse();
            }
        }

        [Fact]
        [Trait("category", "NCache")]
        [Trait("category", "OnlyWorksDebugMode")]
        public async Task NCache_Should_Update_WithBackplane()
        {
            using (var backplaneCache = Alachisoft.NCache.Web.Caching.NCache.InitializeCache(_cacheName))
            using (var topic = backplaneCache.MessagingService.CreateTopic("cacheManagerBackplane"))
            using (var cacheManager = CacheFactory.Build<int>(
                s => s
                    .WithDictionaryHandle()
                    .And
                    .WithNCacheBackplane(Alachisoft.NCache.Web.Caching.NCache.InitializeCache(_cacheName))
                    .WithNCacheHandle(Alachisoft.NCache.Web.Caching.NCache.InitializeCache(_cacheName), true)))
            {
                var sub = topic.CreateSubscription(MessageReceived);
                try
                {
                    var myCounter = 1;

                    cacheManager.AddOrUpdate(nameof(myCounter), myCounter, counter => counter + 1);
                    cacheManager.Update(nameof(myCounter), counter => counter + 1);

                    var receivedMessage = await WaitForMessageReceived(TimeSpan.FromSeconds(8), 1).ConfigureAwait(false);

                    cacheManager.Get(nameof(myCounter)).Should().Be(2);
                    receivedMessage.Should().BeTrue();
                }
                finally
                {
                    sub.UnSubscribe();
                }
            }
        }

        //[Fact]
        //[Trait("category", "NCache")]
        //public async Task NCache_Should_FailOptimisticConcurrency()
        //{
        //    using (var backplaneCache = Alachisoft.NCache.Web.Caching.NCache.InitializeCache(_cacheName))
        //    using (var cacheManager = CacheFactory.Build<int>(
        //        s => s
        //            .WithNCacheHandle(Alachisoft.NCache.Web.Caching.NCache.InitializeCache(_cacheName), true)))
        //    {
        //        var myValue = "hello1";

        //        cacheManager.Add(nameof(myValue), myValue);

        //        cacheManager.AddOrUpdate(nameof(myCounter), myCounter, counter => counter + 1);
        //        cacheManager.Update(nameof(myCounter), counter => counter + 1);

        //        var receivedMessage = await WaitForMessageReceived(TimeSpan.FromSeconds(8), 1).ConfigureAwait(false);

        //        cacheManager.Get(nameof(myCounter)).Should().Be(2);
        //        receivedMessage.Should().BeTrue();
        //    }
        //}

        //public async Task NCache_Test()
        //{
        //    // act/assert
        //    await RunMultipleCaches(
        //        async (cacheA, cacheB) =>
        //        {
        //            cacheA.Put(item);
        //            cacheA.Get(item.Key).Should().Be("something");
        //            await Task.Delay(10);
        //            var value = cacheB.Get(item.Key);
        //            value.Should().Be(item.Value, cacheB.ToString());
        //            cacheB.Put(item.Key, "new value");
        //        },
        //        async (cache) =>
        //        {
        //            int tries = 0;
        //            object value = null;
        //            do
        //            {
        //                tries++;
        //                await Task.Delay(100);
        //                value = cache.Get(item.Key);
        //            }
        //            while (value.ToString() != "new value" && tries < 10);

        //            value.Should().Be("new value", cache.ToString());
        //        },
        //        1,
        //        TestManagers.CreateRedisAndDicCacheWithBackplane(50, true, channelName, Serializer.Json),
        //        TestManagers.CreateRedisAndDicCacheWithBackplane(50, true, channelName, Serializer.Json),
        //        TestManagers.CreateRedisCache(50, false, Serializer.Json),
        //        TestManagers.CreateRedisAndDicCacheWithBackplane(50, true, channelName, Serializer.Json));
        //}

        private async Task<bool> WaitForMessageReceived(TimeSpan timeout, long numberToReceive, bool waitFullDuration = false)
        {
            DateTime giveUpAt = DateTime.Now + timeout;

            while (DateTime.Now < giveUpAt && (waitFullDuration || _messagesReceived < numberToReceive))
            {
                await Task.Delay(500).ConfigureAwait(false);
            }

            return _messagesReceived == numberToReceive;
        }

        private void MessageReceived(object sender, MessageEventArgs args)
        {
            Interlocked.Increment(ref _messagesReceived);
        }
    }
}
