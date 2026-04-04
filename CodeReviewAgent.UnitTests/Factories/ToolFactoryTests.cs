using Xunit;
using CodeReviewAgent.Factories;
using CodeReviewAgent.Tools;
using CodeReviewAgent.Utils;

namespace CodeReviewAgent.UnitTests.Factories
{
    public class ToolFactoryTests
    {
        [Fact]
        public void CreateTool_ReturnsCorrectToolType()
        {
            // Arrange
            var ignoreMatcher = new IgnorePatternMatcher();
            var factory = new ToolFactory(ignoreMatcher);

            // Act & Assert
            var readFileTreeTool = factory.CreateTool("ReadFileTreeTool");
            Assert.NotNull(readFileTreeTool);

            var readFileTool = factory.CreateTool("ReadFileTool");
            Assert.NotNull(readFileTool);

            var writeFileTool = factory.CreateTool("WriteFileTool");
            Assert.NotNull(writeFileTool);

            var searchAndReplaceTool = factory.CreateTool("SearchAndReplaceTool");
            Assert.NotNull(searchAndReplaceTool);

            var fileGitDiffTool = factory.CreateTool("FileGitDiffTool");
            Assert.NotNull(fileGitDiffTool);
        }

        [Fact]
        public void CreateTool_ThrowsForUnknownTool()
        {
            // Arrange
            var ignoreMatcher = new IgnorePatternMatcher();
            var factory = new ToolFactory(ignoreMatcher);

            // Act & Assert
            Assert.Throws<ArgumentException>(() => factory.CreateTool("UnknownTool"));
        }
    }
}
