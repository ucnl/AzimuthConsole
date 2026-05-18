// AzimuthConsole/Commands/CommandResult.cs
namespace AzimuthConsole.Commands
{
    public class CommandResult
    {
        public bool Success { get; init; }
        public string? ErrorMessage { get; init; }
        public Dictionary<string, string> Data { get; init; } = new();

        public static CommandResult Ok() => new() { Success = true };
        public static CommandResult Ok(string key, string value) =>
            new() { Success = true, Data = new() { [key] = value } };
        public static CommandResult Ok(Dictionary<string, string> data) =>
            new() { Success = true, Data = data };
        public static CommandResult Error(string msg) =>
            new() { Success = false, ErrorMessage = msg };

        public static CommandResult Help(Dictionary<string, string> commands)
        {
            return new CommandResult { Success = true, Data = commands };
        }

        public override string ToString()
        {
            if (!Success)
                return $"ERR,msg={ErrorMessage}";

            if (Data.Count == 0) return "OK";

            var parts = new List<string> { "OK" };
            foreach (var kvp in Data)
                parts.Add($"{kvp.Key}={kvp.Value}");
            return string.Join(",", parts);
        }
    }
}