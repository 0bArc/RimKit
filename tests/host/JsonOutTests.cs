using System.Collections.Generic;
using RimKit;
using Xunit;

namespace RimKit.Host.Tests
{
    public class JsonOutTests
    {
        [Fact]
        public void WritesNestedValuesThatTheReaderParsesBack()
        {
            var tree = new Dictionary<string, object>
            {
                ["type"] = "column",
                ["gap"] = 4.5,
                ["count"] = 3L,
                ["flag"] = true,
                ["none"] = null,
                ["children"] = new List<object> { new Dictionary<string, object> { ["text"] = "a \"quoted\" line\n" }, "plain" },
            };
            Assert.True(Json.TryParse(JsonOut.Of(tree), out object back));
            var o = Json.AsObject(back);
            Assert.Equal("column", Json.GetString(o, "type"));
            Assert.Equal(3, Json.GetLong(o, "count"));
            Assert.True(Json.GetBool(o, "flag"));
            var kids = Json.AsArray(o["children"]);
            Assert.Equal("a \"quoted\" line\n", Json.GetString(Json.AsObject(kids[0]), "text"));
            Assert.Equal("plain", kids[1]);
        }

        [Fact]
        public void NonFiniteNumbersBecomeZeroAndDeepTreesAreCut()
        {
            Assert.Equal("[0,0]", JsonOut.Of(new List<object> { double.NaN, float.PositiveInfinity }));
            object deep = "x";
            for (int i = 0; i < 40; i++) deep = new List<object> { deep };
            Assert.Contains("null", JsonOut.Of(deep));
        }

        [Fact]
        public void StringsAndBooleansAndNull()
        {
            Assert.Equal("\"hi\"", JsonOut.Of("hi"));
            Assert.Equal("true", JsonOut.Of(true));
            Assert.Equal("null", JsonOut.Of(null));
        }
    }
}
