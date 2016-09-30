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

    private static Dictionary<NamespaceType, string> ValidNamespaces = new Dictionary<NamespaceType, string>
    {
        { NamespaceType.GPX, "http://www.topografix.com/GPX/1/1" },
        { NamespaceType.TrackPointExtensions, "http://www.garmin.com/xmlschemas/TrackPointExtension/v1" },
        { NamespaceType.GPXExtensions, "http://www.garmin.com/xmlschemas/GpxExtensions/v3" }
    };

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
    
    public static List<DeviceFile> FromGpx(string path)
    {
        var fs = new FileStream(path, FileMode.Open);
        var result = FromGpx(fs);
        fs.Close();
        return result;
    }

    public static List<DeviceFile> FromGpx(Stream dataStream)
    {
        dataStream.Position = 0;
        var doc = new XmlDocument();
        doc.Load(dataStream);
        if ("gpx" == doc.DocumentElement.Name
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
        var nav = document.CreateNavigator();
        nav.MoveToFollowing(XPathNodeType.Element);
        if (nav == null)
        {
            throw new Exception("File Type Not Supported");
        }

        var namespaces = nav.GetNamespacesInScope(XmlNamespaceScope.Local);
        if (namespaces == null || !namespaces.Keys.Any())
        {
            throw new Exception("File Type Not Supported");
        }

        var namespacePrefixes =
            namespaces.Join(
                ValidNamespaces,
                o => o.Value,
                i => i.Value,
                (i, o) => new { Type = o.Key, Prefix = string.IsNullOrWhiteSpace(i.Key) ? "gpx" : i.Key, Url = o.Value })
                .ToList();

        var prefixGpx = namespacePrefixes.Single(x => x.Type == NamespaceType.GPX).Prefix;
        var prefixTrackPointExt = namespacePrefixes.Single(x => x.Type == NamespaceType.TrackPointExtensions).Prefix;
        var prefixGpxExt = namespacePrefixes.Single(x => x.Type == NamespaceType.GPXExtensions).Prefix;

        var namespaceManager = new XmlNamespaceManager(document.NameTable);
        namespacePrefixes.ForEach(x => namespaceManager.AddNamespace(x.Prefix, x.Url));
        XMLParserHelper.Manager = namespaceManager;

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
        var fileTimeString = XMLParserHelper.SelectSingleTextString(document, $"/{prefixGpx}:gpx/{prefixGpx}:metadata/{prefixGpx}:time");
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

            activity.Id = XMLParserHelper.SelectSingleTextString(activityNode, $"{prefixGpx}:name");
            activity.ActivityTime = fileTime;
            activity.Sport = XMLParserHelper.SelectSingleTextString(activityNode, $"{prefixGpx}:type", "Unknown");

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

            var trackPointNodes = activityNode.SelectNodes($"{prefixGpx}:trkseg/{prefixGpx}:trkpt", namespaceManager);
            if (trackPointNodes != null)
            {
                foreach (XmlNode trackPointNode in trackPointNodes)
                {
                    var point = new DevicePoint();
                    lap.Track.Add(point);

                    var pointTime = XMLParserHelper.SelectSingleTextDateTime(trackPointNode, $"{prefixGpx}:time");

                    point.Latitude = XMLParserHelper.SelectSingleAttributeDecimal(trackPointNode, "lat");
                    point.Longitude = XMLParserHelper.SelectSingleAttributeDecimal(trackPointNode, "lon");

                    point.Altitude = XMLParserHelper.SelectSingleTextDecimal(
                        trackPointNode,
                        $"{prefixGpx}:ele");

                    point.HR = XMLParserHelper.SelectSingleTextInt(
                        trackPointNode,
                        $"{prefixGpx}:extensions/{prefixTrackPointExt}:TrackPointExtension/{prefixTrackPointExt}:hr");

                    if (point.HR == null)
                    {
                        point.HR = XMLParserHelper.SelectSingleTextInt(trackPointNode, $"{prefixGpx}:extensions/{prefixGpxExt}:hr");
                    }

                    point.Temp = XMLParserHelper.SelectSingleTextInt(
                        trackPointNode,
                        $"{prefixGpx}:extensions/{prefixTrackPointExt}:TrackPointExtension/{prefixTrackPointExt}:atemp");

                    if (point.Temp == null)
                    {
                        point.Temp = XMLParserHelper.SelectSingleTextInt(trackPointNode, $"{prefixGpx}:extensions/{prefixGpxExt}:temp");
                    }

                    point.CAD = XMLParserHelper.SelectSingleTextInt(
                        trackPointNode,
                        $"{prefixGpx}:extensions/{prefixTrackPointExt}:TrackPointExtension/{prefixTrackPointExt}:cad");

                    if (point.CAD == null)
                    {
                        point.CAD = XMLParserHelper.SelectSingleTextInt(trackPointNode, $"{prefixGpx}:extensions/{prefixGpxExt}:cadence");
                    }

                    // TODO: Multiply CAD result x 2 like in TCX?  Bike no...Run yes?
                    if (activity.Sport == "run")
                    {
                        point.CAD = point.CAD * 2;
                    }

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