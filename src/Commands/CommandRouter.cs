// AzimuthConsole/Commands/CommandRouter.cs
using System.Text;
using System.Text.Json;

namespace AzimuthConsole.Commands
{
    public class CommandRouter
    {
        public delegate Task<CommandResult> CommandHandler(
            Dictionary<string, string> args, ICommandContext ctx);

        private readonly Dictionary<string, CommandHandler> _handlers = new();
        private readonly Dictionary<string, CommandMeta> _meta = new();

        public event Action<string>? OnLog;

        public void Register(CommandMeta meta, CommandHandler handler)
        {
            var id = meta.Id.ToUpper();
            _handlers[id] = handler;
            _meta[id] = meta;
        }

        public async Task ProcessAsync(string line, ICommandContext ctx)
        {
            if (string.IsNullOrWhiteSpace(line)) return;

            line = line.Trim();
            OnLog?.Invoke($"[{ctx.SourceId}] >> {line}");

            var parts = line.Split(',');
            var cmdId = parts[0].ToUpper();

            if (!_handlers.TryGetValue(cmdId, out var handler))
            {
                var response = $"{cmdId},ERR,msg=unknown command";
                OnLog?.Invoke($"[{ctx.SourceId}] << {response}");
                await ctx.SendResponseAsync(cmdId, CommandResult.Error("unknown command"));
                return;
            }

            var args = ParseArgs(parts.Length > 1 ? parts[1..] : Array.Empty<string>());

            try
            {
                var result = await handler(args, ctx);
                var response = $"{cmdId},{result}";
                OnLog?.Invoke($"[{ctx.SourceId}] << {response}");
                await ctx.SendResponseAsync(cmdId, result);
            }
            catch (Exception ex)
            {
                var result = CommandResult.Error(ex.Message);
                var response = $"{cmdId},ERR,msg={ex.Message}";
                OnLog?.Invoke($"[{ctx.SourceId}] << {response}");
                await ctx.SendResponseAsync(cmdId, result);
            }
        }

        public string GetHelp(string? cmdId = null)
        {
            if (cmdId != null && _meta.TryGetValue(cmdId.ToUpper(), out var meta))
                return $"{cmdId.ToUpper()}: {meta.Parameters} - {meta.Description}";

            var sb = new StringBuilder();
            sb.AppendLine("Available commands:");
            foreach (var cmd in _meta.Values.OrderBy(m => m.Id))
            {
                sb.AppendLine($"  {cmd.Id}: {cmd.Parameters} - {cmd.Description}");
            }
            return sb.ToString().TrimEnd();
        }

        public string ExportMarkdown(string title = "Command Reference")
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# {title}");
            sb.AppendLine();

            var categories = _meta.Values
                .GroupBy(m => m.Category)
                .OrderBy(g => g.Key);

            foreach (var category in categories)
            {
                sb.AppendLine($"## {category.Key}");
                sb.AppendLine();
                sb.AppendLine("| Command | Sources | Parameters | Response | Description |");
                sb.AppendLine("|---------|---------|------------|----------|-------------|");

                foreach (var cmd in category.OrderBy(c => c.Id))
                {
                    var id = cmd.Id;
                    var src = cmd.Sources;
                    var prm = cmd.Parameters.Replace("|", "\\|");
                    var rsp = cmd.Response.Replace("|", "\\|");
                    var dsc = cmd.Description.Replace("|", "\\|");

                    sb.AppendLine($"| {id} | {src} | {prm} | {rsp} | {dsc} |");
                }

                sb.AppendLine();
            }

            sb.AppendLine("---");
            sb.AppendLine("*Sources: T=Terminal, R=RCTRL (UDP), W=Web*");

            return sb.ToString();
        }

        private static Dictionary<string, string> ParseArgs(string[] parts)
        {
            var dict = new Dictionary<string, string>();
            for (int i = 0; i < parts.Length; i++)
            {
                var chunk = parts[i];
                var eqIdx = chunk.IndexOf('=');
                if (eqIdx > 0)
                {
                    var key = chunk[..eqIdx].Trim().ToLower();
                    var value = chunk[(eqIdx + 1)..].Trim();
                    dict[key] = value;
                }
                else
                {
                    dict[i.ToString()] = chunk.Trim();
                }
            }
            return dict;
        }

        // В класс CommandRouter добавить:

        public string GetHelpJson()
        {
            var commands = _meta.Values
                .OrderBy(m => m.Category)
                .ThenBy(m => m.Id)
                .Select(m => new
                {
                    id = m.Id,
                    category = m.Category,
                    sources = m.Sources,
                    parameters = m.Parameters,
                    response = m.Response,
                    description = m.Description,
                    paramsParsed = ParseParamsForHelp(m.Parameters)
                });

            return JsonSerializer.Serialize(
                new { commands },
                new JsonSerializerOptions { WriteIndented = false }
            );
        }

        private static List<object> ParseParamsForHelp(string parameters)
        {
            if (string.IsNullOrEmpty(parameters) || parameters == "-")
                return new List<object>();

            return parameters
                .Split(',')
                .Select(p => p.Trim())
                .Where(p => p.Contains('='))
                .Select(p =>
                {
                    var eqIdx = p.IndexOf('=');
                    var name = p[..eqIdx].Trim();
                    var type = p[(eqIdx + 1)..].Trim();
                    return (object)new { name, type };
                })
                .ToList();
        }
    }
}