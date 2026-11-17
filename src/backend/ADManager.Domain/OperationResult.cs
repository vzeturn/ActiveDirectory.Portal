namespace ADManager.Domain;

public class OperationResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }

    public static OperationResult Ok() => new() { Success = true };
    public static OperationResult Fail(string error) => new() { Success = false, Error = error };
}

public sealed class OperationResult<T> : OperationResult
{
    public T? Value { get; init; }

    public static OperationResult<T> Ok(T value) => new() { Success = true, Value = value };
    public static new OperationResult<T> Fail(string error) => new() { Success = false, Error = error };
}
