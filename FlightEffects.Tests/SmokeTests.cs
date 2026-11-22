using Xunit;

namespace FlightEffects.Tests
{
    public class SmokeTests
    {
        [Fact]
        public void CoreAssemblyCompiles()
        {
            Assert.NotNull(typeof(FlightEffects.Core.ColorMath));
        }
    }
}
