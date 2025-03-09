using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;

namespace Pinger;

internal static class Update
{
    private static string? __CurrentVersion;
    public static VersionInfo CurrentVersion => __CurrentVersion ??= typeof(Update).Assembly.GetName().Version!.ToString();

    private static string __RepositoryUri = "https://github.com/Infarh/Pinger";

    public static async Task CheckUpdateAsync(CancellationToken Cancel = default)
    {

    }
}

internal readonly partial struct VersionInfo : IComparable<VersionInfo>, IComparable<string>, IEquatable<VersionInfo>
{
    private static readonly Regex __VersionRegex = GetVersionRegex();

    [GeneratedRegex(@"(?<major>\d+)\.(?<minor>\d+)(?:\.(?<subversion>\d+)(?:\.(?<build>\d+))?)?", RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex GetVersionRegex();

    public int Major { get; }

    public int Minor { get; }

    public int Subversion { get; }

    public int Build { get; }

    public VersionInfo(string VersionString)
    {
        if(__VersionRegex.Match(VersionString) is not
           {
               Success: true, 
               Groups: [_, { Value: var major_s }, { Value: var minor_s }, { Value: var sub_s }, { Value: var build_s }]
           })
            throw new ArgumentException("Invalid version string.", nameof(VersionString));

        Major = int.Parse(major_s);
        Minor = int.Parse(minor_s);
        Subversion = sub_s is { Length: > 0 } ? int.Parse(sub_s) : 0;
        Build = build_s is { Length: > 0 } ? int.Parse(build_s) : 0;
    }

    public override string ToString()
    {
        var str = new StringBuilder(15).Append(Major).Append('.').Append(Minor);
        if(Subversion == 0 && Build == 0) return str.ToString();

        str.Append('.').Append(Subversion);
        if(Build != 0)
            str.Append('.').Append(Build);

        return str.ToString();
    }

    public override bool Equals([NotNullWhen(true)] object? obj) => obj is VersionInfo other && Equals(other);

    public static implicit operator VersionInfo(string VersionString) => new(VersionString);

    public int CompareTo(VersionInfo other)
    {
        var major_comparison = Major.CompareTo(other.Major);
        if (major_comparison != 0) return major_comparison;

        var minor_comparison = Minor.CompareTo(other.Minor);
        if (minor_comparison != 0) return minor_comparison;

        var subversion_comparison = Subversion.CompareTo(other.Subversion);
        if (subversion_comparison != 0) return subversion_comparison;

        return Build.CompareTo(other.Build);
    }

    public int CompareTo(string? other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return CompareTo(new VersionInfo(other));
    }

    public bool Equals(VersionInfo other) => 
        Major == other.Major && 
        Minor == other.Minor && 
        Subversion == other.Subversion && 
        Build == other.Build;

    public override int GetHashCode() => HashCode.Combine(Major, Minor, Subversion, Build);

    public static bool operator ==(VersionInfo left, VersionInfo right) => left.Equals(right);

    public static bool operator !=(VersionInfo left, VersionInfo right) => !left.Equals(right);
}
