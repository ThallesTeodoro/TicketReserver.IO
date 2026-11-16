namespace TR.API.Events.WebApi.Contracts;

public sealed class JsonResponse<TData, TError>
{
    public int StatusCode { get; set; }
    public string Message { get; set; }
    public TData? Data { get; set; }
    public TError? Errors { get; set; }

    public JsonResponse()
    {
        Message = string.Empty;
    }

    public JsonResponse(int statusCode, TData? data, TError? errors, string? message = null)
    {
        StatusCode = statusCode;
        Message = message ?? Microsoft.AspNetCore.WebUtilities.ReasonPhrases.GetReasonPhrase(statusCode) ?? string.Empty;
        Data = data;
        Errors = errors;
    }

    public static JsonResponse<TData, TError> Success(TData data, int statusCode = 200, string? message = null)
    {
        return new JsonResponse<TData, TError>(statusCode, data, default, message);
    }

    public static JsonResponse<TData, TError> Failure(int statusCode, TError errors, string? message = null)
    {
        return new JsonResponse<TData, TError>(statusCode, default, errors, message);
    }
}
