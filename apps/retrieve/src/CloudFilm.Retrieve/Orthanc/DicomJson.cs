using System.Globalization;
using System.Text.Json;

namespace CloudFilm.Retrieve.Orthanc;

/// <summary>
/// Readers for the DICOM JSON model returned by QIDO-RS.
/// </summary>
/// <remarks>
/// Every attribute is keyed by its eight character tag and wraps its data in a
/// <c>Value</c> array, which may be absent entirely when the attribute is empty.
/// Person names nest one level further. Reading these defensively matters: a
/// study missing a description is ordinary, and must not fail the query.
/// </remarks>
internal static class DicomJson
{
    /// <summary>Patient's Name.</summary>
    public const string PatientName = "00100010";

    /// <summary>Patient ID.</summary>
    public const string PatientId = "00100020";

    /// <summary>Patient's Birth Date.</summary>
    public const string PatientBirthDate = "00100030";

    /// <summary>Patient's Sex.</summary>
    public const string PatientSex = "00100040";

    /// <summary>Study Date.</summary>
    public const string StudyDate = "00080020";

    /// <summary>Study Description.</summary>
    public const string StudyDescription = "00081030";

    /// <summary>Modalities in Study.</summary>
    public const string ModalitiesInStudy = "00080061";

    /// <summary>Study Instance UID.</summary>
    public const string StudyInstanceUid = "0020000D";

    /// <summary>Number of Study Related Series.</summary>
    public const string SeriesCount = "00201206";

    /// <summary>Number of Study Related Instances.</summary>
    public const string InstanceCount = "00201208";

    /// <summary>
    /// Reads the first value of an attribute as text.
    /// </summary>
    /// <param name="item">One QIDO-RS result.</param>
    /// <param name="tag">The eight character DICOM tag.</param>
    /// <returns>The value, or an empty string when absent.</returns>
    public static string Text(JsonElement item, string tag)
    {
        if (!TryFirstValue(item, tag, out JsonElement value))
        {
            return string.Empty;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Number => value.ToString(),
            _ => string.Empty,
        };
    }

    /// <summary>
    /// Reads a Person Name attribute.
    /// </summary>
    /// <param name="item">One QIDO-RS result.</param>
    /// <param name="tag">The eight character DICOM tag.</param>
    /// <returns>The alphabetic representation with component separators replaced by spaces.</returns>
    /// <remarks>
    /// Person Name components are caret separated and ordered family, given,
    /// middle, prefix, suffix. Word order differs by culture, so the separators
    /// are replaced rather than reordered into a guess.
    /// </remarks>
    public static string PersonName(JsonElement item, string tag)
    {
        if (!TryFirstValue(item, tag, out JsonElement value))
        {
            return string.Empty;
        }

        string raw = value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Object when value.TryGetProperty("Alphabetic", out JsonElement alphabetic)
                => alphabetic.GetString() ?? string.Empty,
            _ => string.Empty,
        };

        return string.Join(' ', raw.Split('^', StringSplitOptions.RemoveEmptyEntries |
                                               StringSplitOptions.TrimEntries));
    }

    /// <summary>
    /// Reads an integer attribute.
    /// </summary>
    /// <param name="item">One QIDO-RS result.</param>
    /// <param name="tag">The eight character DICOM tag.</param>
    /// <returns>The value, or zero when absent or unparseable.</returns>
    /// <remarks>
    /// Integer String attributes are legally either a JSON number or a JSON
    /// string; archives differ, so both are accepted.
    /// </remarks>
    public static int Integer(JsonElement item, string tag)
    {
        if (!TryFirstValue(item, tag, out JsonElement value))
        {
            return 0;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt32(out int number) => number,
            JsonValueKind.String when int.TryParse(
                value.GetString(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int parsed) => parsed,
            _ => 0,
        };
    }

    /// <summary>
    /// Converts a DICOM date to an ISO 8601 calendar date.
    /// </summary>
    /// <param name="dicomDate">A date in <c>YYYYMMDD</c> form.</param>
    /// <returns>The ISO date, or <c>Unknown</c> when the input is not a DICOM date.</returns>
    public static string IsoDate(string dicomDate)
    {
        return dicomDate.Length == 8 && dicomDate.All(char.IsAsciiDigit)
            ? $"{dicomDate[..4]}-{dicomDate.Substring(4, 2)}-{dicomDate.Substring(6, 2)}"
            : "Unknown";
    }

    /// <summary>
    /// Converts a DICOM patient sex code to a display value.
    /// </summary>
    /// <param name="code">The DICOM code, normally <c>M</c>, <c>F</c> or <c>O</c>.</param>
    /// <returns>A display value, defaulting to <c>Unknown</c>.</returns>
    public static string Gender(string code) => code.Trim().ToUpperInvariant() switch
    {
        "M" => "Male",
        "F" => "Female",
        "O" => "Other",
        _ => "Unknown",
    };

    private static bool TryFirstValue(JsonElement item, string tag, out JsonElement value)
    {
        value = default;

        if (!item.TryGetProperty(tag, out JsonElement attribute) ||
            !attribute.TryGetProperty("Value", out JsonElement values) ||
            values.ValueKind != JsonValueKind.Array ||
            values.GetArrayLength() == 0)
        {
            return false;
        }

        value = values[0];
        return true;
    }
}
