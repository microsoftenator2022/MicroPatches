using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Kingmaker.Modding;

namespace MicroPatches.Patches
{
    #if false
    internal static class OwlmodDependencyVersionFix
    {
        static readonly Regex VersionRegex = new(@"(\d+)(?:\.(\d+))?(?:\.(\d+))?.*");

        static bool DependencyVersionCheck(OwlcatModificationManifest.Dependency dep, OwlcatModification mod)
        {
            int? Major(Match match)
            {
                if (int.TryParse(match.Groups[1].Value, out var value))
                    return value;

                return null;
            }

            int? Minor(Match match)
            {
                if (match.Groups[2].Success && int.TryParse(match.Groups[2].Value, out var value))
                {
                    return value;
                }

                return null;
            }

            int? Rev(Match match)
            {
                if (match.Groups[3].Success && int.TryParse(match.Groups[3].Value, out var value))
                {
                    return value;
                }

                return null;
            }

            Version? TryParse(string versionString)
            {
                var match = VersionRegex.Match(versionString);

                if (!match.Success)
                    return null;

                if (Major(match) is not int major)
                    return null;

                var minor = Minor(match) ?? 0;
                var rev = Rev(match) ?? 0;

                return new(major, minor, rev);
            }
            
            if (TryParse(dep.Version) is not { } dependencyVersion || TryParse(mod.Manifest.Version) is not { } modVersion)
                return false;

            return modVersion >= dependencyVersion;
        }
    }
    #endif
}
