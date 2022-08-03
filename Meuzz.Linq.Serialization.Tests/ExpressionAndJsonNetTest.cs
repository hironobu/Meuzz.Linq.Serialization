using System;
using TestClass;
using Xunit;

namespace Meuzz.Linq.Serialization.Tests
{
    public class ExpressionAndJsonNetTest
    {
        [Fact]
        public void TestSerializeAndDeserializeWithImmediateValues()
        {
            var serializer = ExpressionSerializer.CreateInstance();

            var data = serializer.Serialize<Func<SampleItem, bool>>(x => x.Name == "bbb");

            var ff = serializer.Deserialize<Func<SampleItem, bool>>(data);

            var obj = new SampleItem(1, "bbb");

            var ret = ff(obj);

            Assert.True(ret);
        }

        [Fact]
        public void TestSerializeAndDeserializeWithVariables()
        {
            var serializer = ExpressionSerializer.CreateInstance();

            var s = "bbb";
            var data = serializer.Serialize<Func<SampleItem, bool>>(x => x.Name == s);

            var ff = serializer.Deserialize<Func<SampleItem, bool>>(data);

            var obj = new SampleItem(1, "bbb");

            var ret = ff(obj);

            Assert.True(ret);
        }
    }
}
