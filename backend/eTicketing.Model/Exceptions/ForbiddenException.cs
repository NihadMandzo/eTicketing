namespace eTicketing.Model.Exceptions;

/// <summary>
/// Exception thrown when a user is authenticated but doesn't have permission to perform an action (HTTP 403)
/// </summary>
public class ForbiddenException : Exception
{
    public ForbiddenException(string message) : base(message)
    {
    }

    public ForbiddenException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
