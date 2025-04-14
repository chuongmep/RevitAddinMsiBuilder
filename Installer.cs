using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ICSharpCode.SharpZipLib.Zip;
using WixSharp;
using WixSharp.CommonTasks;
using WixSharp.Controls;
using System.CommandLine;
using System.Threading.Tasks;
using File = System.IO.File;

namespace RevitAddinMsiBuilder
{
    class Program
    {
        const string installationDir = @"%AppDataFolder%\Autodesk\Revit\Addins\";
        const string defaultOutputDir = "output";
        static string version = $"1.0.{GetLastTwoDigitOfYear()}.{GetDayInYear()}";
        static readonly Regex versionRegex = new Regex(@"\d{4}");
        const string defaultProjectName = "RevitAddinMsi"; // Fallback if no name provided

        static async Task<int> Main(string[] args)
        {
            // Define CLI options
            var addinPathOption = new Option<string>(
                aliases: new[] { "--addin-path", "-a" },
                description: "Path to the .addin file or directory containing it")
                { IsRequired = true };

            var revitVersionsOption = new Option<string[]>(
                aliases: new[] { "--revit-versions", "-r" },
                description: "Target Revit versions (e.g., 2022,2023)")
                { AllowMultipleArgumentsPerToken = true };

            var outputDirOption = new Option<string>(
                aliases: new[] { "--output-dir", "-o" },
                description: "Output directory for MSI and ZIP",
                getDefaultValue: () => defaultOutputDir);

            var projectNameOption = new Option<string>(
                aliases: new[] { "--project-name", "-n" },
                description: "Name of the project for MSI and output file (defaults to .addin Name or RevitAddinMsi)");

            var rootCommand = new RootCommand("CLI tool to build MSI for Revit add-ins. Example: RevitAddinMsiBuilder -a path/to/hello.addin");
            rootCommand.AddOption(addinPathOption);
            rootCommand.AddOption(revitVersionsOption);
            rootCommand.AddOption(outputDirOption);
            rootCommand.AddOption(projectNameOption);

            // Interactive mode if no arguments are provided
            if (args.Length == 0)
            {
                Console.Write("Enter .addin file path: ");
                string addinPath = Console.ReadLine();
                Console.Write("Enter Revit versions (e.g., 2022 2023, or press Enter to infer): ");
                var versionsInput = Console.ReadLine();
                string[] revitVersions = string.IsNullOrWhiteSpace(versionsInput) ? Array.Empty<string>() : versionsInput.Split();
                Console.Write("Enter output directory (default: output): ");
                string outputDir = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(outputDir)) outputDir = defaultOutputDir;
                Console.Write("Enter project name (or press Enter for default): ");
                string projectName = Console.ReadLine();
                args = new[]
                {
                    "--addin-path", addinPath,
                    "--revit-versions", string.Join(" ", revitVersions),
                    "--output-dir", outputDir,
                    "--project-name", projectName
                }.Where(a => !string.IsNullOrWhiteSpace(a)).ToArray();
            }

            rootCommand.SetHandler((addinPath, revitVersions, outputDir, projectName) =>
            {
                try
                {
                    // Validate inputs
                    if (!File.Exists(addinPath) && !Directory.Exists(addinPath))
                    {
                        Console.WriteLine($"Error: '{addinPath}' does not exist.");
                        return;
                    }

                    // Find .addin file
                    string addinFile = addinPath;
                    if (Directory.Exists(addinPath))
                    {
                        addinFile = Directory.GetFiles(addinPath, "*.addin", SearchOption.AllDirectories)
                            .FirstOrDefault();
                        if (addinFile == null)
                        {
                            Console.WriteLine($"Error: No .addin file found in '{addinPath}'.");
                            return;
                        }
                    }

                    // Parse .addin file
                    var (assemblyPaths, addinName) = ParseAddinFile(addinFile);
                    if (!assemblyPaths.Any())
                    {
                        Console.WriteLine("Error: No valid assemblies found in .addin file.");
                        return;
                    }

                    // Validate Revit versions
                    var validVersions = new HashSet<string>(revitVersions?.Any() == true
                        ? revitVersions
                        : InferRevitVersions(Path.GetDirectoryName(addinFile)));
                    if (!validVersions.Any())
                    {
                        Console.WriteLine("Error: No valid Revit versions specified or inferred.");
                        return;
                    }

                    // Determine project name
                    projectName = SanitizeProjectName(projectName ?? addinName ?? defaultProjectName);
                    Console.WriteLine($"Using project name: {projectName}");

                    // Build MSI
                    string msiPath = BuildMsi(addinFile, assemblyPaths, validVersions, outputDir, addinName, projectName);
                    if (msiPath == null)
                    {
                        Console.WriteLine("Error: MSI build failed.");
                        return;
                    }

                    // Compress to ZIP
                    string zipPath = Path.Combine(outputDir, $"{projectName}-{version}.zip");
                    CompressFile(msiPath, zipPath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                }
            }, addinPathOption, revitVersionsOption, outputDirOption, projectNameOption);

            return await rootCommand.InvokeAsync(args);
        }

        // Rest of your code (ParseAddinFile, BuildMsi, etc.) remains unchanged
        static (List<string> AssemblyPaths, string AddinName) ParseAddinFile(string addinFile)
        {
            var doc = XDocument.Load(addinFile);
            var assemblyPaths = new List<string>();
            string addinName = "Unknown";

            var addins = doc.Descendants("AddIn");
            foreach (var addin in addins)
            {
                var assembly = addin.Element("Assembly")?.Value;
                addinName = addin.Element("Name")?.Value ?? addinName;
                if (string.IsNullOrEmpty(assembly))
                {
                    Console.WriteLine($"Warning: No Assembly specified in .addin file for AddIn {addinName}.");
                    continue;
                }

                string resolvedPath;
                if (Path.IsPathRooted(assembly))
                {
                    resolvedPath = assembly;
                }
                else
                {
                    resolvedPath = Path.Combine(Path.GetDirectoryName(addinFile), assembly);
                }

                try
                {
                    resolvedPath = Path.GetFullPath(resolvedPath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Invalid assembly path '{assembly}' in .addin file: {ex.Message}");
                    continue;
                }

                if (File.Exists(resolvedPath))
                {
                    if (!assemblyPaths.Contains(resolvedPath))
                    {
                        assemblyPaths.Add(resolvedPath);
                        Console.WriteLine($"Found assembly: {resolvedPath}");
                    }
                }
                else
                {
                    Console.WriteLine($"Warning: Assembly '{resolvedPath}' specified in .addin file does not exist.");
                }
            }

            return (assemblyPaths, addinName);
        }

        static IEnumerable<string> InferRevitVersions(string addinDir)
        {
            var versions = new HashSet<string>();
            if (Directory.Exists(addinDir))
            {
                var dirs = Directory.GetDirectories(addinDir);
                foreach (var d in dirs)
                {
                    var match = versionRegex.Match(Path.GetFileName(d));
                    if (match.Success && int.TryParse(match.Value, out int year) && year >= 2018 && year <= 2025)
                    {
                        versions.Add(match.Value);
                    }
                }
            }
            return versions;
        }

        static string BuildMsi(string addinFile, List<string> assemblyPaths, IEnumerable<string> revitVersions, string outputDir, string addinName, string projectName)
        {
            var fileName = new StringBuilder().Append(projectName).Append("-").Append(version);
            var project = new Project
            {
                Name = projectName,
                OutDir = outputDir,
                Platform = Platform.x64,
                Description = $"Revit Add-in: {addinName}",
                UI = WUI.WixUI_InstallDir,
                Version = new Version(version),
                OutFileName = fileName.ToString(),
                Scope = InstallScope.perUser,
                MajorUpgrade = MajorUpgrade.Default,
                GUID = new Guid("A46C86A0-71A5-460B-8536-98D3ED43B574"),
                BackgroundImage = File.Exists(@"Resources/Icons/BackgroundImage.png") ? @"Resources/Icons/BackgroundImage.png" : null,
                BannerImage = File.Exists(@"Resources/Icons/BannerImage.png") ? @"Resources/Icons/BannerImage.png" : null,
                ControlPanelInfo =
                {
                    Manufacturer = "Autodesk",
                    HelpLink = "https://github.com/chuongmep/RevitAddInManager/issues",
                    Comments = $"Revit Add-in: {addinName}",
                    ProductIcon = File.Exists(@"Resources/Icons/ShellIcon.ico") ? @"Resources/Icons/ShellIcon.ico" : null
                }
            };

            MajorUpgrade.Default.AllowSameVersionUpgrades = true;
            project.RemoveDialogsBetween(NativeDialogs.WelcomeDlg, NativeDialogs.InstallDirDlg);

            var versionStorages = new Dictionary<string, List<WixEntity>>();
            foreach (var version in revitVersions)
            {
                var versionFiles = new List<WixEntity>();
                var contentsFiles = new List<WixEntity>();

                if (File.Exists(addinFile))
                {
                    versionFiles.Add(new WixSharp.File(addinFile));
                    Console.WriteLine($"Added .addin file for version {version}: {addinFile}");
                }

                foreach (var assemblyPath in assemblyPaths)
                {
                    var assemblyDir = Path.GetDirectoryName(assemblyPath);
                    var parentDirName = Path.GetFileName(assemblyDir);
                    bool isVersionSpecific = versionRegex.IsMatch(parentDirName) && parentDirName == version;

                    if (isVersionSpecific || !versionRegex.IsMatch(parentDirName))
                    {
                        if (File.Exists(assemblyPath) && (assemblyPath.EndsWith(".dll") || assemblyPath.EndsWith(".exe") || assemblyPath.EndsWith(".config")))
                        {
                            contentsFiles.Add(new WixSharp.File(assemblyPath));
                            Console.WriteLine($"Added file to contents for version {version}: {assemblyPath}");
                        }

                        if (Directory.Exists(assemblyDir))
                        {
                            var relatedFiles = Directory.GetFiles(assemblyDir, "*.*", SearchOption.TopDirectoryOnly)
                                .Where(f => f.EndsWith(".dll") || f.EndsWith(".exe") || f.EndsWith(".config"))
                                .Where(f => !contentsFiles.Any(cf => cf is WixSharp.File wf && wf.Name.Equals(f, StringComparison.OrdinalIgnoreCase)));

                            foreach (var file in relatedFiles)
                            {
                                contentsFiles.Add(new WixSharp.File(file));
                                Console.WriteLine($"Added related file to contents for version {version}: {file}");
                            }
                        }
                    }
                }

                if (contentsFiles.Any())
                {
                    versionFiles.Add(new Dir("contents", contentsFiles.ToArray()));
                }

                if (versionFiles.Any())
                {
                    versionStorages[version] = versionFiles;
                }
                else
                {
                    Console.WriteLine($"Warning: No files added for version {version}.");
                }
            }

            if (!versionStorages.Any())
            {
                Console.WriteLine("Error: No files found for any Revit version.");
                return null;
            }

            project.Dirs = new Dir[]
            {
                new InstallDir(installationDir, versionStorages.Select(v =>
                    new Dir(v.Key, v.Value.ToArray())).Cast<WixEntity>().ToArray())
            };

            Directory.CreateDirectory(outputDir);
            try
            {
                return project.BuildMsi();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error building MSI: {ex.Message}");
                return null;
            }
        }

        static string SanitizeProjectName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return defaultProjectName;

            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = new string(name.Where(c => !invalidChars.Contains(c)).ToArray());
            return string.IsNullOrWhiteSpace(sanitized) ? defaultProjectName : sanitized.Trim();
        }

        static string GetDayInYear()
        {
            DateTime now = DateTime.Now;
            DateTime startOfYear = new DateTime(now.Year, 1, 1);
            return (now - startOfYear).Days.ToString();
        }

        static string GetLastTwoDigitOfYear()
        {
            DateTime now = DateTime.Now;
            return now.Year.ToString().Substring(2, 2);
        }

        static void CompressFile(string filePath, string outputFilePath, int compressLevel = 9)
        {
            try
            {
                using (var outputStream = new ZipOutputStream(File.Create(outputFilePath)))
                {
                    outputStream.SetLevel(compressLevel);
                    var buffer = new byte[4096];
                    var entry = new ZipEntry(Path.GetFileName(filePath)) { DateTime = DateTime.Now };
                    outputStream.PutNextEntry(entry);

                    using (var fs46 = File.OpenRead(filePath))
                    {
                        int sourceBytes;
                        do
                        {
                            sourceBytes = fs46.Read(buffer, 0, buffer.Length);
                            outputStream.Write(buffer, 0, sourceBytes);
                        } while (sourceBytes > 0);
                    }

                    outputStream.Finish();
                    outputStream.Close();
                    Console.WriteLine($"Zip file created: {outputFilePath}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating ZIP: {ex.Message}");
            }
        }
    }
}