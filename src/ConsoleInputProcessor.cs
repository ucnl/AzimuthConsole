using System.Text;

public class ConsoleInputProcessor : IAsyncDisposable, IDisposable
{
    private readonly StringBuilder _inputBuffer = new();
    private readonly CancellationTokenSource _cts = new();
    private bool _disposed;
    private string cmdToEmulate = string.Empty;

    private readonly Dictionary<ConsoleKeyInfo, (Action action, string description)> _hotkeys = new();

    public void RegisterHotkey(ConsoleKeyInfo keyInfo, Action action, string description)
    {
        var _ki = new ConsoleKeyInfo('\0', keyInfo.Key,
            keyInfo.Modifiers.HasFlag(ConsoleModifiers.Shift),
            keyInfo.Modifiers.HasFlag(ConsoleModifiers.Alt),
            keyInfo.Modifiers.HasFlag(ConsoleModifiers.Control));

        _hotkeys[_ki] = (action, description);
    }

    private static ConsoleKeyInfo DiscardKeyChar(ConsoleKeyInfo keyInfo)
    {
        return new ConsoleKeyInfo('\0', keyInfo.Key,
            keyInfo.Modifiers.HasFlag(ConsoleModifiers.Shift),
            keyInfo.Modifiers.HasFlag(ConsoleModifiers.Alt),
            keyInfo.Modifiers.HasFlag(ConsoleModifiers.Control));
    }

    public void EmulateCommand(string? line)
    {
        cmdToEmulate = line ?? string.Empty;
    }

    public async Task<string?> ReadCommandAsync(CancellationToken cancellationToken = default)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);

        while (!linkedCts.IsCancellationRequested)
        {
            if (!string.IsNullOrEmpty(cmdToEmulate))
            {
                var cmd = cmdToEmulate;
                cmdToEmulate = string.Empty;
                return cmd;
            }

            if (Console.KeyAvailable)
            {
                var keyInfo = Console.ReadKey(true);
                var _ki = DiscardKeyChar(keyInfo);

                if (_hotkeys.TryGetValue(_ki, out var hotkey))
                {
                    hotkey.action.Invoke();
                    continue;
                }

                switch (keyInfo.Key)
                {
                    case ConsoleKey.Enter:
                        var cmd = _inputBuffer.ToString();
                        _inputBuffer.Clear();
                        Console.WriteLine();
                        return cmd;

                    case ConsoleKey.Backspace when _inputBuffer.Length > 0:
                        _inputBuffer.Remove(_inputBuffer.Length - 1, 1);
                        Console.Write("\b \b");
                        break;

                    default:
                        if (!char.IsControl(keyInfo.KeyChar))
                        {
                            _inputBuffer.Append(keyInfo.KeyChar);
                            Console.Write(keyInfo.KeyChar);
                        }
                        break;
                }
            }

            await Task.Delay(10, linkedCts.Token).ConfigureAwait(false);
        }

        return null;
    }

    public string GetHotkeysDescription()
    {
        if (_hotkeys.Count == 0)
            return "No hotkeys descriptions.";

        var sb = new StringBuilder();
        sb.AppendLine("Available hotkeys:");

        foreach (var kvp in _hotkeys)
        {
            var keyInfo = kvp.Key;
            string modifiers = GetModifiersString(keyInfo);
            string keyName = keyInfo.KeyChar != '\0'
                ? keyInfo.KeyChar.ToString()
                : keyInfo.Key.ToString();
            string description = kvp.Value.description;

            sb.AppendLine($"  {modifiers}{keyName} — {description}");
        }

        return sb.ToString();
    }

    private static string GetModifiersString(ConsoleKeyInfo keyInfo)
    {
        var parts = new List<string>();

        if (keyInfo.Modifiers.HasFlag(ConsoleModifiers.Shift))
            parts.Add("Shift+");
        if (keyInfo.Modifiers.HasFlag(ConsoleModifiers.Alt))
            parts.Add("Alt+");
        if (keyInfo.Modifiers.HasFlag(ConsoleModifiers.Control))
            parts.Add("Ctrl+");

        return string.Join("", parts);
    }

    public void Cancel()
    {
        _cts.Cancel();
    }

    public void Dispose()
    {
        if (_disposed) return;

        _cts.Cancel();
        _cts.Dispose();

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        await _cts.CancelAsync();
        _cts.Dispose();

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}