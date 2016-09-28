using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Xml;
using System.Xml.Serialization;
using System.IO;
using System.Text;
//using Dynastream;

[XmlRootAttribute("DeviceFile", IsNullable = false)]
public partial class DeviceFile
{

    public DeviceFile()
    {
        Activities = new List<DeviceActivity>();
    }
    [XmlArrayAttribute("Activities")]
    public List<DeviceActivity> Activities;
    public DeviceAuthor Author;
    public string ActivityIDString()
    {
        string Ret = null;
        foreach (var A in Activities)
        {
            Ret = (Ret == null) ? A.Id : Ret + "||||" + A.Id;
        }
        return Ret;
    }
}

/// <summary>
/// Software that created the TCX File
/// </summary>
public class DeviceAuthor
{
    /// <summary>
    /// Authoring Software's Name
    /// </summary>
    public string Name;
    /// <summary>
    /// Authoring Software's Verion
    /// </summary>
    public string Version;
    /// <summary>
    /// Authoring Software's Language
    /// </summary>
    public string Language;
    /// <summary>
    /// Authoring Software's Part Number
    /// </summary>
    public string PartNumber;
    /// <summary>
    /// Clones the current DeviceAuthor
    /// </summary>
    /// <returns>Cloned DeviceAuthor</returns>
    public DeviceAuthor Clone()
    {
        return new DeviceAuthor { Name = Name, Version = Version, Language = Language, PartNumber = PartNumber };
    }

}

/// <summary>
/// Device that created an Activity
/// </summary>
public class DeviceCreator
{
    /// <summary>
    /// Device's Name
    /// </summary>
    public string Name;
    /// <summary>
    /// Device's Unit ID
    /// </summary>
    public string UnitID;
    /// <summary>
    /// Device's Product ID
    /// </summary>
    public string ProductID;
    /// <summary>
    /// Device's Firmware Version
    /// </summary>
    public string Version;
    /// <summary>
    /// Device's Part Number
    /// </summary>
    public string PartNumber;
    /// <summary>
    /// Clones the current DeviceCreator
    /// </summary>
    /// <returns>Cloned DeviceCreator</returns>
    public DeviceCreator Clone()
    {
        return new DeviceCreator { Name = Name, Version = Version, UnitID = UnitID, ProductID = ProductID, PartNumber = PartNumber };
    }
    public override bool Equals(object obj)
    {
        var w = obj as DeviceCreator;
        w = w ?? new DeviceCreator();
        return (w.ProductID.Equals(this.ProductID) && w.UnitID.Equals(this.UnitID));
    }

}

public class DeviceActivity
{
    public DeviceActivity()
    {
        Laps = new List<DeviceLap>();
    }
    public List<DeviceLap> Laps;
    public string Sport;
    public string Id;
    public DeviceCreator Creator;
    public DateTime? ActivityTime;
    public decimal? DistanceTotal()
    {
        return Laps.Sum(lap => lap.Distance);
    }
    public decimal? DistanceAverage()
    {
        return Laps.Average(lap => lap.Distance);
    }
    public decimal? TimeTotal()
    {
        return Laps.Sum(lap => lap.Time);
    }
    public decimal? TimeAverage()
    {
        return Laps.Average(lap => lap.Time);
    }
    public int? CaloriesTotal()
    {
        return Laps.Sum(lap => lap.Calories);
    }
    public int? CaloriesAverage()
    {
        return (int?) Laps.Average(lap => lap.Calories);
    }

    int? _HRAvg, _RPMAvg, _WattsAvg, _CADAvg;
    int? _HRMin, _HRMax, _RPMMin, _RPMMax, _CADMin, _CADMax, _WattsMin, _WattsMax;
    decimal? _ElevationGain, _ElevationLoss;
    decimal? _SpeedMin, _SpeedAvg, _SpeedMax;
    bool AveragesPerformed = false;

    private void LoadCache()
    {
        if (AveragesPerformed) return;
        var points = Laps.SelectMany(x => x.Track);
        _HRMin = points.Min(p => p.HR);
        _HRMax = points.Max(p => p.HR);
        var RPMFiltered = points.Where(p => (p.RPM ?? 0) > 0);
        _RPMMin = RPMFiltered.Min(p => p.RPM);
        _RPMMax = RPMFiltered.Max(p => p.RPM);
        var CADFiltered = points.Where(p => (p.CAD ?? 0) > 0);
        _CADMin = CADFiltered.Min(p => p.CAD);
        _CADMax = CADFiltered.Max(p => p.CAD);
        var WattsFiltered = points.Where(p => (p.Watts ?? 0) > 0);
        _WattsMin = WattsFiltered.Min(p => p.Watts);
        _WattsMax = WattsFiltered.Max(p => p.Watts);
        var SpeedFiltered = points.Where(p => (p.Speed ?? 0) > 0);
        _SpeedMin = SpeedFiltered.Min(p => p.Speed);
        _SpeedMax = SpeedFiltered.Max(p => p.Speed);
        int? TotalHR, TotalRPM, TotalWatts, TotalCAD;
        decimal? TotalTime, TotalSpeed;
        TotalHR = TotalRPM = TotalWatts = TotalCAD = 0;
        TotalTime = TotalSpeed = 0;
        foreach (var Lap in Laps)
        {
            TotalTime += Lap.Time;
            TotalHR += (int?)(Lap.HeartRateAvg * Lap.Time);
            TotalRPM += (int?)(Lap.RPMAvg * Lap.Time);
            TotalWatts += (int?)(Lap.WattsAvg * Lap.Time);
            TotalCAD += (int?)(Lap.CADAvg * Lap.Time);
            TotalSpeed += Lap.SpeedAvg * Lap.Time;
        }
        if ((TotalTime ?? 0) > 0)
        {
            _HRAvg = (int?)(TotalHR / TotalTime);
            _RPMAvg = (int?)(TotalRPM / TotalTime);
            _CADAvg = (int?)(TotalCAD / TotalTime);
            _WattsAvg = (int?)(TotalWatts / TotalTime);
            _SpeedAvg = (TotalSpeed / TotalTime);
        }
        else
        {
            _HRAvg = _RPMAvg = _CADAvg = _WattsAvg = 0;
            _SpeedAvg = 0;
        }

        AveragesPerformed = true;
    }

    public int? HeartRateMin()
    {
        LoadCache();
        return _HRMin;
    }
    public int? HeartRateAverage()
    {
        LoadCache();
        return _HRAvg;
    }
    public int? HeartRateMax()
    {
        LoadCache();
        return _HRMax;
    }
    public int? RPMMin()
    {
        LoadCache();
        return _RPMMin;
    }
    public int? RPMAverage()
    {
        LoadCache();
        return _RPMAvg;
    }
    public int? RPMMax()
    {
        LoadCache();
        return _RPMMax;
    }
    public int? CADMin()
    {
        LoadCache();
        return _CADMin;
    }
    public int? CADAverage()
    {
        LoadCache();
        return _CADAvg;
    }
    public int? CADMax()
    {
        LoadCache();
        return _CADMax;
    }
    public int? WattsMin()
    {
        LoadCache();
        return _WattsMin;
    }
    public int? WattsAverage()
    {
        LoadCache();
        return _WattsAvg;
    }
    public int? WattsMax()
    {
        LoadCache();
        return _WattsMax;
    }
    public decimal? SpeedMin()
    {
        LoadCache();
        return _SpeedMin;
    }
    public decimal? SpeedAverage()
    {
        LoadCache();
        return _SpeedAvg;
    }
    public decimal? SpeedMax()
    {
        LoadCache();
        return _SpeedMax;
    }

}

public class DeviceLap
{
    public DeviceLap()
    {
        Track = new List<DevicePoint>();
    }
    public List<DevicePoint> Track;
    public double? StartSeconds;
    public decimal? Time;
    public decimal? Distance;
    private decimal? _EStart, _EGain, _ELoss, _EEnd;
    private int? _PointCount, _HRSum, _RPMSum, _CADSum, _PowerSum;
    private bool TrackCalculated = false;

    private void TrackCalc()
    {
        if (TrackCalculated || Track == null || !Track.Any()) return;
        _PointCount = Track.Count;
        _HRSum = _RPMSum = _PowerSum = 0;
        _EStart = Track[0].Altitude ?? 0;
        _EEnd = Track[Track.Count - 1].Altitude ?? 0;
        _EGain = _ELoss = 0;
        decimal Last = -1;
        foreach (var T in Track)
        {

            if (T.Altitude == null) continue;
            if (Last != -1)
            {
                if (Math.Abs((T.Altitude - Last).Value) > 50) continue;
                if (T.Altitude > Last)
                {
                    _EGain += (T.Altitude - Last);
                }
                else if (T.Altitude != null)
                {
                    _ELoss += (Last - T.Altitude);
                }
            }
            Last = T.Altitude ?? Last;

        }
        TrackCalculated = true;
    }

    public void LoadFromTrack()
    {
        // Need to load these link results into a variable and then do avg/max to avoid running the lists twice... but that's for later.
        if (HeartRateAvg == null)
            HeartRateAvg = (int?)(from H in Track where H.HR != null select H.HR).Average();
        if (HeartRateMax == null)
            HeartRateMax = (from H in Track where H.HR != null select H.HR).Max();

        if (RPMAvg == null)
            RPMAvg = (int?)(from H in Track where H.RPM != null select H.RPM).Average();
        if (RPMMax == null)
            RPMMax = (from H in Track where H.RPM != null select H.RPM).Max();

        if (CADAvg == null)
            CADAvg = (int?)(from H in Track where H.CAD != null select H.CAD).Average();
        if (CADMax == null)
            CADMax = (from H in Track where H.CAD != null select H.CAD).Max();

        if (WattsAvg == null)
            WattsAvg = (int?)(from H in Track where H.Watts != null select H.Watts).Average();
        if (WattsMax == null)
            WattsMax = (from H in Track where H.Watts != null select H.Watts).Max();

        if (SpeedAvg == null)
            SpeedAvg = (int?)(from H in Track where H.Speed != null select H.Speed).Average();
        if (SpeedMax == null)
            SpeedMax = (from H in Track where H.Speed != null select H.Speed).Max();

        if (TempAvg == null)
            TempAvg = (int?)(from H in Track where H.Temp != null select H.Temp).Average();
        if (TempMax == null)
            TempMax = (from H in Track where H.Temp != null select H.Temp).Max();

        if (GroundContactAvg == null)
            GroundContactAvg = (int?)(from H in Track where H.GroundContact != null select H.GroundContact).Average();
        if (GroundContactMax == null)
            GroundContactMax = (from H in Track where H.GroundContact != null select H.GroundContact).Max();

        if (VerticalOscillationAvg == null)
            VerticalOscillationAvg = (int?)(from H in Track where H.VerticalOscillation != null select H.VerticalOscillation).Average();
        if (VerticalOscillationMax == null)
            VerticalOscillationMax = (from H in Track where H.VerticalOscillation != null select H.VerticalOscillation).Max();

        TrackCalculated = false;
        TrackCalc();
    }

    public decimal? ElevationStart
    {
        get
        {
            TrackCalc();
            return _EStart;
        }
    }
    public decimal? ElevationGain
    {
        get
        {
            TrackCalc();
            return _EGain;
        }
    }
    public decimal? ElevationLoss
    {
        get
        {
            TrackCalc();
            return _ELoss;
        }
    }
    public decimal? ElevationEnd
    {
        get
        {
            TrackCalc();
            return _EEnd;
        }
    }
    public int? PointCount
    {
        get
        {
            TrackCalc();
            return _PointCount;
        }
    }

    public int? HRSum
    {
        get
        {
            TrackCalc();
            return _HRSum;
        }
    }
    public int? RPMSum
    {
        get
        {
            TrackCalc();
            return _RPMSum;
        }
    }
    public int? CADSum
    {
        get
        {
            TrackCalc();
            return _CADSum;
        }
    }
    public int? PowerSum
    {
        get
        {
            TrackCalc();
            return _PowerSum;
        }
    }

    public int? Calories;

    public int? HeartRateMin
    {
        get { return 0; }
    }
    public int? HeartRateAvg;
    public int? HeartRateMax;

    public int? RPMMin
    {
        get { return 0; }
    }
    public int? RPMAvg;
    public int? RPMMax;

    public int? CADMin
    {
        get { return 0; }
    }
    public int? CADAvg;
    public int? CADMax;

    public int? WattsMin
    {
        get { return 0; }
    }
    public int? WattsAvg;
    public int? WattsMax;

    public decimal? SpeedMin
    {
        get { return 0; }
    }
    public decimal? SpeedAvg;
    public decimal? SpeedMax;

    public int? TempMin
    {
        get { return 0; }
    }
    public int? TempAvg;
    public int? TempMax;

    public decimal? GroundContactMin
    {
        get { return 0; }
    }
    public decimal? GroundContactAvg;
    public decimal? GroundContactMax;

    public decimal? VerticalOscillationMin
    {
        get { return 0; }
    }
    public decimal? VerticalOscillationAvg;
    public decimal? VerticalOscillationMax;

}

public class DevicePoint
{
    [System.Xml.Serialization.XmlElementAttribute(IsNullable = true)]
    public int? LapNumber;
    [System.Xml.Serialization.XmlElementAttribute(IsNullable = true)]
    public double? StartSeconds;
    [System.Xml.Serialization.XmlElementAttribute(IsNullable = true)]
    public decimal? Latitude;
    [System.Xml.Serialization.XmlElementAttribute(IsNullable = true)]
    public decimal? Longitude;
    [System.Xml.Serialization.XmlElementAttribute(IsNullable = true)]
    public decimal? Altitude;
    [System.Xml.Serialization.XmlElementAttribute(IsNullable = true)]
    public decimal? Distance;
    [System.Xml.Serialization.XmlElementAttribute(IsNullable = true)]
    public decimal? Speed;
    [System.Xml.Serialization.XmlElementAttribute(IsNullable = true)]
    public int? HR;
    [System.Xml.Serialization.XmlElementAttribute(IsNullable = true)]
    public int? RPM;
    [System.Xml.Serialization.XmlElementAttribute(IsNullable = true)]
    public int? CAD;
    [System.Xml.Serialization.XmlElementAttribute(IsNullable = true)]
    public int? Watts;
    [System.Xml.Serialization.XmlElementAttribute(IsNullable = true)]
    public int? Calories;
    [System.Xml.Serialization.XmlElementAttribute(IsNullable = true)]
    public int? Temp;
    [System.Xml.Serialization.XmlElementAttribute(IsNullable = true)]
    public decimal? GroundContact;
    [System.Xml.Serialization.XmlElementAttribute(IsNullable = true)]
    public decimal? VerticalOscillation;

}