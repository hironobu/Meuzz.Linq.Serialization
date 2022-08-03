using System;
using Meuzz.Linq.Serialization.Serializers;
using TestClass;
using Xunit;

namespace Meuzz.Linq.Serialization.Tests
{
    public class ExpressionAndJsonNetTest
    {
        [Fact]
        public void TestSerializeAndDeserializeWithImmediateValues()
        {
            var data = new JsonNetSerializer().Serialize<Func<SampleItem, bool>>(x => x.Name == "bbb");

            var ff = new JsonNetSerializer().Deserialize<Func<SampleItem, bool>>(data);

            var obj = new SampleItem(1, "bbb");

            var ret = ff(obj);

            Assert.True(ret);
        }

        [Fact]
        public void TestSerializeAndDeserializeWithVariables()
        {
            var s = "bbb";
            var data = new JsonNetSerializer().Serialize<Func<SampleItem, bool>>(x => x.Name == s);

            var ff = new JsonNetSerializer().Deserialize<Func<SampleItem, bool>>(data);

            var obj = new SampleItem(1, "bbb");

            var ret = ff(obj);

            Assert.True(ret);
        }
    }
}
