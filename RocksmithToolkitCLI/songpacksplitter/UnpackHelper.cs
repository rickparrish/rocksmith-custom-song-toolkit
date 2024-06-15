using RocksmithToolkitLib.DLCPackage;
using RocksmithToolkitLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using System.Diagnostics;

namespace songpacksplitter {
    internal class UnpackHelper {
        private static StringBuilder errorsFound;

        // Copied from DLCPackerUnpacker and DLCPackageData
        internal static List<string> Unpack(List<string> sourceDirectories, string destinationDirectory) {
            Console.WriteLine("Unpack starting");

            List<string> result = UnpackSongs(sourceDirectories, destinationDirectory);

            Console.WriteLine($" - Created {result.Count} unpack directories:\r\n   - {string.Join("\r\n   - ", result)}");
            
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

        // Copied from DLCPackerUnpacker and DLCPackageData
        static List<string> UnpackSongs(IEnumerable<string> srcPaths, string destPath) {
            Packer.ErrMsg = new StringBuilder();
            errorsFound = new StringBuilder();

            var unpackedDirs = new List<string>();

            foreach (string srcPath in srcPaths) {
                Console.WriteLine($" - Unpacking {srcPath}");

                Platform srcPlatform = new Platform(GamePlatform.Pc, GameVersion.RS2014);
                string unpackedDir;

                try {
                    unpackedDir = Packer.Unpack(srcPath, destPath, srcPlatform, false, false, false, false);
                    unpackedDirs.Add(unpackedDir);

                    // songs.psarc doesn't have an appid.appid file, so create one if necessary
                    string appidFilename = Path.Combine(unpackedDir, "appid.appid");
                    if (!File.Exists(appidFilename)) {
                        File.WriteAllText(appidFilename, "221680");
                    }
                } catch (Exception ex) {
                    errorsFound.AppendLine(String.Format("<ERROR> Unpacking file: {0}{1}{2}", Path.GetFileName(srcPath), Environment.NewLine, ex.Message));
                    continue;
                }

                // Enumerate *.bnk files
                var bnkWemList = new List<BnkWemData>();
                var bnkFiles = Directory.EnumerateFiles(unpackedDir, "song_*.bnk", SearchOption.AllDirectories).ToList();
                foreach (var bnkFile in bnkFiles) {
                    var bnkWemData = new BnkWemData { BnkFileName = bnkFile, WemFileId = SoundBankGenerator2014.ReadWemFileId(bnkFile, srcPlatform) };
                    bnkWemList.Add(bnkWemData);
                }

                // Give wem files friendly names
                var wemFiles = Directory.EnumerateFiles(unpackedDir, "*.wem", SearchOption.AllDirectories).ToList();
                foreach (string wemFile in wemFiles) {
                    // Don't rename wem files that were previously renamed already
                    if (wemFile.Contains("song_")) {
                        continue;
                    }

                    foreach (var item in bnkWemList) {
                        if (Path.GetFileName(wemFile).Contains(item.WemFileId)) {
                            var friendlyWemFile = Path.Combine(Path.GetDirectoryName(wemFile), Path.GetFileNameWithoutExtension(wemFile) + "_" + Path.GetFileName(Path.ChangeExtension(item.BnkFileName, ".wem")));
                            File.Move(wemFile, friendlyWemFile);
                            break;
                        }
                    }
                }
            }

            // insert any Packer error messages
            if (!String.IsNullOrEmpty(Packer.ErrMsg.ToString()))
                errorsFound.Insert(0, Packer.ErrMsg.ToString());

            return unpackedDirs;
        }
    }
}
