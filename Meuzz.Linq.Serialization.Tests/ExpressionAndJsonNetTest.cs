using System;
using Xunit;

namespace Meuzz.Linq.Serialization.Tests
{
    public class ExpressionAndJsonNetTest
    {
        [Fact]
        public void TestSerializeAndDeserializeWithImmediateValues()
        {
            var data = JsonNetSerializer.Serialize<Func<SampleItem, bool>>(x => x.Name == "bbb");

            var ff = JsonNetSerializer.Deserialize<Func<SampleItem, bool>>(data);

            var obj = new SampleItem(1, "bbb");

            var ret = ff(obj);

            Assert.True(ret);
        }

        [Fact]
        public void TestSerializeAndDeserializeWithVariables()
        {
            var s = "bbb";
            var data = JsonNetSerializer.Serialize<Func<SampleItem, bool>>(x => x.Name == s);

            var ff = JsonNetSerializer.Deserialize<Func<SampleItem, bool>>(data);

            var obj = new SampleItem(1, "bbb");

            var ret = ff(obj);

            Assert.True(ret);
        }
    }
}
