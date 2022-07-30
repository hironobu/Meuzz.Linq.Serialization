using System;
using Xunit;

namespace Meuzz.Linq.Serialization.Tests
{
    public class ExpressionAndSystemTextJsonTest
    {
        [Fact]
        public void Test01()
        {
            var data = SystemTextJsonSerializer.Serialize<Func<SampleItem, bool>>(x => x.Name == "bbb");

            var ff = SystemTextJsonSerializer.Deserialize<Func<SampleItem, bool>>(data);

            var obj = new SampleItem(1, "bbb");

            var ret = ff(obj);

            Assert.True(ret);
        }

        [Fact]
        public void Test02()
        {
            var s = "bbb";
            var data = SystemTextJsonSerializer.Serialize<Func<SampleItem, bool>>(x => x.Name == s);

            var ff = SystemTextJsonSerializer.Deserialize<Func<SampleItem, bool>>(data);

            var obj = new SampleItem(1, "bbb");

            var ret = ff(obj);

            Assert.True(ret);
        }
    }
}
