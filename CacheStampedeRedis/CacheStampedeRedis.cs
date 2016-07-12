namespace CacheStampedeRedis
{
    using System;
    using System.Diagnostics;

    using ProtoBuf;

    using StackExchange.Redis;
    using StackExchange.Redis.Extensions.Core;
    using StackExchange.Redis.Extensions.Protobuf;

    public interface ICacheStampedeRedis
    {
        T Fetch<T>(int id, string cacheKey, TimeSpan timeToLive, byte beta = 1);
    }

    public interface ICacheStampedeStore
    {
        T Read<T>(int id);
    }

    public class CacheStampedeRedis : ICacheStampedeRedis
    {
        private readonly StackExchangeRedisCacheClient _cache;
        private readonly Random _random = new Random();
        private readonly ICacheStampedeStore _store;

        public CacheStampedeRedis(IConnectionMultiplexer connectionMultiplexer, ICacheStampedeStore store)
        {
            // BUG: Cast to ConnectionMultiplexer until StackExchangeRedisCacheClient is updated to accept IConnectionMultiplexer
            this._cache = new StackExchangeRedisCacheClient((ConnectionMultiplexer)connectionMultiplexer, new ProtobufSerializer());
            this._store = store;
        }

        /// <summary>
        /// Attempts to read a value from the cache and uses probabilistic cache regeneration when necessary.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="id">The ID to use when accessing the Store when regenerating data</param>
        /// <param name="cacheKey">The cache key</param>
        /// <param name="timeToLive">Time which the cached item should be alive for</param>
        /// <param name="beta">Setting value higher than 1 will favor early expire of cache</param>
        /// <returns></returns>
        public T Fetch<T>(int id, string cacheKey, TimeSpan timeToLive, byte beta = 1)
        {
            CacheContainer<T> item = this._cache.Get<CacheContainer<T>>(cacheKey);
            if (item != null)
            {
                double calc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - item.Delta * beta * Math.Log(this._random.NextDouble());
                if (calc < item.ExpirationTime)
                {
                    return item.Value;
                }
            }

            // Start Measuring Delta
            Stopwatch sw = Stopwatch.StartNew();

            // Compute Value
            T value = this._store.Read<T>(id);

            // Stop
            sw.Stop();

            // Add to Cache
            CacheContainer<T> cacheContainer = new CacheContainer<T>
            {
                Value = value,
                Delta = sw.ElapsedMilliseconds,
                ExpirationTime = DateTimeOffset.UtcNow.AddMilliseconds(timeToLive.TotalMilliseconds).ToUnixTimeMilliseconds()
            };

            // Extend cache expiry
            TimeSpan ttl = timeToLive.Add(TimeSpan.FromMilliseconds(cacheContainer.ExpirationTime - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
            this._cache.Add(cacheKey, cacheContainer, ttl);

            // Return Value
            return value;
        }

        [ProtoContract]
        private class CacheContainer<T>
        {
            [ProtoMember(1)]
            internal T Value { get; set; }

            [ProtoMember(2)]
            internal long Delta { get; set; }

            [ProtoMember(3)]
            internal long ExpirationTime { get; set; }
        }
    }
}