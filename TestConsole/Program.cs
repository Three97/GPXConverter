using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            var gpxFile = @"C:\Temp\GPXFiles\activity_1358503471.gpx";
            var test = DeviceFile.FromGpx(gpxFile);

            Console.WriteLine("\n\nPress any key to continue...");
            Console.ReadKey();
        }
    }
}
