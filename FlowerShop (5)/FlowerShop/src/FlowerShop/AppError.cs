namespace FlowerShop;

public enum ErrorKind { Domain, Operational }

public sealed record AppError(ErrorKind Kind, string Code, string Message)
{
    public static AppError Domain(string code, string message) => new(ErrorKind.Domain, code, message);

    public static AppError Operational(string code, string message) => new(ErrorKind.Operational, code, message);
}
