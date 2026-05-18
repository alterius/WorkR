using Shouldly;

namespace WorkR.Tests
{
    [Trait("Category", "L0")]
    public class ValueTriggerContextTests
    {
        [Fact]
        public void Constructor_SetsOccurredAt()
        {
            var now = DateTimeOffset.UtcNow;

            var context = new ValueTriggerContext<string>(now, "hello");

            context.OccurredAt.ShouldBe(now);
        }

        [Fact]
        public void Constructor_SetsValue()
        {
            var context = new ValueTriggerContext<string>(DateTimeOffset.UtcNow, "hello");

            context.Value.ShouldBe("hello");
        }

        [Fact]
        public void Constructor_SetsUniqueExecutionId()
        {
            var a = new ValueTriggerContext<string>(DateTimeOffset.UtcNow, "x");
            var b = new ValueTriggerContext<string>(DateTimeOffset.UtcNow, "x");

            a.ExecutionId.ShouldNotBe(b.ExecutionId);
        }
    }
}
