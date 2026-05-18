// AzimuthConsole/InputSources/TerminalInputSource.cs
using AzimuthConsole.Commands;
using AzimuthConsole.Contexts;
using System.Text;

namespace AzimuthConsole.InputSources
{
    public class TerminalInputSource : IInputSource
    {
        private readonly CancellationTokenSource _cts = new();
        private readonly TerminalContext _context = new();
        private readonly StringBuilder _inputBuffer = new();

        public string SourceId => "terminal";
        public event EventHandler<CommandReceivedEventArgs>? CommandReceived;
        public event Action? OnToggleLogMode;

        public Task StartAsync()
        {
            _ = ReadLoopAsync(_cts.Token);
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            _cts.Cancel();
            return Task.CompletedTask;
        }

        public void PrintPrompt()
        {
            Console.Write("> ");
        }

        private async Task ReadLoopAsync(CancellationToken ct)
        {
            PrintPrompt();

            while (!ct.IsCancellationRequested)
            {
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(true);

                    // F1 — Help
                    if (key.Key == ConsoleKey.F1 && key.Modifiers == 0)
                    {
                        Console.WriteLine();
                        CommandReceived?.Invoke(this, new CommandReceivedEventArgs("HELP", _context));
                        PrintPrompt();
                        continue;
                    }

                    // F12 — переключение режима логирования
                    if (key.Key == ConsoleKey.F12 && key.Modifiers == 0)
                    {
                        OnToggleLogMode?.Invoke();
                        continue;
                    }

                    // Ctrl+L — очистка экрана
                    if (key.Key == ConsoleKey.L && key.Modifiers == ConsoleModifiers.Control)
                    {
                        Console.Clear();
                        _inputBuffer.Clear();
                        PrintPrompt();
                        continue;
                    }

                    // В ReadLoopAsync:

                    // Ctrl+N — OCON
                    if (key.Key == ConsoleKey.N && key.Modifiers == ConsoleModifiers.Control)
                    {
                        Console.WriteLine();
                        CommandReceived?.Invoke(this, new CommandReceivedEventArgs("OCON", _context));
                        PrintPrompt();
                        continue;
                    }

                    // Ctrl+Shift+N — CCON
                    if (key.Key == ConsoleKey.N && key.Modifiers == (ConsoleModifiers.Control | ConsoleModifiers.Shift))
                    {
                        Console.WriteLine();
                        CommandReceived?.Invoke(this, new CommandReceivedEventArgs("CCON", _context));
                        PrintPrompt();
                        continue;
                    }

                    // Ctrl+I — RITG
                    if (key.Key == ConsoleKey.I && key.Modifiers == ConsoleModifiers.Control)
                    {
                        Console.WriteLine();
                        CommandReceived?.Invoke(this, new CommandReceivedEventArgs("RITG", _context));
                        PrintPrompt();
                        continue;
                    }

                    // Ctrl+Shift+I — PITG
                    if (key.Key == ConsoleKey.I && key.Modifiers == (ConsoleModifiers.Control | ConsoleModifiers.Shift))
                    {
                        Console.WriteLine();
                        CommandReceived?.Invoke(this, new CommandReceivedEventArgs("PITG", _context));
                        PrintPrompt();
                        continue;
                    }

                    // Ctrl+E — Exit
                    if (key.Key == ConsoleKey.E && key.Modifiers == ConsoleModifiers.Control)
                    {
                        Console.WriteLine();
                        CommandReceived?.Invoke(this, new CommandReceivedEventArgs("EXIT", _context));
                        PrintPrompt();
                        continue;
                    }

                    // Enter — отправка команды
                    if (key.Key == ConsoleKey.Enter)
                    {
                        Console.WriteLine();
                        var line = _inputBuffer.ToString();
                        _inputBuffer.Clear();

                        if (!string.IsNullOrWhiteSpace(line))
                            CommandReceived?.Invoke(this, new CommandReceivedEventArgs(line, _context));

                        PrintPrompt();
                        continue;
                    }

                    // Backspace
                    if (key.Key == ConsoleKey.Backspace && _inputBuffer.Length > 0)
                    {
                        _inputBuffer.Remove(_inputBuffer.Length - 1, 1);
                        Console.Write("\b \b");
                        continue;
                    }

                    // Обычные символы
                    if (!char.IsControl(key.KeyChar))
                    {
                        _inputBuffer.Append(key.KeyChar);
                        Console.Write(key.KeyChar);
                    }
                }

                await Task.Delay(10, ct);
            }
        }
    }
}