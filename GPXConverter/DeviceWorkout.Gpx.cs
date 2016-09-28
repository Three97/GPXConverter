using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Xml;
using System.Xml.Serialization;
using System.IO;
using System.Text;
//using Dynastream;

/// <summary>
/// Summary description for DeviceWorkout
/// </summary>
public partial class DeviceFile
{
    const double _eQuatorialEarthRadius = 6378.1370D;
    const double _d2r = (Math.PI / 180D);

    public static int HaversineInM(double lat1, double long1, double lat2, double long2)
    {
        return (int)(1000D * HaversineInKM(lat1, long1, lat2, long2));
    }

    public static double HaversineInKM(double lat1, double long1, double lat2, double long2)
    {
        double dlong = (long2 - long1) * _d2r;
        double dlat = (lat2 - lat1) * _d2r;
        double a = Math.Pow(Math.Sin(dlat / 2D), 2D) + Math.Cos(lat1 * _d2r) * Math.Cos(lat2 * _d2r) * Math.Pow(Math.Sin(dlong / 2D), 2D);
        double c = 2D * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1D - a));
        double d = _eQuatorialEarthRadius * c;

        return d;
    }


    public static List<DeviceFile> FromGpx(string Path)
    {
        var fs = new FileStream(Path, FileMode.Open);
        var Ret = FromGpx(fs);
        fs.Close();
        return Ret;
    }

    public static List<DeviceFile> FromGpx(Stream Data)
    {

        var Doc = new XmlDocument();
        Data.Position = 0;
        Doc.Load(Data);
        if ("gpx" == Doc.DocumentElement.Name
            || Doc.DocumentElement.NamespaceURI == "http://www.topografix.com/GPX/1/1")
        {
            return FromGPXV1(Doc);
        }

        throw new Exception("File Type Not Supported");
    }

    private static List<DeviceFile> FromGPXV1(XmlDocument Doc)
    {
        var Res = new List<DeviceFile>();

        var BaseFile = FromGPXV1_Base(Doc);
        DeviceFile attach;
        foreach (var Act in BaseFile.Activities)
        {
            attach = new DeviceFile { Author = BaseFile.Author.Clone() };
            Res.Add(attach);
            attach.Activities.Add(Act);
        }
        return Res;
    }

    private static DeviceFile FromGPXV1_Base(XmlDocument Doc)
    {
        // TODO: How do you handle variable namespace prefixes???
        var ns = new XmlNamespaceManager(Doc.NameTable);
        ns.AddNamespace("gpx", "http://www.topografix.com/GPX/1/1");
        ns.AddNamespace("gpxtpx", "http://www.garmin.com/xmlschemas/TrackPointExtension/v1");
        ns.AddNamespace("gpxx", "http://www.garmin.com/xmlschemas/GpxExtensions/v3");
        XMLParserHelper.Manager = ns;

        var CurrentAuthor = new DeviceAuthor();
        CurrentAuthor.Name = XMLParserHelper.SelectSingleAttributeString(Doc.DocumentElement, "creator");
        // CurrentAuthor.Language = "???";
        // CurrentAuthor.PartNumber = "???";
        CurrentAuthor.Version = XMLParserHelper.SelectSingleAttributeString(Doc.DocumentElement, "version");

        var File = new DeviceFile();
        File.Author = CurrentAuthor;
        DateTimeOffset dtoFileTime;
        DateTime? fileTime = null;
        var fileTimeString = XMLParserHelper.SelectSingleTextString(Doc, "/gpx:gpx/gpx:metadata/gpx:time");
        if (!string.IsNullOrWhiteSpace(fileTimeString) && DateTimeOffset.TryParse(fileTimeString, out dtoFileTime))
        {
            // TODO: FSSecurity.Current.ToUserTime ???
            fileTime = dtoFileTime.DateTime;
        }

        var Activities = Doc.GetElementsByTagName("trk");
        foreach (XmlNode Activity in Activities)
        {
            var CurrentActivity = new DeviceActivity();
            File.Activities.Add(CurrentActivity);

            CurrentActivity.Id = XMLParserHelper.SelectSingleTextString(Activity, "gpx:name");
            CurrentActivity.ActivityTime = fileTime;
            CurrentActivity.Sport = XMLParserHelper.SelectSingleTextString(Activity, "gpx:type", "Unknown");

            var CurrentCreator = new DeviceCreator();
            CurrentCreator.Name = "???";
            CurrentCreator.UnitID = "???";
            CurrentCreator.ProductID = "???";
            CurrentCreator.Version = "???";

            CurrentActivity.Creator = CurrentCreator;

            var Lap = new DeviceLap();
            CurrentActivity.Laps.Add(Lap);

            var TrackPoints = Activity.SelectNodes("gpx:trkseg/gpx:trkpt", ns);
            foreach (XmlNode Point in TrackPoints)
            {
                var CurrentPoint = new DevicePoint();
                Lap.Track.Add(CurrentPoint);
                //var node = Point.SelectSingleNode()
            }
        }

        return File;
    }
}