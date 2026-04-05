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
        double sizeInMb = _maxFileSize / (1024.0 * 1024.0);
        if (sizeInMb < 1)
        {
            double sizeInKb = _maxFileSize / 1024.0;
            return $"Maksimalna dozvoljena veličina fajla je {sizeInKb:F1} KB.";
        }
        return $"Maksimalna dozvoljena veličina fajla je {sizeInMb:F1} MB.";
    }
}
