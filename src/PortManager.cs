// AzimuthConsole/PortManager.cs
using System.Net;
using AzimuthConsole.Commands;
using UCNLDrivers;
using UCNLDrivers.uAux;

namespace AzimuthConsole
{
    public class PortManager
    {
        private readonly AuxManager _aux;
        private IPEndPoint? _rctrlInEndpoint;
        private IPEndPoint? _rctrlOutEndpoint;

        public event Action<string, BaudRate>? OnSerialOutputChanged;
        public event Action<IPEndPoint?>? OnUdpOutputChanged;
        public event Action<int, IPEndPoint?>? OnBeaconUdpChanged;
        public event Action<IPEndPoint>? OnRctrlInChanged;
        public event Action<IPEndPoint>? OnRctrlOutChanged;
        public event Action? OnRctrlDisabled;

        public PortManager(AuxManager aux)
        {
            _aux = aux;
        }

        public async Task<CommandResult> ConfigureAsync(string id, Dictionary<string, string> args)
        {
            if (args.Count == 0)
                return await QueryAsync(id);

            return id switch
            {
                "azm" => ConfigureAzm(args),
                "aux1" => ConfigureAux1(args),
                "aux2" => ConfigureAux2(args),
                "rdt" => ConfigureRdt(args),
                "outs" => ConfigureOuts(args),
                "outu" => ConfigureOutu(args),
                "sioc" => ConfigureSioc(args),
                "rctrl" => ConfigureRctrl(args),
                _ => CommandResult.Error($"unknown port: {id}")
            };
        }

        #region Query

        private Task<CommandResult> QueryAsync(string id)
        {
            switch (id)
            {
                case "sioc":
                    return Task.FromResult(QuerySioc());
                case "rctrl":
                    return Task.FromResult(QueryRctrl());
                case "outu":
                    return Task.FromResult(QueryOutu());
                default:
                    return Task.FromResult(QueryPort(id));
            }
        }

        private CommandResult QueryPort(string id)
        {
            var source = _aux.GetSource(id);
            if (source == null)
                return CommandResult.Ok(new Dictionary<string, string>
                {
                    ["id"] = id,
                    ["status"] = "NotRegistered"
                });

            var data = new Dictionary<string, string>
            {
                ["id"] = id,
                ["status"] = source.Status.ToString()
            };
            if (source.PortName != null) data["port"] = source.PortName;
            return CommandResult.Ok(data);
        }

        private CommandResult QuerySioc()
        {
            var data = new Dictionary<string, string>();
            data["id"] = "sioc";
            data["status"] = "OK";
            // Каналы маяков хранятся в OutputSettings, здесь просто заглушка
            return CommandResult.Ok(data);
        }

        private CommandResult QueryRctrl()
        {
            var data = new Dictionary<string, string>
            {
                ["id"] = "rctrl",
                ["in"] = _rctrlInEndpoint?.ToString() ?? "OFF",
                ["out"] = _rctrlOutEndpoint?.ToString() ?? "OFF",
                ["status"] = _rctrlInEndpoint != null || _rctrlOutEndpoint != null ? "Active" : "Inactive"
            };
            return CommandResult.Ok(data);
        }

        private CommandResult QueryOutu()
        {
            var data = new Dictionary<string, string>
            {
                ["id"] = "outu",
                ["status"] = "OK"
            };
            return CommandResult.Ok(data);
        }

        #endregion

        #region AZM

        private CommandResult ConfigureAzm(Dictionary<string, string> args)
        {
            var port = args.GetValueOrDefault("port", "AUTO");
            var baud = args.GetValueOrDefault("baud", "9600");

            if (port?.ToUpper() == "OFF")
            {
                _aux.Deactivate("azm");
                return CommandResult.Ok();
            }

            var source = _aux.GetSource("azm");
            if (source is uAuxPort p)
            {
                p.ProposedPortName = port?.ToUpper() == "AUTO" ? string.Empty : port;                
                return CommandResult.Ok();
            }

            return CommandResult.Error("azm port not initialized");
        }

        #endregion

        #region AUX1

        private CommandResult ConfigureAux1(Dictionary<string, string> args)
        {
            var proto = args.GetValueOrDefault("proto", "NMEA").ToUpper();
            var port = args.GetValueOrDefault("port", "AUTO");
            var baud = args.GetValueOrDefault("baud", "9600");

            if (port?.ToUpper() == "OFF")
            {
                _aux.Deactivate("aux1");
                return CommandResult.Ok();
            }

            _aux.Remove("aux1");

            uAuxPort source = proto switch
            {
                "BP" => new uAuxBPPort("aux1", (BaudRate)int.Parse(baud!)),
                _ => new uAuxGNSSPort("aux1", (BaudRate)int.Parse(baud!))
            };

            source.ProposedPortName = port?.ToUpper() == "AUTO" ? string.Empty : port;
            source.IsTryAlways = true;
            source.IsLogIncoming = true;

            _aux.Register(source);            
            return CommandResult.Ok();
        }

        #endregion

        #region AUX2

        private CommandResult ConfigureAux2(Dictionary<string, string> args)
        {
            var port = args.GetValueOrDefault("port", "AUTO");
            var baud = args.GetValueOrDefault("baud", "9600");

            if (port?.ToUpper() == "OFF")
            {
                _aux.Deactivate("aux2");
                return CommandResult.Ok();
            }

            _aux.Remove("aux2");
            var source = new uAuxGNSSPort("aux2", (BaudRate)int.Parse(baud!))
            {
                ProposedPortName = port?.ToUpper() == "AUTO" ? string.Empty : port,
                Mode = GNSSMode.CompassOnly,
                IsTryAlways = true,
                IsLogIncoming = true
            };

            _aux.Register(source);            
            return CommandResult.Ok();
        }

        #endregion

        #region RDT (поворотка)

        private CommandResult ConfigureRdt(Dictionary<string, string> args)
        {
            var port = args.GetValueOrDefault("port", "AUTO");
            var baud = args.GetValueOrDefault("baud", "9600");

            if (port?.ToUpper() == "OFF")
            {
                _aux.Deactivate("rdt");
                return CommandResult.Ok();
            }

            _aux.Remove("rdt");

            var source = new uAuxRadantPort("rdt", (BaudRate)int.Parse(baud!))
            {
                ProposedPortName = port?.ToUpper() == "AUTO" ? string.Empty : port,
                IsTryAlways = true,
                IsLogIncoming = true,
                IsRawModeOnly = true
            };

            _aux.Register(source);            
            return CommandResult.Ok();
        }

        #endregion

        #region OUTS (serial output)

        private CommandResult ConfigureOuts(Dictionary<string, string> args)
        {
            var port = args.GetValueOrDefault("port", "");
            var baudStr = args.GetValueOrDefault("baud", "9600");

            if (string.IsNullOrEmpty(port) || port.ToUpper() == "OFF")
            {
                OnSerialOutputChanged?.Invoke("OFF", BaudRate.baudRate9600);
                return CommandResult.Ok();
            }

            if (!int.TryParse(baudStr, out var baudInt))
                return CommandResult.Error("invalid baud rate");

            OnSerialOutputChanged?.Invoke(port, (BaudRate)baudInt);
            return CommandResult.Ok();
        }

        #endregion

        #region OUTU (UDP broadcast output)

        private CommandResult ConfigureOutu(Dictionary<string, string> args)
        {
            var addr = args.GetValueOrDefault("addr", "");

            if (string.IsNullOrEmpty(addr) || addr.ToUpper() == "OFF")
            {
                OnUdpOutputChanged?.Invoke(null);
                return CommandResult.Ok();
            }

            if (!IPEndPoint.TryParse(addr, out var ep))
                return CommandResult.Error("invalid endpoint format, use ip:port");

            OnUdpOutputChanged?.Invoke(ep);
            return CommandResult.Ok();
        }

        #endregion

        #region SIOC (индивидуальный UDP для маяка)

        private CommandResult ConfigureSioc(Dictionary<string, string> args)
        {
            if (!args.TryGetValue("addr", out var addrStr))
                return CommandResult.Error("usage: SIOC,addr=N,ep=ip:port");

            if (!int.TryParse(addrStr, out var addr))
                return CommandResult.Error("invalid beacon address");

            var epStr = args.GetValueOrDefault("ep", "OFF");

            if (epStr.ToUpper() == "OFF")
            {
                OnBeaconUdpChanged?.Invoke(addr, null);
                return CommandResult.Ok();
            }

            if (!IPEndPoint.TryParse(epStr, out var ep))
                return CommandResult.Error("invalid endpoint format, use ip:port");

            OnBeaconUdpChanged?.Invoke(addr, ep);
            return CommandResult.Ok();
        }

        #endregion

        #region RCTRL (удалённое управление)

        private CommandResult ConfigureRctrl(Dictionary<string, string> args)
        {
            var inStr = args.GetValueOrDefault("in", "");
            var outStr = args.GetValueOrDefault("out", "");

            // OFF — отключить всё
            if (inStr.ToUpper() == "OFF" || (string.IsNullOrEmpty(inStr) && !string.IsNullOrEmpty(outStr) && outStr.ToUpper() == "OFF"))
            {
                _rctrlInEndpoint = null;
                _rctrlOutEndpoint = null;
                OnRctrlDisabled?.Invoke();
                return CommandResult.Ok();
            }

            // IN
            if (!string.IsNullOrEmpty(inStr) && int.TryParse(inStr, out var inPort))
            {
                _rctrlInEndpoint = new IPEndPoint(IPAddress.Any, inPort);
                OnRctrlInChanged?.Invoke(_rctrlInEndpoint);
            }

            // OUT
            if (!string.IsNullOrEmpty(outStr) && IPEndPoint.TryParse(outStr, out var outEp))
            {
                _rctrlOutEndpoint = outEp;
                OnRctrlOutChanged?.Invoke(_rctrlOutEndpoint);
            }

            return CommandResult.Ok();
        }

        #endregion

        #region Helpers

        public IEnumerable<string> GetAllInfo()
        {
            foreach (var info in _aux.GetAllSources())
                yield return $"{info.Id}|{info.PortName ?? "AUTO"}|{info.Status}";

            var rctrlStatus = _rctrlInEndpoint != null || _rctrlOutEndpoint != null
                ? AuxStatus.Detected : AuxStatus.Inactive;
            yield return $"rctrl|IN:{_rctrlInEndpoint?.Port.ToString() ?? ""}|OUT:{_rctrlOutEndpoint}|{rctrlStatus}";
        }

        #endregion
    }
}