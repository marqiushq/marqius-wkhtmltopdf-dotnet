namespace Marqius.Wkhtmltopdf
{
    // A build from the source alone (no build.py): no programs were checked, so no digest is checked
    // either. The packages on nuget.org are made by build.py, which writes builds/Builds.cs instead.
    internal static class Builds
    {
        internal const string Version = "0.0.0-dev";

        internal static string Sha256(string runtimeId) => null;
    }
}
