
using Xunit;

namespace K_Sweet_Kreations_Co_Tests
{
    public class UnitTest1
    {
        [Fact]
        public void Addition_TwoNumbers_ReturnsCorrectSum()
        {
            // Arrange
            int number1 = 10;
            int number2 = 5;

            // Act
            int result = number1 + number2;

            // Assert
            Assert.Equal(15, result);
        }
    }
}
