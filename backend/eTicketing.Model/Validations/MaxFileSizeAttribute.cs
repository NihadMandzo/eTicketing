using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace eTicketing.Model.Validations;

public class MaxFileSizeAttribute : ValidationAttribute
{
    private readonly int _maxFileSize;

    public MaxFileSizeAttribute(int maxFileSize)
    {
        _maxFileSize = maxFileSize;
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is IFormFile file)
        {
            if (file.Length > _maxFileSize)
            {
                return new ValidationResult(GetErrorMessage());
            }
        }

        return ValidationResult.Success;
    }

    public string GetErrorMessage()
    {
        return $"Maksimalna dozvoljena veličina fajla je {_maxFileSize / (1024 * 1024)} MB.";
    }
}
