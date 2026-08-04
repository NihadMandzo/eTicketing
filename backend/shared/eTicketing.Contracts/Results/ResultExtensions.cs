using Microsoft.AspNetCore.Http;
using Http = Microsoft.AspNetCore.Http.Results;

namespace eTicketing.Contracts.Results;

// Napomena: ovaj namespace se zove "Results", isto kao Microsoft.AspNetCore.Http.Results
// klasa — zato se ta klasa ovdje referencira preko alias-a "Http" da ne dođe do sudara imena.
public static class ResultExtensions
{
    public static IResult ToHttpResult(this Result result, int successStatusCode = StatusCodes.Status200OK)
    {
        if (result.IsSuccess)
            return Http.StatusCode(successStatusCode);

        return ToProblemResult(result.Error);
    }

    public static IResult ToHttpResult<T>(this Result<T> result, int successStatusCode = StatusCodes.Status200OK)
    {
        if (result.IsSuccess)
            return Http.Json(result.Value, statusCode: successStatusCode);

        return ToProblemResult(result.Error);
    }

    private static IResult ToProblemResult(Error error) => error.Type switch
    {
        ErrorType.NotFound => Http.NotFound(new { error.Code, error.Message }),
        ErrorType.Validation => Http.BadRequest(new { error.Code, error.Message }),
        ErrorType.Conflict => Http.Conflict(new { error.Code, error.Message }),
        ErrorType.Unauthorized => Http.Json(new { error.Code, error.Message }, statusCode: StatusCodes.Status403Forbidden),
        _ => Http.Problem(title: error.Message, statusCode: StatusCodes.Status500InternalServerError)
    };
}
