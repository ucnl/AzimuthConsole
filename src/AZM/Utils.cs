using System.Text;
using UCNLNav;

namespace AzimuthConsole.AZM
{
    public static class Utils
    {
        public static void AppendAgingValue(StringBuilder sb, IAging avalue)
        {
            if (avalue.IgnoreAge)
            {
                if (avalue.IsInitializedAndNotObsolete)
                {
                    sb.Append(FormattableString.Invariant($"{avalue},"));
                }
                else
                {
                    sb.Append(",");
                }
            }
            else
            {
                if (avalue.IsInitializedAndNotObsolete)
                {
                    sb.Append(FormattableString.Invariant($"{avalue},{avalue.Age.TotalSeconds:F1},"));
                }
                else
                {
                    sb.Append(",,");
                }
            }
        }

        public static void AppendAgingValueDesciption(StringBuilder sb, IAging value)
        {
            sb.AppendFormat("{0},", value.Name);
            if (!value.IgnoreAge)
                sb.Append("age,");
        }
    }
}
