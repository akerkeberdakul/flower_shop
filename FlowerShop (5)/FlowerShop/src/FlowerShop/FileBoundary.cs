using System.Collections.Immutable;

namespace FlowerShop;

public static class FileBoundary
{
    public static Result<ImmutableArray<string>, AppError> ReadLines(string? path, string description)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Failure("invalid_path", $"No path was given for the {description} file.");
        }

        try
        {
            return Result<ImmutableArray<string>, AppError>.Ok(File.ReadAllLines(path).ToImmutableArray());
        }
        catch (FileNotFoundException)
        {
            return Failure("file_not_found", $"The {description} file '{path}' was not found.");
        }
        catch (DirectoryNotFoundException)
        {
            return Failure("file_not_found", $"The folder of the {description} file '{path}' was not found.");
        }
        catch (UnauthorizedAccessException)
        {
            return Failure("access_denied", $"Access to the {description} file '{path}' was denied.");
        }
        catch (IOException ex)
        {
            return Failure("io_error", $"The {description} file '{path}' could not be read: {ex.Message}");
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            return Failure("invalid_path", $"The {description} path '{path}' is not valid: {ex.Message}");
        }
    }

    private static Result<ImmutableArray<string>, AppError> Failure(string code, string message) =>
        Result<ImmutableArray<string>, AppError>.Fail(AppError.Operational(code, message));
}
