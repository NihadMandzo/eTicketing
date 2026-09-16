namespace eTicketing.Contracts.Results;

public enum ErrorType
{
    None,
    Validation,
    NotFound,
    Conflict,
    Unauthorized,
    Failure
}
