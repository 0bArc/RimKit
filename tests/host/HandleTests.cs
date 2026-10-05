using RimKit;
using Xunit;

namespace RimKit.Host.Tests
{
    // Handles for plain objects are pinned. Things, maps and factions are weak and need the game to construct, so the
    // weak path is covered by the in-game smoke test.
    [Collection("handles")]
    public class HandleTests
    {
        private sealed class Plain { }

        static HandleTests() { GameAssemblies.Ensure(); }

        [Fact]
        public void SameObjectGetsTheSameHandle()
        {
            ObjectHandles.Clear();
            var o = new Plain();
            int a = ObjectHandles.GetOrAdd(o);
            int b = ObjectHandles.GetOrAdd(o);
            Assert.True(a > 0);
            Assert.Equal(a, b);
        }

        [Fact]
        public void DifferentObjectsGetDifferentHandles()
        {
            ObjectHandles.Clear();
            Assert.NotEqual(ObjectHandles.GetOrAdd(new Plain()), ObjectHandles.GetOrAdd(new Plain()));
        }

        [Fact]
        public void GetReturnsTheObjectAndNullForTheWrongTypeOrAnUnknownHandle()
        {
            ObjectHandles.Clear();
            var o = new Plain();
            int h = ObjectHandles.GetOrAdd(o);
            Assert.Same(o, ObjectHandles.Get<Plain>(h));
            Assert.Null(ObjectHandles.Get<string>(h));
            Assert.Null(ObjectHandles.Get<Plain>(987654));
            Assert.Null(ObjectHandles.Get<Plain>(0));
        }

        [Fact]
        public void NullHasNoHandle()
        {
            Assert.Equal(0, ObjectHandles.GetOrAdd(null));
        }

        [Fact]
        public void ClearInvalidatesEveryHandle()
        {
            ObjectHandles.Clear();
            var o = new Plain();
            int h = ObjectHandles.GetOrAdd(o);
            int generation = ObjectHandles.Generation;
            ObjectHandles.Clear();
            Assert.Null(ObjectHandles.Get<Plain>(h));
            Assert.True(ObjectHandles.Generation >= generation);
        }
    }
}
