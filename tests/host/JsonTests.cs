using System.Collections.Generic;
using System.Text;
using RimKit;
using Xunit;

namespace RimKit.Host.Tests
{
    public class JsonTests
    {
        [Fact]
        public void ParsesObjectsArraysAndScalars()
        {
            Assert.True(Json.TryParse("{\"a\":1,\"b\":[true,null,\"x\"],\"c\":{\"d\":2.5}}", out object v));
            var o = Json.AsObject(v);
            Assert.Equal(1, Json.GetLong(o, "a"));
            Assert.Equal(3, Json.AsArray(o["b"]).Count);
            Assert.Equal(2.5, (double)Json.AsObject(o["c"])["d"]);
        }

        [Fact]
        public void RejectsBrokenJson()
        {
            Assert.False(Json.TryParse("{\"a\":", out _));
        }

        [Fact]
        public void StringsRoundTripWithEscapes()
        {
            string original = "line1\nline2\t\"quoted\" \\ back";
            var sb = new StringBuilder();
            Json.WriteString(sb, original);
            Assert.True(Json.TryParse(sb.ToString(), out object parsed));
            Assert.Equal(original, parsed);
        }

        [Fact]
        public void StringListSkipsMissingKey()
        {
            Assert.True(Json.TryParse("{\"sig\":[\"int\",\"string\"]}", out object v));
            Assert.Equal(new List<string> { "int", "string" }, Json.GetStringList(Json.AsObject(v), "sig"));
            Assert.Empty(Json.GetStringList(Json.AsObject(v), "none"));
        }
    }
}
