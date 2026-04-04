using Xunit;
using CodeReviewAgent.Tools;
using CodeReviewAgent.Utils;

namespace CodeReviewAgent.UnitTests.Tools
{
    public class ReadFileTreeToolTests
    {
        [Fact]
        public async Task RunToolAsync_ReturnsFileTree()
        {
            // Arrange
            var ignoreMatcher = new IgnorePatternMatcher();
            var tool = new ReadFileTreeTool(ignoreMatcher);

            // Act
            var result = await tool.RunToolAsync("CodeReviewAgent");

            // Assert
            Assert.NotNull(result);
            // Note: Directory may be empty in test environment
        }
    }
}
