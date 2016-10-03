using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace TestConsole
{
    using System.IO;
    using System.Xml.Serialization;

    class Program
    {
        static void Main(string[] args)
        {
            const string outputDir = @"..\..\..\Output";
            decimal lapIntervalMeters = 1000;
            //var gpxFile = @"C:\DEV\GitHub\GPXConverter\GPXFiles\activity_1358503471.gpx";
            //var gpxFile = @"C:\DEV\GitHub\GPXConverter\GPXFiles\Evening_Run.gpx";
            //var gpxFile = @"C:\DEV\GitHub\GPXConverter\GPXFiles\Morning_Ride.gpx";
            //var gpxFile = @"..\..\..\GPXFiles\Morning_Ride.gpx";
            var gpxFile = @"..\..\..\GPXFiles\Evening_Run.gpx";

            var files = DeviceFile.FromGpx(gpxFile, lapIntervalMeters);

            var serializer = new XmlSerializer(typeof(DeviceFile));
            var idx = 1;
            files.ForEach(
                file =>
                    {
                        var outputFile = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(gpxFile) + (idx++) + ".xml");
                        using (TextWriter writer = new StreamWriter(outputFile))
                        {
                            serializer.Serialize(writer, file);
                        }
                    });

            Console.WriteLine("\n\nPress any key to continue...");
            Console.ReadKey();
        }
    }
}
