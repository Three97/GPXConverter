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

    public static List<DeviceFile> FromTcx(string Path)
    {
        FileStream fs = new FileStream(Path, FileMode.Open);
        var Ret = FromTcx(fs);
        fs.Close();
        return Ret;
    }

    public static List<DeviceFile> FromTcx(Stream Data)
    {

        var Doc = new XmlDocument();
        // Apparently we've beren saving a copy of all tcx files to this xml file forever.
        //var f = new FileStream(HttpContext.Current.Server.MapPath("~/MyFile.xml"), FileMode.OpenOrCreate);
        //f.SetLength(0);
        //byte[] buffer = new byte[256];
        //int bytesRead = Data.Read(buffer, 0, 256);
        //while (bytesRead > 0)
        //{
        //    f.Write(buffer, 0, bytesRead);
        //    bytesRead = Data.Read(buffer, 0, 256);
        //}
        //f.Close();
        Data.Position = 0;
        Doc.Load(Data);
        if ("TrainingCenterDatabase" == Doc.DocumentElement.Name
            || Doc.DocumentElement.NamespaceURI == "http://www.garmin.com/xmlschemas/TrainingCenterDatabase/v2")
        {
            return FromTcxV2(Doc);
        }
        throw new Exception("File Type Not Supported");
    }

    private static List<DeviceFile> FromTcxV2(XmlDocument Doc)
    {
        var Res = new List<DeviceFile>();

        var BaseFile = FromTcxV2_Base(Doc);
        DeviceFile attach;
        foreach (var Act in BaseFile.Activities)
        {
            attach = new DeviceFile { Author = BaseFile.Author.Clone() };
            Res.Add(attach);
            attach.Activities.Add(Act);
        }
        return Res;
    }

    private static DeviceFile FromTcxV2_Base(XmlDocument Doc)
    {
        var ns = new XmlNamespaceManager(Doc.NameTable);

        ns.AddNamespace("tcd", "http://www.garmin.com/xmlschemas/TrainingCenterDatabase/v2");
        ns.AddNamespace("ae", "http://www.garmin.com/xmlschemas/ActivityExtension/v2");
        XMLParserHelper.Manager = ns;
        //var Activities = Doc.SelectNodes("/tcd:TrainingCenterDatabase/tcd:Activities/tcd:Activity", ns);
        var Activities = Doc.GetElementsByTagName("Activity");
        DeviceActivity CurrentActivity;
        DeviceLap CurrentLap;
        DevicePoint CurrentPoint;
        DeviceCreator CurrentCreator;
        DeviceAuthor CurrentAuthor;
        XmlNode WorkNode;
        XmlAttribute WorkAttr;
        DateTime dtTryParse;
        DateTimeOffset dtoTryParse;

        DeviceFile File = new DeviceFile();

        CurrentAuthor = new DeviceAuthor();
        File.Author = CurrentAuthor;

        string VMajor, VMinor, BMajor, BMinor;

        CurrentAuthor.Name = XMLParserHelper.SelectSingleTextString(Doc, "/tcd:TrainingCenterDatabase/tcd:Author/tcd:Name");
        CurrentAuthor.Language = XMLParserHelper.SelectSingleTextString(Doc, "/tcd:TrainingCenterDatabase/tcd:Author/tcd:LangID");
        CurrentAuthor.PartNumber = XMLParserHelper.SelectSingleTextString(Doc, "/tcd:TrainingCenterDatabase/tcd:Author/tcd:PartNumber");
        VMajor = XMLParserHelper.SelectSingleTextString(Doc, "/tcd:TrainingCenterDatabase/tcd:Author/tcd:Build/tcd:Version/tcd:VersionMajor", "0");
        VMinor = XMLParserHelper.SelectSingleTextString(Doc, "/tcd:TrainingCenterDatabase/tcd:Author/tcd:Build/tcd:Version/tcd:VersionMinor", "0");
        BMajor = XMLParserHelper.SelectSingleTextString(Doc, "/tcd:TrainingCenterDatabase/tcd:Author/tcd:Build/tcd:Version/tcd:BuildMajor", "0");
        BMinor = XMLParserHelper.SelectSingleTextString(Doc, "/tcd:TrainingCenterDatabase/tcd:Author/tcd:Build/tcd:Version/tcd:BuildMinor", "0");
        CurrentAuthor.Version = VMajor + "." + VMinor + "." + BMajor + "." + BMinor;

        foreach (XmlNode Activity in Activities)
        {
            CurrentActivity = new DeviceActivity();
            File.Activities.Add(CurrentActivity);
            CurrentActivity.Id = XMLParserHelper.SelectSingleTextString(Activity, "tcd:Id");
            if (CurrentActivity.Id != null && DateTimeOffset.TryParse(CurrentActivity.Id, out dtoTryParse))
            {
                //dtoTryParse = FSSecurity.Current.ToUserTime(dtoTryParse);
                dtTryParse = new DateTime(dtoTryParse.Year, dtoTryParse.Month, dtoTryParse.Day, dtoTryParse.Hour, dtoTryParse.Minute, dtoTryParse.Second);
                CurrentActivity.ActivityTime = dtTryParse;
            }
            CurrentActivity.Sport = XMLParserHelper.SelectSingleAttributeString(Activity, "Sport", "Unknown");

            CurrentCreator = new DeviceCreator();
            CurrentActivity.Creator = CurrentCreator;

            CurrentCreator.Name = XMLParserHelper.SelectSingleTextString(Activity, "tcd:Creator/tcd:Name");
            CurrentCreator.UnitID = XMLParserHelper.SelectSingleTextString(Activity, "tcd:Creator/tcd:UnitId");
            CurrentCreator.ProductID = XMLParserHelper.SelectSingleTextString(Activity, "tcd:Creator/tcd:ProductID");
            VMajor = XMLParserHelper.SelectSingleTextString(Activity, "tcd:Creator/tcd:Version/tcd:VersionMajor", "0");
            VMinor = XMLParserHelper.SelectSingleTextString(Activity, "tcd:Creator/tcd:Version/tcd:VersionMinor", "0");
            BMajor = XMLParserHelper.SelectSingleTextString(Activity, "tcd:Creator/tcd:Version/tcd:BuildMajor", "0");
            BMinor = XMLParserHelper.SelectSingleTextString(Activity, "tcd:Creator/tcd:Version/tcd:BuildMinor", "0");
            CurrentCreator.Version = VMajor + "." + VMinor + "." + BMajor + "." + BMinor;
            var CreatorName = CurrentCreator.Name ?? "";
            var Laps = Activity.SelectNodes("tcd:Lap", ns);
            var IsFirst = true;
            foreach (XmlNode Lap in Laps)
            {
                var MyInt = XMLParserHelper.SelectSingleTextString(Lap, "tcd:Intensity", "");
                // This is for trailing resting tomtom laps that have zero distance and time
                if (MyInt.ToLower() == "resting" && CreatorName.ToLower().IndexOf("tomtom") >= 0)
                {
                    continue;
                }
                if (IsFirst)
                {
                    IsFirst = false;
                    var TestSeconds = XMLParserHelper.SelectSingleTextDecimal(Lap, "tcd:TotalTimeSeconds", null);
                    var TestDist = XMLParserHelper.SelectSingleTextDecimal(Lap, "tcd:DistanceMeters", null);
                    if (TestSeconds == 0 && TestDist == 0)
                        continue;
                }
                CurrentLap = new DeviceLap();
                CurrentActivity.Laps.Add(CurrentLap);
                WorkAttr = Lap.Attributes["StartTime"];
                if (null != WorkAttr && null != CurrentActivity.ActivityTime && DateTimeOffset.TryParse(WorkAttr.Value, out dtoTryParse))
                {
                    //dtoTryParse = FSSecurity.Current.ToUserTime(dtoTryParse);
                    dtTryParse = new DateTime(dtoTryParse.Year, dtoTryParse.Month, dtoTryParse.Day, dtoTryParse.Hour, dtoTryParse.Minute, dtoTryParse.Second);
                    CurrentLap.StartSeconds = (dtTryParse - (CurrentActivity.ActivityTime ?? DateTime.Now)).TotalSeconds;
                }

                CurrentLap.Time = XMLParserHelper.SelectSingleTextDecimal(Lap, "tcd:TotalTimeSeconds", null);
                CurrentLap.Distance = XMLParserHelper.SelectSingleTextDecimal(Lap, "tcd:DistanceMeters", null);
                CurrentLap.SpeedAvg = XMLParserHelper.SelectSingleTextDecimal(Lap, "tcd:Extensions/ae:LX/ae:AvgSpeed", null);
                CurrentLap.SpeedMax = XMLParserHelper.SelectSingleTextDecimal(Lap, "tcd:MaximumSpeed", null);
                CurrentLap.Calories = XMLParserHelper.SelectSingleTextInt(Lap, "tcd:Calories", null);
                CurrentLap.RPMAvg = XMLParserHelper.SelectSingleTextInt(Lap, "tcd:Cadence", null);
                CurrentLap.RPMMax = XMLParserHelper.SelectSingleTextInt(Lap, "tcd:Extensions/ae:LX/ae:MaxBikeCadence", null);
                CurrentLap.HeartRateAvg = XMLParserHelper.SelectSingleTextInt(Lap, "tcd:AverageHeartRateBpm/tcd:Value", null);
                CurrentLap.HeartRateMax = XMLParserHelper.SelectSingleTextInt(Lap, "tcd:MaximumHeartRateBpm/tcd:Value", null);
                CurrentLap.WattsAvg = XMLParserHelper.SelectSingleTextInt(Lap, "tcd:Extensions/ae:LX/ae:AvgWatts", null);
                CurrentLap.WattsMax = XMLParserHelper.SelectSingleTextInt(Lap, "tcd:Extensions/ae:LX/ae:MaxWatts", null);

                var TrackPoints = Lap.SelectNodes("tcd:Track/tcd:Trackpoint", ns);
                DevicePoint LastTrackPoint = null;
                foreach (XmlNode Point in TrackPoints)
                {
                    CurrentPoint = new DevicePoint();
                    CurrentLap.Track.Add(CurrentPoint);
                    WorkNode = Point.SelectSingleNode("tcd:Time", ns);
                    if (null != WorkNode && null != CurrentActivity.ActivityTime && DateTimeOffset.TryParse(WorkNode.InnerText, out dtoTryParse))
                    {
                        //dtoTryParse = FSSecurity.Current.ToUserTime(dtoTryParse);
                        dtTryParse = new DateTime(dtoTryParse.Year, dtoTryParse.Month, dtoTryParse.Day, dtoTryParse.Hour, dtoTryParse.Minute, dtoTryParse.Second);
                        CurrentPoint.StartSeconds = (dtTryParse - (CurrentActivity.ActivityTime ?? DateTime.Now)).TotalSeconds;
                    }

                    CurrentPoint.Latitude = XMLParserHelper.SelectSingleTextDecimal(Point, "tcd:Position/tcd:LatitudeDegrees", null);
                    CurrentPoint.Longitude = XMLParserHelper.SelectSingleTextDecimal(Point, "tcd:Position/tcd:LongitudeDegrees", null);
                    CurrentPoint.Altitude = XMLParserHelper.SelectSingleTextDecimal(Point, "tcd:AltitudeMeters", null);
                    CurrentPoint.Distance = XMLParserHelper.SelectSingleTextDecimal(Point, "tcd:DistanceMeters", null);
                    CurrentPoint.HR = XMLParserHelper.SelectSingleTextInt(Point, "tcd:HeartRateBpm/tcd:Value", null);
                    CurrentPoint.RPM = XMLParserHelper.SelectSingleTextInt(Point, "tcd:Cadence", null);
                    CurrentPoint.CAD = XMLParserHelper.SelectSingleTextInt(Point, "tcd:Extensions/ae:TPX/ae:RunCadence", null);
                    if (CurrentPoint.CAD != null) CurrentPoint.CAD = CurrentPoint.CAD.Value * 2;
                    CurrentPoint.Speed = XMLParserHelper.SelectSingleTextDecimal(Point, "tcd:Extensions/ae:TPX/ae:Speed", null);
                    CurrentPoint.Watts = XMLParserHelper.SelectSingleTextInt(Point, "tcd:Extensions/ae:TPX/ae:Watts", null);
                    if (LastTrackPoint != null && CurrentPoint.Speed == null && (CurrentPoint.Distance != null && CurrentPoint.StartSeconds != null) && (LastTrackPoint.Distance != null && LastTrackPoint.StartSeconds != null))
                    {
                        // Last Distance and Time vs Current Distance and Time ->
                        var SecondDelta = (decimal)(CurrentPoint.StartSeconds - LastTrackPoint.StartSeconds);
                        var DistanceDelta = CurrentPoint.Distance - LastTrackPoint.Distance;
                        if (SecondDelta == 0 || DistanceDelta == 0)
                            CurrentPoint.Speed = 0;
                        else
                            CurrentPoint.Speed = DistanceDelta / SecondDelta;
                    }
                    LastTrackPoint = CurrentPoint;
                }

                var CadencePoints = from x in CurrentLap.Track
                                    where x.CAD != null
                                    select x;
                if (CadencePoints.Any())
                {
                    var m = (from x in CadencePoints
                             select x.CAD).Max();
                    var a = (from x in CadencePoints
                             select x.CAD).Average();
                    CurrentLap.CADAvg = (int?)a;
                    CurrentLap.CADMax = (int?)m;
                }
            }
        }
        return File;
    }

}