using RocksmithToolkitLib;
using RocksmithToolkitLib.DLCPackage;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace songpacksplitter {
    internal class PackHelper {
        private static StringBuilder errorsFound;

        internal static List<string> Pack(List<string> sourceDirectories, string destinationDirectory) {
            List<string> result = new List<string>();

            Console.WriteLine("Pack starting");

            foreach (string sourceDirectory in sourceDirectories) {
                Console.WriteLine($" - Packing {sourceDirectory}");

                string psarcType = Path.GetFileName(Path.GetDirectoryName(sourceDirectory));
                string psarcFilename = Path.Combine(destinationDirectory, psarcType, Path.GetFileName(sourceDirectory) + ".psarc");

                // Only create the .psarc file if it doesn't exist, or if the source data is newer
                if (File.Exists(psarcFilename)) {
                    var lastWriteTimes = Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories).Select(f => File.GetLastWriteTimeUtc(f));
                    if (lastWriteTimes.All(x => x <= File.GetLastWriteTimeUtc(psarcFilename))) {
                        continue;
                    }
                }

                result.Add(PackSong(sourceDirectory, psarcFilename));
            }

            Console.WriteLine($" - Created {result.Count} psarc files:\r\n   - {string.Join("\r\n   - ", result)}");

            if (errorsFound.Length > 0) {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Errors found: {errorsFound}");
                Console.ForegroundColor = ConsoleColor.Gray;
            }

            if (Debugger.IsAttached) {
                Console.WriteLine("Hit ENTER to continue to the next step...");
                Console.ReadLine();
            }

            return result;
        }

        // Copied from DLCPackerUnpacker
        static string PackSong(string srcPath, string destPath) {
            errorsFound = new StringBuilder();
            var archivePath = String.Empty;

            try {
                Directory.CreateDirectory(Path.GetDirectoryName(destPath));

                Platform destPlatform = new Platform(GamePlatform.Pc, GameVersion.RS2014);

                archivePath = Packer.Pack(srcPath, destPath, destPlatform, false, false);
            } catch (OutOfMemoryException ex) {
                errorsFound.AppendLine(String.Format("{0}\n{1}", ex.Message, ex.InnerException) + Environment.NewLine +
                "Toolkit is not capable of repacking some large system artifact files.   " + Environment.NewLine +
                "Defragging the hard drive and clearing the 'pagefile.sys' may help.");
            } catch (Exception ex) {
                errorsFound.AppendLine(String.Format("{0}\n{1}", ex.Message, ex.InnerException) + Environment.NewLine +
                "Confirm GamePlatform and GameVersion are set correctly for" + Environment.NewLine +
                "the desired destination in the toolkit Configuration settings.");
            }

            return archivePath;
        }
    }
}
