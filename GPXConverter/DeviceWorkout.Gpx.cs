using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.IO;
using System.Net;
using System.Runtime.CompilerServices;
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

    private static readonly Dictionary<NamespaceType, string> ValidNamespaces = new Dictionary<NamespaceType, string>
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
    
    public static List<DeviceFile> FromGpx(string path, decimal lapIntervalMeters)
    {
        var fs = new FileStream(path, FileMode.Open);
        var result = FromGpx(fs, lapIntervalMeters);
        fs.Close();
        return result;
    }

    public static List<DeviceFile> FromGpx(Stream dataStream, decimal lapIntervalMeters)
    {
        dataStream.Position = 0;
        var doc = new XmlDocument();
        doc.Load(dataStream);
        if (doc.DocumentElement != null
            && ("gpx" == doc.DocumentElement.Name
                || doc.DocumentElement.NamespaceURI == ValidNamespaces[NamespaceType.GPX]))
        {
            return FromGPXV1(doc, lapIntervalMeters);
        }

        throw new Exception("File Type Not Supported");
    }

    private static List<DeviceFile> FromGPXV1(XmlDocument document, decimal lapIntervalMeters)
    {
        var result = new List<DeviceFile>();

        var baseFile = FromGPXV1_Base(document, lapIntervalMeters);
        foreach (var activity in baseFile.Activities)
        {
            var attach = new DeviceFile { Author = baseFile.Author.Clone() };
            result.Add(attach);
            attach.Activities.Add(activity);
        }
        return result;
    }

    private static DeviceFile FromGPXV1_Base(XmlDocument document, decimal lapIntervalMeters)
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

        var creator = new DeviceCreator
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

        var author = new DeviceAuthor { Name = creator.Name, Version = creator.Version };

        var file = new DeviceFile();
        file.Author = author;
        DateTimeOffset dtoFileTime;
        DateTime? fileTime = null;
        var fileTimeString = XMLParserHelper.SelectSingleTextString(document, $"/{prefixGpx}:gpx/{prefixGpx}:metadata/{prefixGpx}:time");
        if (!string.IsNullOrWhiteSpace(fileTimeString) && DateTimeOffset.TryParse(fileTimeString, out dtoFileTime))
        {
            // TODO: FSSecurity.Current.ToUserTime ???
            fileTime = dtoFileTime.DateTime;
        }

        // Walk through all tracks and assign to Activities; usually just 1...
        var activityNodes = document.GetElementsByTagName("trk");
        foreach (XmlNode activityNode in activityNodes)
        {
            var activity = new DeviceActivity();
            file.Activities.Add(activity);

            activity.Id = XMLParserHelper.SelectSingleTextString(activityNode, $"{prefixGpx}:name");
            activity.Creator = creator;
            activity.Sport = XMLParserHelper.SelectSingleTextString(activityNode, $"{prefixGpx}:type", "Unknown");
            activity.ActivityTime = fileTime;

            // Parse entire list of track points...
            var allTrackPoints = new List<DevicePoint>();
            var trackPointNodes = activityNode.SelectNodes($"{prefixGpx}:trkseg/{prefixGpx}:trkpt", namespaceManager);
            if (trackPointNodes != null)
            {
                DateTime? activityStartTimeUtc = null;
                foreach (XmlNode trackPointNode in trackPointNodes)
                {
                    var point = new DevicePoint();
                    var pointTimeUtc = XMLParserHelper.SelectSingleTextDateTime(trackPointNode, $"{prefixGpx}:time");
                    if (pointTimeUtc != null)
                    {
                        if (activityStartTimeUtc == null)
                        {
                            activityStartTimeUtc = pointTimeUtc;
                            point.StartSeconds = 0;
                        }
                        else
                        {
                            point.StartSeconds = (pointTimeUtc.Value - activityStartTimeUtc.Value).TotalSeconds;
                        }
                    }

                    // Parse latitude / longitude...
                    point.Latitude = XMLParserHelper.SelectSingleAttributeDecimal(trackPointNode, "lat");
                    point.Longitude = XMLParserHelper.SelectSingleAttributeDecimal(trackPointNode, "lon");

                    // Parse altitude...
                    point.Altitude = XMLParserHelper.SelectSingleTextDecimal(
                        trackPointNode,
                        $"{prefixGpx}:ele");

                    // Parse heart rate...
                    point.HR = XMLParserHelper.SelectSingleTextInt(
                        trackPointNode,
                        $"{prefixGpx}:extensions/{prefixTrackPointExt}:TrackPointExtension/{prefixTrackPointExt}:hr");

                    if (point.HR == null)
                    {
                        point.HR = XMLParserHelper.SelectSingleTextInt(trackPointNode, $"{prefixGpx}:extensions/{prefixGpxExt}:hr");
                    }

                    // Parse ambient temp...
                    point.Temp = XMLParserHelper.SelectSingleTextInt(
                        trackPointNode,
                        $"{prefixGpx}:extensions/{prefixTrackPointExt}:TrackPointExtension/{prefixTrackPointExt}:atemp");

                    if (point.Temp == null)
                    {
                        point.Temp = XMLParserHelper.SelectSingleTextInt(trackPointNode, $"{prefixGpx}:extensions/{prefixGpxExt}:temp");
                    }

                    // Parse cadence...
                    point.CAD = XMLParserHelper.SelectSingleTextInt(
                        trackPointNode,
                        $"{prefixGpx}:extensions/{prefixTrackPointExt}:TrackPointExtension/{prefixTrackPointExt}:cad");

                    if (point.CAD == null)
                    {
                        point.CAD = XMLParserHelper.SelectSingleTextInt(trackPointNode, $"{prefixGpx}:extensions/{prefixGpxExt}:cadence");
                    }

                    // Multiply CAD result x 2 like in TCX?  
                    // TODO: Bike no...Run yes?
                    if (point.CAD != null)
                    {
                        point.CAD = point.CAD * 2;
                    }

                    allTrackPoints.Add(point);
                }
            }

            // Re-run through point list to calculate distances and break up into laps...
            var lapList = new List<DeviceLap>();
            var currentLap = new DeviceLap();
            decimal previousPointDistance = 0;
            decimal previousLapDistance = 0;
            for (var i = 0; i < allTrackPoints.Count - 2; i++)
            {
                if (currentLap.Track.Count == 0)
                {
                    activity.Laps.Add(currentLap);
                }

                var currentPoint = allTrackPoints[i];
                var nextPoint = allTrackPoints[i + 1];

                if (currentPoint.Latitude == null 
                    || currentPoint.Longitude == null 
                    || nextPoint.Latitude == null
                    || nextPoint.Longitude == null)
                {
                    continue;
                }

                var distanceKm = HaversineInKM(
                    (double)currentPoint.Latitude.Value,
                    (double)currentPoint.Longitude.Value,
                    (double)nextPoint.Latitude.Value,
                    (double)nextPoint.Longitude.Value);

                currentPoint.Distance = (decimal)(distanceKm * 1000.0) + previousPointDistance;
                previousPointDistance = currentPoint.Distance.Value;
                currentLap.Track.Add(currentPoint);

                if (currentLap.StartSeconds == null)
                {
                    currentLap.StartSeconds = currentPoint.StartSeconds;
                }

                currentLap.Distance = currentLap.Track.Last().Distance.Value - previousLapDistance;
                if (currentLap.Distance.Value < lapIntervalMeters)
                {
                    continue;
                }

                // Reached the end of the lap, start a new one...
                previousLapDistance = currentLap.Distance.Value;
                currentLap = new DeviceLap();
            }

            // Loop through all resulting laps to calculate remaining aggregates...
            activity.Laps.ForEach(
                lap =>
                    {
                        // Calculate HRMax and HRAvg from lap's collection of HR points...
                        var hrPoints = lap.Track.Where(t => t.HR != null).Select(x => x.HR).ToList();
                        lap.HeartRateMax = hrPoints.Any() ? hrPoints.Max() : null;
                        lap.HeartRateAvg = hrPoints.Any() ? (int?)hrPoints.Average() : null;

                        // Calculate CADMax and CADAvg from lap's collection of CAD points...
                        var cadPoints = lap.Track.Where(t => t.CAD != null).Select(x => x.CAD).ToList();
                        lap.CADMax = cadPoints.Any() ? cadPoints.Max() : null;
                        lap.CADAvg = cadPoints.Any() ? (int?)cadPoints.Average() : null;

                        // Calculate TempMax and TempAvg from lap's collection of Temp points...
                        var tempPoints = lap.Track.Where(t => t.Temp != null).Select(x => x.Temp).ToList();
                        lap.TempMax = tempPoints.Any() ? tempPoints.Max() : null;
                        lap.TempAvg = tempPoints.Any() ? (int?)tempPoints.Average() : null;
                    });
        }

        return file;
    }
}