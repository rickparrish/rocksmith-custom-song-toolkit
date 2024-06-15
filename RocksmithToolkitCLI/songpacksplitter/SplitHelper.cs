using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace songpacksplitter {
    internal class SplitHelper {
        private static void CopyAudio(string sourceDirectory, string destinationDirectory, string dlcKey) {
            string subDirectory = @"audio\windows";

            if (Directory.Exists(Path.Combine(sourceDirectory, subDirectory))) {
                CopyFiles(sourceDirectory, subDirectory, destinationDirectory, $"song_{dlcKey}*.bnk");

                string[] noThrowDlcKeys = new string[] { "rs2chordnamestress", "rs2levelbreak", "rs2tails" };
                bool throwOnZero = !noThrowDlcKeys.Contains(dlcKey);

                // .wem files are named in {id}_song_{dlckey}.wem format, and we want {id}.wem, so we can't use CopyFiles()
                string[] wemFiles = Directory.GetFiles(Path.Combine(sourceDirectory, subDirectory), $"*song_{dlcKey}*.wem", SearchOption.TopDirectoryOnly);
                if (!wemFiles.Any()) {
                    if (throwOnZero) {
                        throw new Exception($"No audio .wem files found for {dlcKey}");
                    }
                }
                foreach (string wemFile in wemFiles) {
                    string newFilename = Path.GetFileName(wemFile).Split('_').First() + ".wem";
                    FileCopyIfNewer(wemFile, Path.Combine(destinationDirectory, subDirectory, newFilename));
                }
            }
        }

        private static void CopyFiles(string sourceDirectory, string subDirectory, string destinationDirectory, string searchPattern, bool throwOnZero = true) {
            string sourceSubdirectory = Path.Combine(sourceDirectory, subDirectory);
            string destinationSubdirectory = Path.Combine(destinationDirectory, subDirectory);

            if (Directory.Exists(sourceSubdirectory)) {
                string[] filesToCopy = Directory.GetFiles(sourceSubdirectory, searchPattern, SearchOption.TopDirectoryOnly);
                if (filesToCopy.Any()) {
                    Directory.CreateDirectory(destinationSubdirectory);

                    foreach (string fileToCopy in filesToCopy) {
                        string newFilename = Path.Combine(destinationSubdirectory, Path.GetFileName(fileToCopy));
                        FileCopyIfNewer(fileToCopy, newFilename);
                    }
                    return;
                }
            }

            // If we get here, we didn't find any files using any of the search patterns
            if (throwOnZero) {
                throw new Exception($"No files to copy found in {sourceSubdirectory} using {searchPattern}");
            }
        }

        private static void CopyManifests(string sourceDirectory, string destinationDirectory, string dlcKey, bool throwOnZero) {
            string subdirectory = $@"manifests\songs_dlc_{dlcKey}";
            string sourceSubdirectory = Path.Combine(sourceDirectory, "manifests");
            string destinationSubdirectory = Path.Combine(destinationDirectory, subdirectory);

            Directory.CreateDirectory(destinationSubdirectory);

            // Manifests are in a subdirectory with a name based on the songpack, so we need to search AllDirectories instead of TopDirectoryOnly
            // This means we need to copy manually, instead of calling CopyFiles()
            string[] filesToCopy = Directory.GetFiles(sourceSubdirectory, $"{dlcKey}*.json", SearchOption.AllDirectories);
            if (!filesToCopy.Any()) {
                if (throwOnZero) {
                    throw new Exception($"No manifests .json files found for {dlcKey}");
                }
            }
            foreach (string fileToCopy in filesToCopy) {
                FileCopyIfNewer(fileToCopy, Path.Combine(destinationSubdirectory, Path.GetFileName(fileToCopy)));
            }

            // The manifests directory also has a .hsan file with a name based on the songpack.  It contains an entry for each arrangement
            // for all songs in the songpack.  For now we'll just copy the full file, but if that doesn't work properly, then we'll need to
            // create a new one with just this song's entries
            string hsanFilename = $"songs_dlc_{dlcKey}.hsan";
            filesToCopy = Directory.GetFiles(sourceSubdirectory, $"*.hsan", SearchOption.AllDirectories);
            if (filesToCopy.Length != 1) {
                throw new Exception($"Unexpected number of .hsan files (should only be 1, found {filesToCopy.Length})");
            }
            FileCopyIfNewer(filesToCopy.Single(), Path.Combine(destinationSubdirectory, hsanFilename));
        }

        private static void FileCopyIfNewer(string sourceFilename, string destinationFilename) {
            if (File.Exists(destinationFilename)) {
                if (File.GetLastWriteTimeUtc(sourceFilename) <= File.GetLastWriteTimeUtc(destinationFilename)) {
                    return;
                }
            }

            File.Copy(sourceFilename, destinationFilename, true);
        }

        private static List<string> GetDlcKeys(string sourceDirectory) {
            string sourceSubdirectory = Path.Combine(sourceDirectory, "gamexblocks", "nsongs");
            string[] result = Directory.GetFiles(sourceSubdirectory, "*.xblock", SearchOption.TopDirectoryOnly);
            return result.Select(x => Path.GetFileNameWithoutExtension(x)).ToList();
        }

        private static Dictionary<string, string> GetDlcKeysAndType(List<string> sourceDirectories) {
            Dictionary<string, string> result = new Dictionary<string, string>();

            foreach (string sourceDirectory in sourceDirectories) {
                string sourceSubdirectory = Path.Combine(sourceDirectory, "gamexblocks", "nsongs");
                string[] xblockFilenames = Directory.GetFiles(sourceSubdirectory, "*.xblock", SearchOption.TopDirectoryOnly);
                foreach (string xblockFilename in xblockFilenames) {
                    string dlcKey = Path.GetFileNameWithoutExtension(xblockFilename).Replace("_fcp_dlc", "").Replace("_fcp_disk", "");

                    if (sourceDirectory.Contains("rs1compatibilitydisc_RS2014_Pc")) {
                        // RS2012 base songs
                        result.Add(dlcKey, "2012_base");
                    } else if (sourceDirectory.Contains("rs1compatibilitydlc_RS2014_Pc")) {
                        // RS2012 dlc songs
                        if (!result.ContainsKey(dlcKey)) {
                            result.Add(dlcKey, "2012_dlc");
                        }
                    } else if (sourceDirectory.Contains("songs_psarc_RS2014_Pc")) {
                        // RS2012 dlc or RS2014 base songs
                        if (xblockFilename.Contains("_fcp")) {
                            // RS2012 dlc songs
                            if (!result.ContainsKey(dlcKey)) {
                                result.Add(dlcKey, "2012_dlc");
                            }
                        } else {
                            // RS2014 base songs
                            result.Add(dlcKey, "2014_base");
                        }
                    }
                }
            }

            return result;
        }

        internal static List<string> Split(List<string> sourceDirectories, string destinationDirectory) {
            var result = new HashSet<string>();
            var errorsFound = new StringBuilder();

            Console.WriteLine("Split starting");

            // Get a list of the dlc keys and their type (2012_base, 2012_dlc, 2014_base)
            // Need to do this before cleaning up the filenames in the next step, because we need the _fcp_* suffix to know what the type is
            Dictionary<string, string> dlcKeyTypes = GetDlcKeysAndType(sourceDirectories);

            foreach (string sourceDirectory in sourceDirectories) {
                try {
                    Console.WriteLine($" - Splitting {sourceDirectory}");

                    // Clean up some of the filenames (some have _fcp_dlc or _fcp_disk)
                    // This seems to be because rs1 dlc has the audio data in songs.psarc (_fcp_disk suffix), and the song data in rs1compatibilitydlc_p.psarc (_fcp_dlc suffix)
                    foreach (string filename in Directory.GetFiles(sourceDirectory, "*_fcp_d*", SearchOption.AllDirectories)) {
                        string dn = Path.GetDirectoryName(filename);
                        string fn = Path.GetFileName(filename);
                        File.Move(filename, Path.Combine(dn, fn.Replace("_fcp_dlc", "").Replace("_fcp_disk", "")));
                    }

                    List<string> dlcKeys = GetDlcKeys(sourceDirectory);
                    Console.WriteLine($"   - Found {dlcKeys.Count} DLC Keys");

                    foreach (string dlcKey in dlcKeys) {
                        try {
                            string destinationSubdirectory = Path.Combine(destinationDirectory, dlcKeyTypes[dlcKey], $"{dlcKey}_p");
                            Directory.CreateDirectory(destinationSubdirectory);

                            bool isSongsPsarc = sourceDirectory.Contains("songs_psarc_RS2014_Pc");

                            CopyFiles(sourceDirectory, $@"assets\ui\lyrics\{dlcKey}", destinationSubdirectory, $"lyrics_{dlcKey}.dds", false);
                            CopyAudio(sourceDirectory, destinationSubdirectory, dlcKey);
                            CopyFiles(sourceDirectory, @"flatmodels\rs", destinationSubdirectory, $"*.flat");
                            CopyFiles(sourceDirectory, @"gamexblocks\nsongs", destinationSubdirectory, $"{dlcKey}.xblock");
                            CopyFiles(sourceDirectory, @"gfxassets\album_art", destinationSubdirectory, $"album_{dlcKey}_*.dds", !isSongsPsarc);
                            CopyManifests(sourceDirectory, destinationSubdirectory, dlcKey, !isSongsPsarc);
                            CopyFiles(sourceDirectory, @"songs\arr", destinationSubdirectory, $"{dlcKey}_showlights.xml", !isSongsPsarc);
                            CopyFiles(sourceDirectory, @"songs\bin\generic", destinationSubdirectory, $"{dlcKey}_*.sng", !isSongsPsarc);
                            CopyFiles(sourceDirectory, @"", destinationSubdirectory, "appid.appid", !isSongsPsarc);

                            result.Add(destinationSubdirectory);
                        } catch (Exception ex) {
                            errorsFound.AppendLine($"Error splitting {sourceDirectory} for {dlcKey}: {ex.Message}");
                        }
                    }
                } catch (Exception ex) {
                    errorsFound.AppendLine($"Error splitting {sourceDirectory}: {ex.Message}");
                }
            }

            Console.WriteLine($" - Created {result.Count} split directories:\r\n   - {string.Join("\r\n   - ", result)}");

            if (errorsFound.Length > 0) {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Errors found: {errorsFound}");
                Console.ForegroundColor = ConsoleColor.Gray;
            }

            if (Debugger.IsAttached) {
                Console.WriteLine("Hit ENTER to continue to the next step...");
                Console.ReadLine();
            }

            return result.ToList();
        }
    }
}
