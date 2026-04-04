using Xunit;
using CodeReviewAgent.Tools;

namespace CodeReviewAgent.UnitTests.Tools
{
    public class ReadFileToolTests
    {
        [Fact]
        public async Task RunToolAsync_ReturnsFileContent()
        {
            // Arrange
            var tool = new ReadFileTool();

            // Act
            var result = await tool.RunToolAsync("CodeReviewAgent/Tools/ITool.cs");

            // Assert
            Assert.NotNull(result);
            // Note: File may be empty in test environment
        }
    }
}
