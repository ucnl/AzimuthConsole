using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace AzimuthConsole
{
    internal class AppUtils
    {
        public static string GetApplicationVersion()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var versionAttribute = assembly?.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            return versionAttribute?.InformationalVersion ?? "Unknown";
        }

        public static string GetFullVersionInfo()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var name = assembly?.GetName().Name ?? "Unknown";
            var versionAttribute = assembly?.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            return $"{name} v{(versionAttribute?.InformationalVersion ?? "Unknown")}";
        }
    }
}
