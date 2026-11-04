using System;
using System.Text.RegularExpressions;

namespace ShopHoa.Services;

/// <summary>
/// Quản lý và so sánh phiên bản theo chuẩn Semantic Versioning (SemVer 2.0).
/// Đảm bảo so sánh số học (ví dụ: 1.10.0 lớn hơn 1.2.0).
/// </summary>
public sealed class SemanticVersion : IComparable<SemanticVersion>, IEquatable<SemanticVersion>
{
    private static readonly Regex SemVerRegex = new(
        @"^[vV]?(?<major>\d+)(\.(?<minor>\d+))?(\.(?<patch>\d+))?(\.(?<build>\d+))?(-(?<prerelease>[0-9A-Za-z\.-]+))?(\+(?<metadata>[0-9A-Za-z\.-]+))?$",
        RegexOptions.Compiled);

    public int Major { get; }
    public int Minor { get; }
    public int Patch { get; }
    public int Build { get; }
    public string? PreRelease { get; }
    public string? BuildMetadata { get; }
    public string RawString { get; }

    public SemanticVersion(int major, int minor = 0, int patch = 0, int build = 0, string? preRelease = null, string? buildMetadata = null, string? rawString = null)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
        Build = build;
        PreRelease = preRelease;
        BuildMetadata = buildMetadata;
        RawString = rawString ?? $"{major}.{minor}.{patch}{(build > 0 ? $".{build}" : "")}{(string.IsNullOrEmpty(preRelease) ? "" : $"-{preRelease}")}";
    }

    public static bool TryParse(string? input, out SemanticVersion? version)
    {
        version = null;
        if (string.IsNullOrWhiteSpace(input))
            return false;

        var match = SemVerRegex.Match(input.Trim());
        if (!match.Success)
            return false;

        int major = int.Parse(match.Groups["major"].Value);
        int minor = match.Groups["minor"].Success ? int.Parse(match.Groups["minor"].Value) : 0;
        int patch = match.Groups["patch"].Success ? int.Parse(match.Groups["patch"].Value) : 0;
        int build = match.Groups["build"].Success ? int.Parse(match.Groups["build"].Value) : 0;
        string? prerelease = match.Groups["prerelease"].Success ? match.Groups["prerelease"].Value : null;
        string? metadata = match.Groups["metadata"].Success ? match.Groups["metadata"].Value : null;

        version = new SemanticVersion(major, minor, patch, build, prerelease, metadata, input.Trim());
        return true;
    }

    public static SemanticVersion Parse(string input)
    {
        if (TryParse(input, out var version) && version != null)
            return version;

        throw new FormatException($"Chuỗi '{input}' không đúng định dạng Semantic Version.");
    }

    public int CompareTo(SemanticVersion? other)
    {
        if (other is null)
            return 1;

        if (Major != other.Major)
            return Major.CompareTo(other.Major);

        if (Minor != other.Minor)
            return Minor.CompareTo(other.Minor);

        if (Patch != other.Patch)
            return Patch.CompareTo(other.Patch);

        if (Build != other.Build)
            return Build.CompareTo(other.Build);

        // Bản không có PreRelease luôn lớn hơn bản có PreRelease (ví dụ 1.0.0 > 1.0.0-rc1)
        if (string.IsNullOrEmpty(PreRelease) && !string.IsNullOrEmpty(other.PreRelease))
            return 1;
        if (!string.IsNullOrEmpty(PreRelease) && string.IsNullOrEmpty(other.PreRelease))
            return -1;
        if (!string.IsNullOrEmpty(PreRelease) && !string.IsNullOrEmpty(other.PreRelease))
            return string.Compare(PreRelease, other.PreRelease, StringComparison.OrdinalIgnoreCase);

        return 0;
    }

    public bool Equals(SemanticVersion? other)
    {
        if (other is null)
            return false;
        return CompareTo(other) == 0;
    }

    public override bool Equals(object? obj) => obj is SemanticVersion sv && Equals(sv);

    public override int GetHashCode() => HashCode.Combine(Major, Minor, Patch, Build, PreRelease);

    public override string ToString() => RawString;

    public static bool operator ==(SemanticVersion? left, SemanticVersion? right) => left?.CompareTo(right) == 0 || (left is null && right is null);
    public static bool operator !=(SemanticVersion? left, SemanticVersion? right) => !(left == right);
    public static bool operator <(SemanticVersion? left, SemanticVersion? right) => left is not null && left.CompareTo(right) < 0;
    public static bool operator <=(SemanticVersion? left, SemanticVersion? right) => left is null || left.CompareTo(right) <= 0;
    public static bool operator >(SemanticVersion? left, SemanticVersion? right) => left is not null && left.CompareTo(right) > 0;
    public static bool operator >=(SemanticVersion? left, SemanticVersion? right) => left is null ? right is null : left.CompareTo(right) >= 0;
}
