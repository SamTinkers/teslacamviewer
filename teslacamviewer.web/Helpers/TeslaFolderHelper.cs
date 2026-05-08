using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using teslacamviewer.data.Enums;
using teslacamviewer.web.Models;

namespace teslacamviewer.web.Helpers
{
    public static class TeslaFolderHelper
    {
        public static string TeslaFolderPathParser(string fullPath) {
            var x = Path.DirectorySeparatorChar;
            var strings = fullPath.Split(Path.DirectorySeparatorChar);
            return strings[strings.Length - 1];
        }
        
        public static bool IsValidFolder(string directory) {
            var directoryName = directory.Split(Path.DirectorySeparatorChar).Last();
            var regex = new System.Text.RegularExpressions.Regex(@"\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2}");
            return regex.IsMatch(directoryName);
        }

        public static bool ContainsThumbnail(List<string> files, string directory) {
            return files.Contains(Path.Combine(directory, "thumb.png"));
        }

        public static string TeslaClipPathParser(string fullPath) {
            var strings = fullPath.Split(Path.DirectorySeparatorChar);
            return strings[strings.Length - 1];
        }

        public static DateTime TeslaClipDateTimeParser(string fullPath) {
            var strings = fullPath.Split("_");
            var dateString = strings[0];
            var timeStrings = strings[1].Split("-"); 
            return Convert.ToDateTime($"{dateString}T{timeStrings[0]}:{timeStrings[1]}:{timeStrings[2]}");
        }

        public static SideEnum TeslaClipSideParser(string fullPath) {
            // Tesla clips are named like:
            //   2026-05-08_10-36-22-front.mp4
            //   2026-05-08_10-36-22-left_repeater.mp4
            //   2026-05-08_10-36-22-right_pillar.mp4
            // Original parser took the suffix-after-last-dash and dropped everything
            // after the first underscore — silently mangling left_repeater -> "repeater"
            // (which doesn't match SideEnum) and so on for HW4 cars.
            // We instead match the known camera-name suffix explicitly.
            var fileName = System.IO.Path.GetFileNameWithoutExtension(fullPath);
            var match = System.Text.RegularExpressions.Regex.Match(
                fileName,
                @"-(front|back|left_repeater|right_repeater|left_pillar|right_pillar|left|right)$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!match.Success) {
                throw new ArgumentException($"Cannot parse Tesla camera side from filename: {fileName}");
            }
            // "left_repeater" -> "leftrepeater" -> SideEnum.LeftRepeater (case-insensitive)
            var raw = match.Groups[1].Value.Replace("_", "");
            return Enum.Parse<SideEnum>(raw, true);
        }

        internal static bool ContainsTeslaEvent(string directory)
        {
            return Directory.GetFiles(directory).Contains(Path.Combine(directory, "event.json"));
        }
    }
}