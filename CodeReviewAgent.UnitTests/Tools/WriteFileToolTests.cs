using Xunit;
using System.IO;
using CodeReviewAgent.Tools;

namespace CodeReviewAgent.UnitTests.Tools
{
    public class WriteFileToolTests
    {
        [Fact]
        public async Task RunToolAsync_WritesContentToFile()
        {
            // Arrange
            var tool = new WriteFileTool();
            var testFile = "test_write_file.txt";
            var content = "Test content";

            // Act
            var result = await tool.RunToolAsync(testFile, content);

            // Assert
            Assert.True(result);
            Assert.True(File.Exists(testFile));
            var fileContent = await File.ReadAllTextAsync(testFile);
            Assert.Equal(content, fileContent);

            // Cleanup
            File.Delete(testFile);
        }
    }
}
