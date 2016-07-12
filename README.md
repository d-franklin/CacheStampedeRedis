## CacheStampedeRedis

A solution to cache stampede with redis, written in C#.

#### WARNING

This has **not** been tested in a production environment and should *only* be treated as a *potential* solution to the problem of Cache Stampede.

Also this solution is not designed to reduce data regeneration requests to an absolute optimal number, which is typically one regeneration request per cached object.

Instead it will allow multiple regeneration requests to be made while reducing the number of requests to an acceptable level and avoiding cache stampede.

NOTE: At the time of writting (2016/07/12) StackExchange.Redis.Extensions.Core doesn't accept IConnectionMultiplexer as a parameter for StackExchangeRedisCacheClient contructor. So while you can pass IConnectionMultiplexer it will be cast to ConnectionMultiplexer.

#### About

##### Cache Stampede

> A cache stampede is a type of cascading failure that can occur when massively parallel computing systems with caching mechanisms come under high load. This behaviour is sometimes also called dog-piling.
>
> Under low load, cache misses result in a single regeneration operation. The system will continue as before, with average load being kept very low because of the high cache hit rate.
>
> However, under heavy load when the cached version expires, there may be sufficient concurrency that multiple threads of execution will all attempt to regenerate the data simultaneously.
> 
> If sufficiently high load is present, this may by itself be enough to bring about congestion collapse of the system via exhausting shared resources.
> 
> Congestion collapse results in preventing the data from ever being completely re-generated and re-cached, as every attempt to do so times out.
> 
> Thus, cache stampede reduces the cache hit rate to zero and keeps the system continuously in congestion collapse as it attempts to regenerate the resource for as long as the load remains very heavy.
>
> *[wikipedia](https://en.wikipedia.org/wiki/Cache_stampede)*

##### Alternative Solutions

There are a great number of proposed solutions to the problem of Cache Stampede with most relying on locking, a retry mechanism or a mixture of the two.

While this works and might not be noticeable when data regeneration is quick, it is not optimal since it will delay (via blocking) new requests until data regeneration is complete. The delay in turn becomes noticeable when data regeneration is slow and makes the solution unusable.

##### Solution

The solution to mitigating cache stampede without locks or retries is to regenerate the data before cache expiry which allows data to be returned while regeneration is in process.

_Simple example_

```
public class CacheItem
{
    public object Value { get; set; }
    public double ExpirationTime { get; set; }
}

public object Get(stirng key, TimeSpan cacheExpiry)
{
	CacheItem cache = GetFromCache(key);
	if (cache != null && DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() < cache.ExpirationTime)
	{
		return cache;
	}

	if (cache != null)
	{
		// Extend the cache expiry by 10% so subsequent request will skip cache regeneration
		CacheItem item = new CacheItem
		{
			item.Value = data;
			// Extend the cache expiry time by 10% and add that to the CacheItem object
			item.ExpirationTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + timespan.TotalMilliseconds - timespan.TotalMilliseconds * 0.1;
		};

		AddToCache(item);
	}

	object data = RegenerateDataFromStore();
	
	CacheItem item = new CacheItem
	{
		item.Value = data;
		// Reduce the cache expiry time by 20% and add that to the CacheItem object
		item.ExpirationTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + timespan.TotalMilliseconds - timespan.TotalMilliseconds * 0.2;
	};

	AddToCache(item);

	return data;
}
```

The problem with using the above method is that there is no way to predict the regeneration time, so reducing the expiry by the arbitrary amount of 20% may work but any value is prone to error. When using this method, it is typical to use large gaps between the object expiry (expiry value stored in the object) and the cache expiry in an attempt to avoid this problem (for example, the object expiry is 1 hour and the cache expiry is 1 day).

Using an algorithmic approach the need for a large expiry gap between object and cache can be eliminated.

This implementation uses an algorithm taken from the research paper titled '[Optimal Probabilistic Cache Stampede Prevention](http://cseweb.ucsd.edu/~avattani/papers/cache_stampede.pdf)' by Andrea Vattani, Flavio Chierichetti and Keegan Lowenstein.

The algorythm takes into account the time needed for regeneration (delta), along with the ability to favor early regeneration (beta) which allows cache regeneration to be performed in the most optimal manner.

#### Usage

* Implement ICacheStampedeStore. The method 'Read' will be called when data regeneration is required.

    *Example*
    ````
    public class SQL : ICacheStampedeStore
    {
        public T Read<T>(int id)
        {
            // Return DB reults where ID = id
        }
    }

    OR

    public class MyPreExistingSQL : ISql, ICacheStampedeStore
    ...
    ````

* Register with IoC or create new instance
    ```
    // Example using Simple Injector
    container.Register<IConnectionMultiplexer, ConnectionMultiplexer>(Lifestyle.Singleton);
    container.Register<ICacheStampedeStore, SQL>(Lifestyle.Transient);
    container.Register<ICacheStampedeRedis, CacheStampedeRedis>(Lifestyle.Singleton);

    OR

    CacheStampedeRedis cache = new CacheStampedeRedis(new ConnectionMultiplexer(), new SQL());
    ```

* Access Cache
    ````
    cacheStampede.Fetch(1, "Cache Key", TimeSpan.FromDays(30))
    
    OR
    
    // Setting the beta value higher than 1 will favor early expire of cache.
    cacheStampede.Fetch(1, "Cache Key", TimeSpan.FromDays(30), 2)
    ````

#### Dependancies

* StackExchange.Redis
* StackExchange.Redis.Extensions.Core (serialization of objects)
* StackExchange.Redis.Extensions.Protobuf (serialize to ProtoBuf)
* protobuf-net

Note: Objects don't need to be serialized with ProtoBuf nor does this implementation require the use of Redis. With minor modifications, any caching solution could be used, MemoryCache for example.

#### License

MIT License

Copyright (c) 2016 Daniel Franklin

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.