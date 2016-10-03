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
            var gpxFiles = new List<string>
            {
                @"..\..\..\GPXFiles\activity_1358503471.gpx",
                @"..\..\..\GPXFiles\Morning_Ride.gpx",
                @"..\..\..\GPXFiles\Evening_Run.gpx",
                @"..\..\..\GPXFiles\Morning_Run.gpx",
                @"..\..\..\GPXFiles\Cone Peak.gpx"
            };

            var serializer = new XmlSerializer(typeof(DeviceFile));
            var deviceFiles = gpxFiles.ToDictionary(f => Path.GetFileNameWithoutExtension(f), f => DeviceFile.FromGpx(f, lapIntervalMeters));
            deviceFiles.Keys.ToList().ForEach(k =>
            {
                var first = true;
                var idx = 2;
                deviceFiles[k].ToList().ForEach(file =>
                {
                    var filenameSuffix = first ? string.Empty : (idx++).ToString();
                    var outputFile = Path.Combine(outputDir, $"{k}{filenameSuffix}.xml");

                    using (TextWriter writer = new StreamWriter(outputFile))
                    {
                        serializer.Serialize(writer, file);
                    }

                    first = false;
                });
            });

            Console.WriteLine("\n\nPress any key to continue...");
            Console.ReadKey();
        }
    }
}
