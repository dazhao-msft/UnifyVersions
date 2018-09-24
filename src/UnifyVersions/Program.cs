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
            if (args == null || args.Length != 2)
            {
                Console.WriteLine("Expected arguments: {path to root directory} {path to PackageVersions.props");
                return;
            }

            string rootDirectory = args[0];
            if (!Directory.Exists(rootDirectory))
            {
                Console.WriteLine("Root directory doesn't exist");
            }

            string pkgPropsFile = args[1];
            if (!File.Exists(pkgPropsFile) ||
                !StringComparer.OrdinalIgnoreCase.Equals("PackageVersions.props", Path.GetFileName(pkgPropsFile)))
            {
                Console.WriteLine("PackageVersions.props is not found.");
            }

            string[] projectFiles = Directory.GetFiles(rootDirectory, "*.csproj", SearchOption.AllDirectories);

            HashSet<Package> packages = GetAllPackages(projectFiles);

            RewriteProjectFiles(projectFiles);

            PrintPackagePropertiesToCopy(packages);

            Console.WriteLine("Completed.");

            Console.Read();
        }

        private static HashSet<Package> GetAllPackages(IEnumerable<string> projectFiles)
        {
            var packages = new HashSet<Package>(new PackageComparer());

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

                    packages.Add(new Package(include, version));
                }
            }

            return packages;
        }

        private static void RewriteProjectFiles(IEnumerable<string> projectFiles)
        {
            foreach (string projectFile in projectFiles)
            {
                var document = XDocument.Parse(File.ReadAllText(projectFile));

                var packageReferenceList = new List<XElement>();
                packageReferenceList.AddRange(document.Root.Elements("ItemGroup").SelectMany(p => p.Elements("PackageReference")));
                packageReferenceList.AddRange(document.Root.Elements("ItemGroup").SelectMany(p => p.Elements("DotNetCliToolReference")));

                foreach (var packageReference in packageReferenceList)
                {
                    string include = packageReference.Attribute("Include")?.Value ?? packageReference.Attribute("include")?.Value;

                    var attribute = packageReference.Attribute("Version") ?? packageReference.Attribute("version");

                    if (string.IsNullOrEmpty(include) || string.IsNullOrEmpty(attribute?.Value))
                    {
                        Console.WriteLine($"Warning: invalid package reference: {packageReference.ToString()}");
                        continue;
                    }

                    var package = new Package(include, /* version doesn't matter here */ string.Empty);

                    attribute.Value = package.MSBuildReferencedPackageVersionProperty;
                }

                document.Save(projectFile);
            }
        }

        private static void PrintPackagePropertiesToCopy(IEnumerable<Package> packages)
        {
            Console.WriteLine("Copy the following to PackageVersions.props:");
            Console.WriteLine();

            var packagesToAdd = packages.Where(p => p.Version != p.MSBuildReferencedPackageVersionProperty)
                                        .OrderBy(p => p, new PackageComparer())
                                        .ToList();

            foreach (var packageToAdd in packagesToAdd)
            {
                Console.WriteLine(string.Format("<{0}>{1}</{0}>", packageToAdd.Id, packageToAdd.Version));
            }
        }

        private static List<string> GetAllMSBuildPackageVersionProperties(string packagePropsFile)
        {
            XNamespace MSBuildNamespace = "http://schemas.microsoft.com/developer/msbuild/2003";

            return new List<string>();
        }

        private class Package
        {
            public Package(string id, string version)
            {
                Id = id ?? throw new ArgumentNullException(nameof(id));
                Version = version ?? throw new ArgumentNullException(nameof(version));
            }

            public string Id { get; }

            public string Version { get; }

            public string MSBuildPackageVersionProperty => "PackageVersion_" + Id.Replace(".", "_");

            public string MSBuildReferencedPackageVersionProperty => $"$({MSBuildPackageVersionProperty})";
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
