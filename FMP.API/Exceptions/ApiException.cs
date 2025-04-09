namespace FMP.API;

public class ApiException : Exception
{
    public string Code { get; }
    public int StatusCode { get; }
    public Dictionary<string, object>? Details { get; }

    public ApiException(string code, string message, int statusCode = 400, Dictionary<string, object>? details = null)
        : base(message)
    {
        Code = code;
        StatusCode = statusCode;
        Details = details;
    }
}
