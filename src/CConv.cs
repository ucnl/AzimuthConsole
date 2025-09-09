using UCNLDrivers;

namespace AzimuthConsole
{
    public static class CConv
    {
        #region Methods

        public static readonly Func<object, BaudRate, BaudRate> O2Baudrate_D = (o, b) => o == null ? b : (BaudRate)(int)o;
        public static readonly Func<object, string, string> O2S_D = (o, s) => o == null ? s : (string)o;


        public static readonly Func<object, double, double> O2D_D = (o, d) => o == null ? d : (double)o;
        public static readonly Func<object, int, int> O2S32_D = (o, i) => o == null ? i : (int)o;
        public static readonly Func<object, UInt16, UInt16> O2U16_D = (o, u) => o == null ? u : (UInt16)(int)o;

        public static readonly Func<object, string> O2S = (o) => o == null ? string.Empty : (string)o;

        #endregion
    }
}
