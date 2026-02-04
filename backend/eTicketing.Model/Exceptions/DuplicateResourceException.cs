namespace eTicketing.Model.Exceptions;

/// <summary>
/// Exception thrown when attempting to create a resource that already exists
/// </summary>
public class DuplicateResourceException : Exception
{
    public string ResourceType { get; }
    public string ResourceIdentifier { get; }

    public DuplicateResourceException(string message) : base(message)
    {
        ResourceType = string.Empty;
        ResourceIdentifier = string.Empty;
    }

    public DuplicateResourceException(string message, string resourceType, string resourceIdentifier) 
        : base(message)
    {
        ResourceType = resourceType;
        ResourceIdentifier = resourceIdentifier;
    }
}
