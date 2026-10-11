
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using K_Sweet_Kreations_Co_Backend_API.Dtos;
using Xunit;

namespace K_Sweet_Kreations_Co_Tests
{
    public class EnquiryValidationTests
    {
        private static List<ValidationResult> Validate(
            CreateEnquiryDto dto)
        {
            var results = new List<ValidationResult>();

            var context = new ValidationContext(dto);

            Validator.TryValidateObject(
                dto,
                context,
                results,
                validateAllProperties: true);

            return results;
        }

        [Fact]
        public void ValidEnquiry_ShouldPassValidation()
        {
            // Arrange
            var dto = new CreateEnquiryDto
            {
                Name = "Test Customer",
                Email = "customer@example.com",
                Subject = "Birthday Cake Enquiry",
                Message = "I would like to order a birthday cake."
            };

            // Act
            var errors = Validate(dto);

            // Assert
            Assert.Empty(errors);
        }

        [Fact]
        public void InvalidEmail_ShouldFailValidation()
        {
            // Arrange
            var dto = new CreateEnquiryDto
            {
                Name = "Test Customer",
                Email = "invalid-email",
                Subject = "Birthday Cake Enquiry",
                Message = "I would like to order a birthday cake."
            };

            // Act
            var errors = Validate(dto);

            // Assert
            Assert.Contains(errors, error =>
                error.MemberNames.Contains(nameof(dto.Email)));
        }

        [Fact]
        public void ShortMessage_ShouldFailValidation()
        {
            // Arrange
            var dto = new CreateEnquiryDto
            {
                Name = "Test Customer",
                Email = "customer@example.com",
                Subject = "Birthday Cake Enquiry",
                Message = "Hi"
            };

            // Act
            var errors = Validate(dto);

            // Assert
            Assert.Contains(errors, error =>
                error.MemberNames.Contains(nameof(dto.Message)));
        }
    }
}
