using Xunit;
using System.IO;
using CodeReviewAgent.Tools;

namespace CodeReviewAgent.UnitTests.Tools
{
    public class SearchAndReplaceToolTests
    {
        [Fact]
        public async Task RunToolAsync_ReplacesContentInFile()
        {
            // Arrange
            var tool = new SearchAndReplaceTool();
            var testFile = "test_search_replace.txt";
            var originalContent = "Hello World";
            var searchText = "World";
            var replaceText = "Universe";

            await File.WriteAllTextAsync(testFile, originalContent);

            // Act
            var result = await tool.RunToolAsync(testFile, searchText, replaceText);

            // Assert
            Assert.True(result);
            var fileContent = await File.ReadAllTextAsync(testFile);
            Assert.Equal("Hello Universe", fileContent);

            // Cleanup
            File.Delete(testFile);
        }
    }
}
