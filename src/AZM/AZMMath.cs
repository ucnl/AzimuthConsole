using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UCNLNav;

namespace AzimuthConsole.AZM
{
    public class AZMMath
    {
        /// <summary>
        /// Solves direct geodetic problem: calculates absolute (geographic) location of a point accodring to specified base point, 
        /// distance and azimuth angle using Vincenty equations, and if it doesn't converges, using haversine equation.
        /// </summary>
        /// <param name="olat_rad">Base point lat, radians</param>
        /// <param name="olon_rad">Base point lon, radians</param>
        /// <param name="azm_rad">Azimuth angle, radians (clockwise from the north direction)</param>
        /// <param name="dst_m">Distance, meters</param>
        /// <param name="rlat_rad">Calculated latitude, radians</param>
        /// <param name="rlon_rad">Calculated longitude, radians</param>
        /// <param name="razm_rad">Reverse azimuth, radians</param>
        public static void CalculateAbsLocationDirectGeodetic(double olat_rad, double olon_rad,
            double azm_rad, double dst_m,
            out double rlat_rad, out double rlon_rad, out double razm_rad)
        {
            if (!Algorithms.VincentyDirect(olat_rad, olon_rad, azm_rad, dst_m,
                Algorithms.WGS84Ellipsoid,
                Algorithms.VNC_DEF_EPSILON, Algorithms.VNC_DEF_IT_LIMIT,
                out rlat_rad, out rlon_rad, out razm_rad, out _))
            {
                Algorithms.HaversineDirect(olat_rad, olon_rad, dst_m, azm_rad,
                    Algorithms.WGS84Ellipsoid.MajorSemiAxis_m,
                    out rlat_rad, out rlon_rad);

                razm_rad = Algorithms.Wrap2PI(azm_rad + Math.PI);
            }
        }

        /// <summary>
        /// All angles clockwise from the North direction
        /// </summary>
        /// <param name="heading_deg">Compass reading, 0-360° clockwise from North direction</param>
        /// <param name="phi_deg">Antenna - comрass zero directions difference, °</param>
        /// <param name="bearing_deg">Bearing to a responder, 0-360° clockwise from North direction</param>
        /// <param name="r_m">slant range projection, m</param>
        /// <param name="xt">transversal GNSS/antenna offset</param>
        /// <param name="yt">longitudal GNSS/antenna offset</param>
        /// <param name="a_deg">Absolute azimuth to the responder</param>
        /// <param name="r_a">Range to the responder (from the GNSS position)</param>
        public static void PolarCS_ShiftRotate(double heading_deg, double phi_deg, double bearing_deg,
            double r_m, double xt, double yt,
            out double a_deg, out double r_a)
        {
            double teta = Algorithms.Wrap2PI(Algorithms.Deg2Rad(bearing_deg + phi_deg));

            double xr = xt + r_m * Math.Sin(teta);
            double yr = yt + r_m * Math.Cos(teta);

            r_a = Math.Sqrt(xr * xr + yr * yr);

            double a_r = Math.Atan2(xr, yr);
            if (a_r < 0)
                a_r += 2 * Math.PI;

            a_r += Algorithms.Deg2Rad(heading_deg);
            a_r = Algorithms.Wrap2PI(a_r);

            a_deg = Algorithms.Rad2Deg(a_r);
        }

        /// <summary>
        /// Tries to calculate a slant range projection by the slant range and two depths
        /// </summary>
        /// <param name="dpt1"></param>
        /// <param name="dpt2"></param>
        /// <param name="srange"></param>
        /// <returns></returns>
        public static double TryCalculateSlantRangeProjection(double dpt1, double dpt2, double srange)
        {
            double d_dpt = Math.Abs(dpt1 - dpt2);
            if (d_dpt < srange)
                return Math.Sqrt(srange * srange - d_dpt * d_dpt);
            else
                return srange;
        }


    }
}
