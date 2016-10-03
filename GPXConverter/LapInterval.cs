/// <summary>
/// The lap interval.
/// </summary>
public class LapInterval_OLD
{
    public LapInterval_OLD(decimal value, IntervalUnit units)
    {
        this.Value = value;
        this.Units = units;
    }

    /// <summary>
    /// The interval unit.
    /// </summary>
    public enum IntervalUnit
    {
        /// <summary>
        /// The kilometers unit type.
        /// </summary>
        Kilometers,

        /// <summary>
        /// The miles unit type.
        /// </summary>
        Miles
    }

    /// <summary>
    /// Gets the value.
    /// </summary>
    public decimal Value { get; private set; }

    /// <summary>
    /// Gets the units.
    /// </summary>
    public IntervalUnit Units { get; private set; }
}
