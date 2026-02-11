using System.Globalization;
using System.Text;
using static AzimuthConsole.Command;

namespace AzimuthConsole
{
    public sealed record Command(
        string Id,
        string ParametersDescriptor,
        CommandHandlerDelegate CommandHandler,
        string Description)
    {
        public delegate bool CommandHandlerDelegate(params object[] args);

        public override string ToString() => $"{Id}: {Description}";
    }

    public sealed class CmdProcessor
    {
        readonly Dictionary<string, Command> commands;

        public event Action<string>? OutputMessageHandler;


        public CmdProcessor()
        {
            commands = [];
        }

        private static Dictionary<string, Func<string, object>> parsers = new()
        {
            { "x", x => int.Parse(x) },
            { "x.x", x => double.Parse(x, CultureInfo.InvariantCulture) },
            { "c--c", x => x },
        };

        private static Dictionary<string, Func<object, string>> formatters = new()
        {
            { "x", x => ((int)(x)).ToString() },
            { "x.x", x => ((double)x).ToString("F06", CultureInfo.InvariantCulture).TrimEnd(['0']) },
            { "c--c", x => x.ToString() },
        };

        public string GetCommandsDescriptions()
        {
            StringBuilder sb = new();

            foreach (var item in commands.Values)
            {
                sb.Append(item.ToString());
                sb.Append(Environment.NewLine);
            }

            return sb.ToString();
        }

        public bool IsCommandPresent(string cmdID)
        {
            return commands.ContainsKey(cmdID);
        }

        public void OnOutput(string line)
        {
            OutputMessageHandler?.Invoke(line);
        }


        public Command AddCommand(Command cmd)
        {
            return AddCommand(cmd.Id, cmd.ParametersDescriptor, cmd.CommandHandler, cmd.Description);
        }

        public Command AddCommand(string cmdID, string parameters_descriptor, CommandHandlerDelegate cmdHandler, string description)
        {
            if (!IsCommandPresent(cmdID))
            {
                var newCmd = new Command(cmdID, parameters_descriptor, cmdHandler, description);
                commands.Add(cmdID, newCmd);
                return newCmd;
            }
            else
            {
                throw new KeyNotFoundException(cmdID);
            }
        }

        public void RemoveCommand(string cmdID)
        {
            if (IsCommandPresent(cmdID))
            {
                commands.Remove(cmdID);
            }
            else
            {
                throw new KeyNotFoundException(cmdID);
            }
        }

        public void Process(string line)
        {
            if (line.StartsWith('#')) return;

            var splits = line.Split(',');
            if (splits.Length > 0)
            {
                var cmdID = splits[0].ToUpper();                

                if (IsCommandPresent(cmdID))
                {
                    if (string.IsNullOrEmpty(commands[cmdID].ParametersDescriptor))
                    {
                        commands[cmdID].CommandHandler(null);
                        OutputMessageHandler?.Invoke($"\"{line}\" OK");
                    }
                    else
                    {
                        var descriptors = commands[cmdID].ParametersDescriptor.Split(',');
                        if (descriptors.Length == splits.Length - 1)
                        {
                            List<object> args = [];

                            bool isOK = true;
                            int wrongParamIdx = -1;

                            for (int i = 0; (i < descriptors.Length) && isOK; i++)
                            {
                                if (parsers.ContainsKey(descriptors[i]))
                                {
                                    try
                                    {
                                        if (string.IsNullOrEmpty(splits[i + 1]))
                                            args.Add(null);
                                        else
                                            args.Add(parsers[descriptors[i]](splits[i + 1]));
                                    }
                                    catch (Exception)
                                    {
                                        isOK = false;
                                        wrongParamIdx = i;
                                    }
                                }
                                else
                                {
                                    isOK = false;
                                    wrongParamIdx = i;
                                }
                            }

                            if (isOK)
                            {
                                var cmd_result = commands[cmdID].CommandHandler([.. args]);
                                OutputMessageHandler?.Invoke($"\"{line}\" {(cmd_result ? "OK" : "Failed")}");
                            }
                            else
                            {
                                OutputMessageHandler?.Invoke($"'{cmdID}' Syntax error: parameter {wrongParamIdx} wrong format. Type \'Help\' for supported commands list");
                            }
                        }
                        else
                        {
                            OutputMessageHandler?.Invoke($"'{cmdID}' Syntax error: wrong paramters number. Type \'Help\' for supported commands list");
                        }
                    }
                }
                else
                {
                    OutputMessageHandler?.Invoke($"\'{cmdID}\' - Unknown command. Type \'Help\' for supported commands list");
                }
            }
        }
    }
}
