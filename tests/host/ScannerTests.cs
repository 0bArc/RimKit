using System.Linq;
using RimKit;
using Xunit;

namespace RimKit.Host.Tests
{
    public class ScannerTests
    {
        // A rule shows up either as a blocking hit (the mod is quarantined) or as a warning (the sandbox already removes the function).
        private static bool Flags(string lua, string rule) =>
            LuaThreatScanner.Analyze(lua).Concat(LuaThreatScanner.Warnings(lua)).Any(h => h.Contains("[" + rule + "]"));

        private static bool Blocks(string lua, string rule) => LuaThreatScanner.Analyze(lua).Any(h => h.Contains("[" + rule + "]"));

        [Fact]
        public void SandboxedLoadersOnlyWarn()
        {
            Assert.False(LuaThreatScanner.Analyze("local f = load(\"return 1\")\nlocal d = debug.traceback()\nlocal v = _G[\"x\"]\n").Any());
            Assert.True(LuaThreatScanner.Warnings("local f = load(\"return 1\")\n").Any());
        }

        [Fact]
        public void OsAndIoStillBlock()
        {
            Assert.True(Blocks("os.execute(\"x\")", "os.execute"));
            Assert.True(Blocks("local f = io.open(\"a\")", "io library"));
        }

        [Fact]
        public void MemberNamesThatLookLikeLibrariesAreNotFlagged()
        {
            Assert.False(Flags("game.os.execute(1)\nfoo.debug.trace()\nthing.io.open(2)\n", "os.execute"));
            Assert.False(Flags("foo.debug.trace()", "debug library"));
            Assert.False(Flags("thing.io.open(2)", "io library"));
        }

        [Fact]
        public void CleanCodeHasNoHits()
        {
            Assert.Empty(LuaThreatScanner.Analyze("local x = game.time.ticks()\nlog.info(\"hello\")\n"));
        }

        [Fact]
        public void GlobalLoadCallIsFlagged()
        {
            Assert.True(Flags("local f = load(\"return 1\")", "load()"));
        }

        [Fact]
        public void FunctionNamedLoadIsNotAGlobalLoadCall()
        {
            Assert.False(Flags("local function load(path) return path end", "load()"));
        }

        [Fact]
        public void MemberLoadIsNotFlagged()
        {
            Assert.False(Flags("store:load(1)\nstore.load(2)\n", "load()"));
        }

        [Fact]
        public void WordsInsideStringsAreNotFlagged()
        {
            Assert.Empty(LuaThreatScanner.Analyze("log.info(\"call os.execute or load( later\")"));
        }

        [Fact]
        public void WordsInsideCommentsAreNotFlagged()
        {
            Assert.Empty(LuaThreatScanner.Analyze("-- os.execute(\"x\")\n--[[ load(\"y\") ]]\nlocal a = 1\n"));
        }

        [Fact]
        public void GlobalTableTricksAreFlagged()
        {
            Assert.True(Flags("_G.load(\"x\")", "global loader member"));
            Assert.True(Blocks("_G.os.execute(\"x\")", "global dangerous member"));
            Assert.True(Flags("_G[\"load\"](\"x\")", "global table indexing"));
        }

        [Fact]
        public void ReflectionIntoSystemIoIsFlaggedEvenInsideAString()
        {
            Assert.True(Flags("game.reflect.static_call(\"System.IO.File\", \"Delete\", \"x\")", "System.IO"));
        }

        [Fact]
        public void HitReportsTheLine()
        {
            var hit = LuaThreatScanner.Analyze("local a = 1\nlocal b = 2\nos.execute(\"x\")\n").First();
            Assert.StartsWith("3 ", hit);
        }

        [Fact]
        public void BlankingKeepsLineNumbers()
        {
            string source = "a = [[x\ny]]\nb = \"q\"\n";
            string blanked = LuaThreatScanner.BlankStrings(source);
            Assert.Equal(source.Length, blanked.Length);
            Assert.Equal(LuaThreatScanner.LineOf(source, source.Length), LuaThreatScanner.LineOf(blanked, blanked.Length));
            Assert.DoesNotContain("x", blanked);
        }
    }
}
