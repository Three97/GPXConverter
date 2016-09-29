using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.IO;
using System.Xml.XPath;
//using Dynastream;

/// <summary>
/// Summary description for DeviceWorkout
/// </summary>
public partial class DeviceFile
{
    private enum NamespaceType
    {
        GPX,
        TrackPointExtensions,
        GPXExtensions
    }

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

    public static List<DeviceFile> FromGpx(Stream dataStream)
    {
        dataStream.Position = 0;
        var doc = new XmlDocument();
        doc.Load(dataStream);
        if (doc.DocumentElement == null 
            || "gpx" == doc.DocumentElement.Name
            || doc.DocumentElement.NamespaceURI == "http://www.topografix.com/GPX/1/1")
        {
            return FromGPXV1(doc);
        }

        throw new Exception("File Type Not Supported");
    }

    private static List<DeviceFile> FromGPXV1(XmlDocument document)
    {
        var result = new List<DeviceFile>();

        var baseFile = FromGPXV1_Base(document);
        foreach (var activity in baseFile.Activities)
        {
            var attach = new DeviceFile { Author = baseFile.Author.Clone() };
            result.Add(attach);
            attach.Activities.Add(activity);
        }
        return result;
    }

    private static DeviceFile FromGPXV1_Base(XmlDocument document)
    {
        var validNamespaceDict = new Dictionary<NamespaceType, string>
        {
            { NamespaceType.GPX,  "http://www.topografix.com/GPX/1/1" },
            { NamespaceType.TrackPointExtensions, "http://www.garmin.com/xmlschemas/TrackPointExtension/v1" },
            { NamespaceType.GPXExtensions, "http://www.garmin.com/xmlschemas/GpxExtensions/v3" }
        };

        var namespacePrefixDict = new Dictionary<NamespaceType, string>();

        var nav = document.CreateNavigator();
        nav.MoveToFollowing(XPathNodeType.Element);
        if (nav == null)
        {
            throw new Exception("File Type Not Supported");
        }

        var namespaceDict = nav.GetNamespacesInScope(XmlNamespaceScope.Local);
        if (namespaceDict == null || !namespaceDict.Keys.Any())
        {
            throw new Exception("File Type Not Supported");
        }

        var namespaces =
            namespaceDict.Join(
                validNamespaceDict,
                o => o.Value,
                i => i.Value,
                (i, o) => new { Type = o.Key, Prefix = string.IsNullOrWhiteSpace(i.Key) ? "gpx" : i.Key, Url = o.Value })
                .ToList();

        namespaces.ForEach(x => namespacePrefixDict.Add(x.Type, x.Prefix));
        
        var ns = new XmlNamespaceManager(document.NameTable);
        namespaces.ForEach(x => ns.AddNamespace(x.Prefix, x.Url));
        XMLParserHelper.Manager = ns;

        var author = new DeviceAuthor
                                {
                                    Name =
                                        XMLParserHelper.SelectSingleAttributeString(
                                            document.DocumentElement,
                                            "creator"),
                                    Version =
                                        XMLParserHelper.SelectSingleAttributeString(
                                            document.DocumentElement,
                                            "version")
                                };

        var file = new DeviceFile { Author = author };
        DateTimeOffset dtoFileTime;
        DateTime? fileTime = null;
        var fileTimeString = XMLParserHelper.SelectSingleTextString(document, $"/{namespacePrefixDict[NamespaceType.GPX]}:gpx/{namespacePrefixDict[NamespaceType.GPX]}:metadata/{namespacePrefixDict[NamespaceType.GPX]}:time");
        if (!string.IsNullOrWhiteSpace(fileTimeString) && DateTimeOffset.TryParse(fileTimeString, out dtoFileTime))
        {
            // TODO: FSSecurity.Current.ToUserTime ???
            fileTime = dtoFileTime.DateTime;
        }

        var activityNodes = document.GetElementsByTagName("trk");
        foreach (XmlNode activityNode in activityNodes)
        {
            var activity = new DeviceActivity();
            file.Activities.Add(activity);

            activity.Id = XMLParserHelper.SelectSingleTextString(activityNode, $"{namespacePrefixDict[NamespaceType.GPX]}:name");
            activity.ActivityTime = fileTime;
            activity.Sport = XMLParserHelper.SelectSingleTextString(activityNode, $"{namespacePrefixDict[NamespaceType.GPX]}:type", "Unknown");

            var creator = new DeviceCreator
                                     {
                                         Name = "???",
                                         UnitID = "???",
                                         ProductID = "???",
                                         Version = "???"
                                     };

            activity.Creator = creator;

            var lap = new DeviceLap();
            activity.Laps.Add(lap);

            // TODO: Calculate these values???
            lap.StartSeconds = null;
            lap.Time = null;
            lap.Distance = null;
            lap.SpeedAvg = null;
            lap.SpeedMax = null;
            lap.Calories = null;
            lap.RPMAvg = null;
            lap.RPMMax = null;
            lap.WattsAvg = null;
            lap.WattsMax = null;

            var trackPointNodes = activityNode.SelectNodes($"{namespacePrefixDict[NamespaceType.GPX]}:trkseg/{namespacePrefixDict[NamespaceType.GPX]}:trkpt", ns);
            if (trackPointNodes != null)
            {
                foreach (XmlNode trackPointNode in trackPointNodes)
                {
                    var point = new DevicePoint();
                    lap.Track.Add(point);

                    var pointTime = XMLParserHelper.SelectSingleTextDateTime(
                        trackPointNode,
                        $"{namespacePrefixDict[NamespaceType.GPX]}:time");

                    point.Latitude = XMLParserHelper.SelectSingleAttributeDecimal(trackPointNode, "lat");
                    point.Longitude = XMLParserHelper.SelectSingleAttributeDecimal(trackPointNode, "lon");

                    point.Altitude = XMLParserHelper.SelectSingleTextDecimal(
                        trackPointNode,
                        $"{namespacePrefixDict[NamespaceType.GPX]}:ele");

                    point.HR = XMLParserHelper.SelectSingleTextInt(
                        trackPointNode,
                        $"{namespacePrefixDict[NamespaceType.GPX]}:extensions/{namespacePrefixDict[NamespaceType.TrackPointExtensions]}:TrackPointExtension/{namespacePrefixDict[NamespaceType.TrackPointExtensions]}:hr");

                    point.Temp = XMLParserHelper.SelectSingleTextInt(
                        trackPointNode,
                        $"{namespacePrefixDict[NamespaceType.GPX]}:extensions/{namespacePrefixDict[NamespaceType.TrackPointExtensions]}:TrackPointExtension/{namespacePrefixDict[NamespaceType.TrackPointExtensions]}:atemp");

                    // TODO: Multiply CAD result x 2?
                    point.CAD = XMLParserHelper.SelectSingleTextInt(
                        trackPointNode,
                        $"{namespacePrefixDict[NamespaceType.GPX]}:extensions/{namespacePrefixDict[NamespaceType.TrackPointExtensions]}:TrackPointExtension/{namespacePrefixDict[NamespaceType.TrackPointExtensions]}:cad");

                    // TODO: What, if anything, are we supposed to do with these values?
                    point.StartSeconds = null; // TODO: Calculate from pointTimes?
                    point.Distance = null;
                    point.Speed = null; // Recalculate this value later?
                    point.RPM = null;
                    point.Watts = null;
                }
            }

            // Calculate HRAvg and HRMax from lap's collection of HR points...
            var hrPoints = lap.Track.Where(t => t.HR != null).Select(x => x.HR).ToList();
            lap.HeartRateMax = hrPoints.Max();
            lap.HeartRateAvg = (int?)hrPoints.Average();

            // Calculate CADAvg and CADMax from lap's collection of CAD points...
            var cadPoints = lap.Track.Where(t => t.CAD != null).Select(x => x.CAD).ToList();
            lap.CADMax = cadPoints.Any() ? cadPoints.Max() : null;
            lap.CADAvg = cadPoints.Any() ? (int?)cadPoints.Average() : null;
        }

        return file;
    }
}