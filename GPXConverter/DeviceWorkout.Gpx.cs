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
        CluetrustExtensions
    }

    private static readonly Dictionary<NamespaceType, string> ValidNamespaces = new Dictionary<NamespaceType, string>
    {
        { NamespaceType.GPX, "http://www.topografix.com/GPX/1/1" },
        { NamespaceType.TrackPointExtensions, "http://www.garmin.com/xmlschemas/TrackPointExtension/v1" },
        { NamespaceType.CluetrustExtensions, "http://www.garmin.com/xmlschemas/GpxExtensions/v3" }
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
                o => o.Value.ToLower(),
                i => i.Value.ToLower(),
                (i, o) => new { Type = o.Key, Prefix = string.IsNullOrWhiteSpace(i.Key) ? "gpx" : i.Key, Url = o.Value })
                .ToList();

        var prefixGpx = namespacePrefixes.Single(x => x.Type == NamespaceType.GPX).Prefix;
        var prefixTrackPointExt = namespacePrefixes.SingleOrDefault(x => x.Type == NamespaceType.TrackPointExtensions)?.Prefix;
        var prefixCluetrustExt = namespacePrefixes.SingleOrDefault(x => x.Type == NamespaceType.CluetrustExtensions)?.Prefix;

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
                            point.StartSeconds = 1;
                            point.Distance = 0;
                        }
                        else
                        {
                            point.StartSeconds = (pointTimeUtc.Value - activityStartTimeUtc.Value.AddSeconds(1)).TotalSeconds;
                        }
                    }

                    // Parse latitude / longitude...
                    point.Latitude = XMLParserHelper.SelectSingleAttributeDecimal(trackPointNode, "lat");
                    point.Longitude = XMLParserHelper.SelectSingleAttributeDecimal(trackPointNode, "lon");

                    // Parse altitude...
                    point.Altitude = XMLParserHelper.SelectSingleTextDecimal(
                        trackPointNode,
                        $"{prefixGpx}:ele");

                    // Parse TrackPoint extension data...
                    if (prefixTrackPointExt != null)
                    {
                        // Parse heart rate...
                        point.HR = XMLParserHelper.SelectSingleTextInt(
                            trackPointNode,
                            $"{prefixGpx}:extensions/{prefixTrackPointExt}:TrackPointExtension/{prefixTrackPointExt}:hr");

                        // Parse ambient temp...
                        point.Temp = XMLParserHelper.SelectSingleTextInt(
                            trackPointNode,
                            $"{prefixGpx}:extensions/{prefixTrackPointExt}:TrackPointExtension/{prefixTrackPointExt}:atemp");

                        // Parse cadence...
                        point.CAD = XMLParserHelper.SelectSingleTextInt(
                            trackPointNode,
                            $"{prefixGpx}:extensions/{prefixTrackPointExt}:TrackPointExtension/{prefixTrackPointExt}:cad");
                    }

                    // Parse Cluetrust extension data...
                    if (prefixCluetrustExt != null)
                    {
                        // Parse heart rate...
                        if (point.HR == null)
                        {
                            point.HR = XMLParserHelper.SelectSingleTextInt(trackPointNode, $"{prefixGpx}:extensions/{prefixCluetrustExt}:hr");
                        }

                        // Parse ambient temp...
                        if (point.Temp == null)
                        {
                            point.Temp = XMLParserHelper.SelectSingleTextInt(trackPointNode, $"{prefixGpx}:extensions/{prefixCluetrustExt}:temp");
                        }

                        // Parse cadence...
                        if (point.CAD == null)
                        {
                            point.CAD = XMLParserHelper.SelectSingleTextInt(trackPointNode, $"{prefixGpx}:extensions/{prefixCluetrustExt}:cadence");
                        }
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
            var currentLap = new DeviceLap { StartSeconds = 0 };
            for (var i = 1; i < allTrackPoints.Count; i++)
            {
                var currentPoint = allTrackPoints[i];
                var previousPoint = allTrackPoints[i - 1];

                if (currentLap.Track.Count == 0)
                {
                    var previousLap = activity.Laps.LastOrDefault();
                    if (previousLap != null)
                    {
                        previousLap.Time = (decimal)(currentPoint.StartSeconds.Value - previousLap.StartSeconds.Value);
                    }

                    activity.Laps.Add(currentLap);
                }

                if (currentPoint.Latitude == null 
                    || currentPoint.Longitude == null 
                    || previousPoint.Latitude == null
                    || previousPoint.Longitude == null)
                {
                    continue;
                }

                var distanceKm = HaversineInKM(
                    (double)previousPoint.Latitude.Value,
                    (double)previousPoint.Longitude.Value,
                    (double)currentPoint.Latitude.Value,
                    (double)currentPoint.Longitude.Value);

                var currentPointDistanceDelta = (decimal)(distanceKm * 1000.0);
                currentPoint.Distance = currentPointDistanceDelta + previousPoint.Distance;
                var startSecondsDelta = (decimal)(currentPoint.StartSeconds.Value - previousPoint.StartSeconds.Value);
                startSecondsDelta = startSecondsDelta == 0 ? 1 : startSecondsDelta;
                currentPoint.Speed = currentPointDistanceDelta / startSecondsDelta;

                currentLap.Track.Add(currentPoint);

                if (currentLap.StartSeconds == null)
                {
                    currentLap.StartSeconds = currentPoint.StartSeconds;
                }

                currentLap.Distance = currentLap.Track.Last().Distance.Value - currentLap.Track.First().Distance.Value;
                if (currentLap.Distance.Value < lapIntervalMeters)
                {
                    continue;
                }

                // Reached the end of the lap, start a new one...
                currentLap = new DeviceLap { StartSeconds = currentLap.Track.Last().StartSeconds.Value };
            }

            // Calculate Time for last lap...
            var lastLap = activity.Laps.Last();
            lastLap.Time = (decimal)(lastLap.Track.Last().StartSeconds.Value - lastLap.Track.First().StartSeconds.Value);

            // Loop through resulting laps to calculate Time and remaining aggregates...
            activity.Laps.ForEach(lap =>
            {
                // Calculate HRMax and HRAvg from lap's collection of HR points...
                var hrPoints = lap.Track.Where(t => t.HR != null).Select(x => x.HR.Value).ToList();
                lap.HeartRateMax = hrPoints.Any() ? (int?)hrPoints.Max() : null;
                lap.HeartRateAvg = hrPoints.Any() ? (int?)hrPoints.Average() : null;

                // Calculate CADMax and CADAvg from lap's collection of CAD points...
                var cadPoints = lap.Track.Where(t => t.CAD != null).Select(x => x.CAD.Value).ToList();
                lap.CADMax = cadPoints.Any() ? (int?)cadPoints.Max() : null;
                lap.CADAvg = cadPoints.Any() ? (int?)cadPoints.Average() : null;

                // Calculate TempMax and TempAvg from lap's collection of Temp points...
                var tempPoints = lap.Track.Where(t => t.Temp != null).Select(x => x.Temp.Value).ToList();
                lap.TempMax = tempPoints.Any() ? (int?)tempPoints.Max() : null;
                lap.TempAvg = tempPoints.Any() ? (int?)tempPoints.Average() : null;

                // Calculage SpeedMax and SpeedAvg from lap's collection of points...
                var speedPoints = lap.Track.Where(t => t.Speed != null).Select(x => x.Speed.Value).ToList();
                lap.SpeedMax = speedPoints.Any() ? (decimal?)speedPoints.Max() : null;
                lap.SpeedAvg = speedPoints.Any() ? (decimal?)speedPoints.Average() : null;
            });
        }

        return file;
    }
}