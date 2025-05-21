
using RankCalculator;

namespace RankCalculatorTests
{
    public class RankCalculatorTests
    {
        [Fact]
        public void CalculateRank_EmptyString_ReturnsZero()
        {
            // Arrange
            // Act -
            double rank = Program.CalculateRank("");
            // Assert - 
            Assert.Equal(0, rank);

        }

        [Fact]
        public void CalculateRank_EmptyString_Spaces_Returns1()
        {
            // Arrange
            // Act -
            double rank = Program.CalculateRank("   ");
            // Assert - 
            Assert.Equal(1, rank);

        }

        [Theory]
        [InlineData("Hello", 0)] 
        [InlineData("ABCD1234", 0.5)] 
        [InlineData("ABСD12", 2.0 / 6)] 
        [InlineData("123!@#", 1)] 
        [InlineData("A👍", 0.5)] 
        [InlineData("ABCdef", 0.0)] 
        [InlineData("🚀ABC", 0.25)] 
        [InlineData("A", 0)] 
        [InlineData("Привет", 0)] 
        [InlineData("a b c а б в ", 0.5)] 
        public void CalculateRank_ValidInput_ReturnsCorrectValue(string text, double expected)
        {
            // Act
            double result = Program.CalculateRank(text);

            // Assert
            Assert.Equal(expected, result);
        }
    }
}