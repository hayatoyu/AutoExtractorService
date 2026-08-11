using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoExtractorService
{
    public class ExtractorOptions
    {
        public const string Position = "ExtractorSettings";

        public string WatchFolder { get; set; } = string.Empty;
        public string SevenZipPath { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public List<string> VideoExtensions { get; set; } = new List<string>();
        public List<string> SubtitleExtensions { get; set; } = new List<string>();
        public List<string> ArchiveExtensions { get; set; } = new List<string>();
    }
}
