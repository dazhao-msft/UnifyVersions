using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace UnifyVersions
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            if (args == null || args.Length < 1 || !Directory.Exists(args[0]))
            {
                Console.WriteLine("Root directory is expected.");
                return;
            }

            string rootDirectory = args[0];

            string[] projectFiles = Directory.GetFiles(rootDirectory, "*.csproj", SearchOption.AllDirectories);

            //
            // Collects package info.
            //

            List<Package> packages = new List<Package>();

            foreach (string projectFile in projectFiles)
            {
                var document = XDocument.Parse(File.ReadAllText(projectFile));

                var packageReferenceList = new List<XElement>();
                packageReferenceList.AddRange(document.Root.Elements("ItemGroup").SelectMany(p => p.Elements("PackageReference")));
                packageReferenceList.AddRange(document.Root.Elements("ItemGroup").SelectMany(p => p.Elements("DotNetCliToolReference")));

                foreach (var packageReference in packageReferenceList)
                {
                    string include = packageReference.Attribute("Include")?.Value ?? packageReference.Attribute("include")?.Value;

                    string version = packageReference.Attribute("Version")?.Value ?? packageReference.Attribute("version")?.Value;

                    if (string.IsNullOrEmpty(include) || string.IsNullOrEmpty(version))
                    {
                        Console.WriteLine($"Warning: invalid package reference: {packageReference.ToString()}");
                        continue;
                    }

                    if (StringComparer.OrdinalIgnoreCase.Equals(version, GetReferencedPackageVersionProperty(include)))
                    {
                        continue;
                    }

                    packages.Add(new Package() { Id = include, Version = version });
                }
            }

            var packageComparer = new PackageComparer();

            var uniquePackages = packages.Distinct(packageComparer).OrderBy(p => p, packageComparer).ToList();

            //
            // Rewrites project files with version properties.
            //

            foreach (string file in projectFiles)
            {
                var document = XDocument.Parse(File.ReadAllText(file));

                var packageReferenceList = new List<XElement>();
                packageReferenceList.AddRange(document.Root.Elements("ItemGroup").SelectMany(p => p.Elements("PackageReference")));
                packageReferenceList.AddRange(document.Root.Elements("ItemGroup").SelectMany(p => p.Elements("DotNetCliToolReference")));

                foreach (var packageReference in packageReferenceList)
                {
                    string include = packageReference.Attribute("Include")?.Value ?? packageReference.Attribute("include")?.Value;

                    var attribute = packageReference.Attribute("Version") ?? packageReference.Attribute("version");

                    if (string.IsNullOrEmpty(include) || attribute == null)
                    {
                        Console.WriteLine($"Warning: invalid package reference: {packageReference.ToString()}");
                        continue;
                    }

                    attribute.Value = GetReferencedPackageVersionProperty(include);
                }

                document.Save(file);
            }

            Console.WriteLine("Copy the following to PackageVersions.props:");
            Console.WriteLine();

            var packageVersionProperties = uniquePackages.Select(p => $"<{GetPackageVersionProperty(p.Id)}>{p.Version}</{GetPackageVersionProperty(p.Id)}>").ToList();

            foreach (string packageVersionProperty in packageVersionProperties)
            {
                Console.WriteLine(packageVersionProperty);
            }

            Console.WriteLine("Completed.");

            Console.Read();
        }

        private static string GetPackageVersionProperty(string packageId) => "PackageVersion_" + packageId.Replace(".", "_");

        private static string GetReferencedPackageVersionProperty(string packageId) => $"$({GetPackageVersionProperty(packageId)})";

        private class Package
        {
            public string Id { get; set; }

            public string Version { get; set; }
        }

        private class PackageComparer : IComparer<Package>, IEqualityComparer<Package>
        {
            public int Compare(Package x, Package y)
            {
                int result = 0;

                result = StringComparer.OrdinalIgnoreCase.Compare(x.Id, y.Id);
                if (result != 0)
                {
                    return result;
                }

                result = StringComparer.OrdinalIgnoreCase.Compare(x.Version, y.Version);
                if (result != 0)
                {
                    return result;
                }

                return result;
            }

            public bool Equals(Package x, Package y)
            {
                return Compare(x, y) == 0;
            }

            public int GetHashCode(Package obj)
            {
                return StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Id) ^
                       StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Version);
            }
        }
    }
}
