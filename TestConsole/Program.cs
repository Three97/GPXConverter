using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace TestConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            //var gpxFile = @"C:\DEV\GitHub\GPXConverter\GPXFiles\activity_1358503471.gpx";
            var gpxFile = @"C:\DEV\GitHub\GPXConverter\GPXFiles\Evening_Run.gpx";
            var test = DeviceFile.FromGpx(gpxFile);

            Console.WriteLine("\n\nPress any key to continue...");
            Console.ReadKey();
        }
    }
}
