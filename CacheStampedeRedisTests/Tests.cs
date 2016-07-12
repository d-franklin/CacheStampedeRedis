namespace CacheStampedeRedisTests
{
    using System;
    using System.Net;
    using System.Threading.Tasks;

    using CacheStampedeRedis;

    using Moq;

    using ProtoBuf;

    using StackExchange.Redis;

    using Xunit;
    using Xunit.Abstractions;

    public class Tests : IDisposable
    {
        private readonly ConnectionMultiplexer _db;
        private readonly ITestOutputHelper _output;

        public Tests(ITestOutputHelper output)
        {
            this._output = output;

            this._db = ConnectionMultiplexer.Connect(new ConfigurationOptions
            {
                DefaultVersion = new Version(3, 0, 500),
                EndPoints = { { "localhost", 6379 } },
                AllowAdmin = true
            });
        }

        public void Dispose()
        {
            foreach (EndPoint endpoint in this._db.GetEndPoints())
            {
                IServer server = this._db.GetServer(endpoint);
                server.FlushDatabase();
            }

            this._db.Dispose();
        }

        [Fact]
        public void Fetch_Should_Call_Read_In_ICacheStampedeStore()
        {
            Mock<ICacheStampedeStore> mock = new Mock<ICacheStampedeStore>();
            mock.Setup(m => m.Read<TestObject>(It.IsAny<int>())).Returns((int i) => new TestObject { Id = i });

            CacheStampedeRedis cache = new CacheStampedeRedis(this._db, mock.Object);
            TestObject test = cache.Fetch<TestObject>(1, "Test Cache Key 1", TimeSpan.FromDays(1));

            mock.Verify(m => m.Read<TestObject>(1), Times.Once);
            Assert.Equal(1, test.Id);
        }

        [Fact]
        public void Fetch_Should_Return_From_Cache()
        {
            Mock<ICacheStampedeStore> mock = new Mock<ICacheStampedeStore>();
            mock.Setup(m => m.Read<TestObject>(It.IsAny<int>())).Returns((int i) => new TestObject { Id = i });

            CacheStampedeRedis cache = new CacheStampedeRedis(this._db, mock.Object);
            cache.Fetch<TestObject>(1, "Test Cache Key 2", TimeSpan.FromDays(1));

            TestObject test = cache.Fetch<TestObject>(1, "Test Cache Key 2", TimeSpan.FromDays(1));

            mock.Verify(m => m.Read<TestObject>(1), Times.Once);
            Assert.Equal(1, test.Id);
        }

        [Fact]
        public void Read_Calls_Should_Be_In_Range()
        {
            const int totalFetches = 100;
            int taskDelay = (int)TimeSpan.FromMilliseconds(100).TotalMilliseconds;
            TimeSpan cacheTime = TimeSpan.FromMilliseconds(200);

            int callbackCount = 0;

            Mock<ICacheStampedeStore> mock = new Mock<ICacheStampedeStore>();
            mock.Setup(m => m.Read<TestObject>(It.IsAny<int>())).Returns((int i) =>
            {
                // Simulate latency
                Task.Delay(taskDelay).Wait();
                callbackCount++;
                return new TestObject { Id = i };
            });

            CacheStampedeRedis cache = new CacheStampedeRedis(this._db, mock.Object);

            for (int i = 0; i < totalFetches; i++)
            {
                TestObject test = cache.Fetch<TestObject>(i, "Test Cache Key 3", cacheTime);
                //this._output.WriteLine("Object ID: {0}", test.Id);
            }

            this._output.WriteLine("Total Fetches: {0}, Total Callbacks: {1}", totalFetches, callbackCount);
            
            Assert.InRange(callbackCount, 1, 20);
        }
    }

    [ProtoContract]
    public class TestObject
    {
        [ProtoMember(1)]
        public int Id { get; set; }
    }
}