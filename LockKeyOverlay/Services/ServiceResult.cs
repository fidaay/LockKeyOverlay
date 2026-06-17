namespace LockKeyOverlay;

internal readonly record struct ServiceResult(
    bool Succeeded,
    string Message,
    Exception? Exception = null,
    int? NativeErrorCode = null)
{
    public static ServiceResult Success(string message = "OK")
    {
        return new ServiceResult(true, message);
    }

    public static ServiceResult Failure(string message, Exception? exception = null, int? nativeErrorCode = null)
    {
        return new ServiceResult(false, message, exception, nativeErrorCode);
    }

    public string DiagnosticMessage
    {
        get
        {
            string message = Message;

            if (NativeErrorCode.HasValue)
                message += $" Native error: {NativeErrorCode.Value}.";

            if (Exception is not null)
                message += $" {Exception.GetType().Name}: {Exception.Message}";

            return message;
        }
    }
}

internal readonly record struct ServiceResult<T>(
    bool Succeeded,
    T Value,
    string Message,
    Exception? Exception = null,
    int? NativeErrorCode = null)
{
    public static ServiceResult<T> Success(T value, string message = "OK")
    {
        return new ServiceResult<T>(true, value, message);
    }

    public static ServiceResult<T> Failure(T fallbackValue, string message, Exception? exception = null, int? nativeErrorCode = null)
    {
        return new ServiceResult<T>(false, fallbackValue, message, exception, nativeErrorCode);
    }

    public ServiceResult ToServiceResult()
    {
        return Succeeded
            ? ServiceResult.Success(Message)
            : ServiceResult.Failure(Message, Exception, NativeErrorCode);
    }
}

internal sealed class ServiceResultEventArgs : EventArgs
{
    public ServiceResultEventArgs(ServiceResult result)
    {
        Result = result;
    }

    public ServiceResult Result { get; }
}
