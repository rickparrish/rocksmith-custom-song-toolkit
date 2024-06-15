using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace songpacksplitter {
    internal class Program {
        static void Main() {
            List<string> sourceFilenames = new List<string>() {
                // Do songs.psarc first, so then rs1compatibilitydlc_p.psarc can overwrite the existing files with new files from it
                // I don't have any rs1 dlc, so I can't actually test they work, but DLC Builder seems to load them OK
                @"C:\Program Files (x86)\Steam\steamapps\common\Rocksmith2014\songs.psarc",
                @"C:\Program Files (x86)\Steam\steamapps\common\Rocksmith2014\dlc\rs1compatibilitydisc_p.psarc",
                @"C:\Program Files (x86)\Steam\steamapps\common\Rocksmith2014\dlc\rs1compatibilitydlc_p.psarc",
                //@"C:\Temp\songpacksplitter\songs.psarc",
                //@"C:\Temp\songpacksplitter\rs1compatibilitydisc_p.psarc",
                //@"C:\Temp\songpacksplitter\rs1compatibilitydlc_p.psarc",
            };
            string unpackDirectory = @"C:\Temp\songpacksplitter\unpack";
            string splitDirectory = @"C:\Temp\songpacksplitter\split";
            string psarcDirectory = @"C:\Temp\songpacksplitter\psarc";

            Console.ForegroundColor = ConsoleColor.Gray;

            List<string> unpackedDirectories = UnpackHelper.Unpack(sourceFilenames, unpackDirectory);
            List<string> splitDirectories = SplitHelper.Split(unpackedDirectories, splitDirectory);
            PackHelper.Pack(splitDirectories, psarcDirectory);

            if (Debugger.IsAttached) {
                Console.WriteLine("Done, hit ENTER to quit");
                Console.ReadLine();
            }
        }
    }
}
