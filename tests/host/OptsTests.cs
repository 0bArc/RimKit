using System.Collections.Generic;
using RimKit;
using Xunit;

namespace RimKit.Host.Tests
{
    public class OptsTests
    {
        private static Dictionary<string, string> Args(string json) => new Dictionary<string, string> { ["opts"] = json };

        [Fact]
        public void ReadsScalarsWithFallbacks()
        {
            Opts o = Opts.From(Args("{\"def\":\"Steel\",\"count\":25,\"ratio\":0.5,\"queue\":true}"), "opts");
            Assert.Equal("Steel", o.Str("def"));
            Assert.Equal(25, o.Int("count"));
            Assert.Equal(0.5, o.Num("ratio"));
            Assert.True(o.Bool("queue"));
            Assert.Equal("fallback", o.Str("missing", "fallback"));
            Assert.Equal(7, o.Int("missing", 7));
            Assert.False(o.Has("missing"));
            Assert.True(o.Has("def"));
        }

        [Fact]
        public void MissingOrBrokenOptsGiveEmptyOptions()
        {
            Assert.False(Opts.From(new Dictionary<string, string>(), "opts").Has("x"));
            Assert.False(Opts.From(Args("not json"), "opts").Has("x"));
            Assert.False(Opts.From(Args(""), "opts").Has("x"));
        }

        [Fact]
        public void ReadsNestedObjectsAndStringLists()
        {
            Opts o = Opts.From(Args("{\"area\":{\"x1\":1,\"z1\":2},\"defs\":[\"Steel\",\"Silver\"]}"), "opts");
            Assert.Equal(1, Json.GetLong(o.Obj("area"), "x1"));
            Assert.Equal(new List<string> { "Steel", "Silver" }, o.Strings("defs"));
            Assert.Null(o.Obj("nothing"));
        }

        [Fact]
        public void NullValuesCountAsAbsent()
        {
            Opts o = Opts.From(Args("{\"a\":null}"), "opts");
            Assert.False(o.Has("a"));
        }

        [Fact]
        public void HandleObjectWithoutLiveTargetIsNull()
        {
            Opts o = Opts.From(Args("{\"map\":{\"$h\":424242}}"), "opts");
            Assert.Null(o.Handle<object>("map"));
            Assert.Empty(o.Handles<object>("none"));
        }
    }

    public class JbTests
    {
        [Fact]
        public void BuildsAnObject()
        {
            string json = Jb.Obj().S("a", "x\"y").I("b", 3).F("c", 1.5).B("d", true).ToString();
            Assert.True(Json.TryParse(json, out object v));
            var o = Json.AsObject(v);
            Assert.Equal("x\"y", Json.GetString(o, "a"));
            Assert.Equal(3, Json.GetLong(o, "b"));
            Assert.True(Json.GetBool(o, "d"));
        }

        [Fact]
        public void NullStringsLeaveTheKeyOut()
        {
            Assert.Equal("{}", Jb.Obj().S("a", null).ToString());
        }

        [Fact]
        public void BuildsAnArrayOfObjects()
        {
            string json = Jb.Arr().Add(Jb.Obj().I("n", 1)).Add(Jb.Obj().I("n", 2)).AddS("end").ToString();
            Assert.True(Json.TryParse(json, out object v));
            var list = Json.AsArray(v);
            Assert.Equal(3, list.Count);
            Assert.Equal(2, Json.GetLong(Json.AsObject(list[1]), "n"));
            Assert.Equal("end", list[2]);
        }

        [Fact]
        public void NonFiniteNumbersBecomeZero()
        {
            string json = Jb.Obj().F("x", double.NaN).F("y", double.PositiveInfinity).ToString();
            Assert.True(Json.TryParse(json, out object v));
            Assert.Equal(0, Json.GetLong(Json.AsObject(v), "x"));
            Assert.Equal(0, Json.GetLong(Json.AsObject(v), "y"));
        }

        [Fact]
        public void OkWrapsTheResultForTheNativeLayer()
        {
            string ok = Jb.Obj().I("n", 1).Ok();
            Assert.True(Json.TryParse(ok, out object v));
            var o = Json.AsObject(v);
            Assert.True(Json.GetBool(o, "ok"));
            Assert.Equal("j", Json.GetString(o, "t"));
            Assert.NotNull(Json.AsObject(o["v"]));
        }

        [Fact]
        public void RawNullIsWritten()
        {
            Assert.Equal("{\"a\":null}", Jb.Obj().Raw("a", null).ToString());
        }
    }

    public class JsonLiteTests
    {
        [Fact]
        public void QuoteEscapesSpecialCharacters()
        {
            Assert.Equal("\"a\\\"b\\\\c\\nd\"", JsonLite.Quote("a\"b\\c\nd"));
            Assert.Equal("\"\"", JsonLite.Quote(null));
        }

        [Fact]
        public void ParseObjectReadsFlatArgumentsCaseInsensitively()
        {
            var d = JsonLite.ParseObject("{\"h\":5,\"Def\":\"Steel\",\"flag\":true}");
            Assert.Equal("5", d["h"]);
            Assert.Equal("Steel", d["def"]);
            Assert.Equal("true", d["FLAG"]);
        }

        [Fact]
        public void ParseObjectToleratesGarbage()
        {
            Assert.Empty(JsonLite.ParseObject(""));
            Assert.Empty(JsonLite.ParseObject("[1,2]"));
            Assert.Empty(JsonLite.ParseObject("null"));
        }

        [Fact]
        public void NestedTableArgumentsStayAsText()
        {
            var d = JsonLite.ParseObject("{\"opts\":\"{\\\"def\\\":\\\"Steel\\\"}\"}");
            Assert.Equal("{\"def\":\"Steel\"}", d["opts"]);
            Assert.Equal("Steel", Opts.From(d, "opts").Str("def"));
        }
    }
}
