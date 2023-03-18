using Flurl.Http;
using M3u8Parser;
using MediaInfo.DotNetWrapper.Enumerations;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace RaiPlayLive
{
    class Program
    {
        static async Task Main(string[] args)
        {
            string dirette = "https://www.raiplay.it/dirette.json";

            string json = await dirette.GetStringAsync();
            
            JsonDocument document = JsonDocument.Parse(json);
            
            var channels = document.RootElement.GetProperty("contents").EnumerateArray();
            
            foreach (var channel in channels)
            {
                string name = channel.GetProperty("channel").GetString();
                string relinkerUrl = channel.GetProperty("video").GetProperty("content_url").GetString();

                Console.WriteLine(name);

                string manifestUrl = await GetManifestUrl(relinkerUrl);
                string manifestContent = await GetManifest(manifestUrl);
                var streams = ParseManifest(manifestContent);

                // https://stackoverflow.com/a/1616425/1633924
                string hlsDirectory = new Uri(new Uri(manifestUrl), ".").OriginalString;

                await OutputStreamsInfo(name, hlsDirectory, streams);
                
                Console.WriteLine();
                Console.WriteLine("----");
                Console.WriteLine();
            }

            Console.WriteLine("END");
            Console.ReadLine();
        }

        private static async Task<string> DownloadFirstChunk(string channel, string chunklistUrl)
        {
            string hlsChunkList = await chunklistUrl.GetStringAsync();
            var lines = hlsChunkList.Split("\n");

            string chunkFileName = null;

            foreach (var line in lines)
            {
                // encrypted
                if (line.StartsWith("#EXT-X-KEY"))
                {
                    return null;
                }

                // take the .ts file name
                if (line.Contains(".ts"))
                {
                    chunkFileName = line;
                    break;
                }
            }

            if (chunkFileName == null)
            {
                return null;
            }

            string chunkUrl;

            if (chunkFileName.StartsWith("http"))
            {
                chunkUrl = chunkFileName;
            }
            else
            {
                string hlsDirectory = new Uri(new Uri(chunklistUrl), ".").OriginalString;
                chunkUrl = Path.Combine(hlsDirectory, chunkFileName);
            }

            string currentDir = Directory.GetCurrentDirectory();
            await chunkUrl.DownloadFileAsync(currentDir, $"{channel}.ts");

            return Path.Combine(currentDir, $"{channel}.ts");
        }

        private static async Task OutputStreamsInfo(string channel, string hlsDirectory, List<M3u8Media> streams)
        {
            foreach (var stream in streams)
            {
                double bandwidth = stream.Bandwidth / 1024.0 / 1024.0;
                Console.Write($"{stream.Resolution} | {bandwidth:0.00} Mbps | {stream.Codecs}");

                string chunklistUrl;
                if (stream.Url.StartsWith("http"))
                {
                    chunklistUrl = stream.Url;
                }
                else
                {
                    chunklistUrl = Path.Combine(hlsDirectory, stream.Url);
                }

                string localSegmentFileName = await DownloadFirstChunk(channel, chunklistUrl);

                if (localSegmentFileName != null)
                {
                    using (var info = new MediaInfo.DotNetWrapper.MediaInfo())
                    {
                        info.Open(localSegmentFileName);
                        //Console.WriteLine(info.Option("Info_Parameters"));

                        // includes level. ex: Main@L3.1
                        string profile = info.Get(StreamKind.Video, 0, "Format_Profile");
                        string refFrames = info.Get(StreamKind.Video, 0, "Format_Settings_RefFrames");
                        string gop = info.Get(StreamKind.Video, 0, "Format_Settings_GOP");

                        if (string.IsNullOrEmpty(gop))
                        {
                            gop = "?";
                        }

                        double duration = double.Parse(info.Get(StreamKind.Video, 0, "Duration")) / 1000.0;
                        string bitrate = info.Get(StreamKind.Video, 0, "BitRate/String").Replace(" ", "");

                        if (string.IsNullOrEmpty(bitrate))
                        {
                            bitrate = info.Get(StreamKind.Video, 0, "BitRate_Nominal/String").Replace(" ", "");
                        }

                        string overall = info.Get(StreamKind.General, 0, "OverallBitRate/String").Replace(" ", "");

                        string mode = info.Get(StreamKind.Video, 0, "BitRate_Mode");
                        string fps = info.Get(StreamKind.Video, 0, "FrameRate");

                        Console.WriteLine($" | {profile} | ref={refFrames} | GOP {gop} | segment={duration:0.0} | nominal={bitrate:0.###} {mode} | total={overall} | fps={fps}");
                    }
                }
                else
                {
                    Console.WriteLine();
                }
            }
        }

        private static List<M3u8Media> ParseManifest(string manifestContent)
        {
            IM3u8Parser parser = new M3u8Parser.M3u8Parser();

            parser.Load(manifestContent);
            var streams = parser.Parse();

            return streams;
        }

        private static Task<string> GetManifest(string manifestUrl)
        {
            return manifestUrl.GetStringAsync();
        }

        private static async Task<string> GetManifestUrl(string relinkerUrl)
        {
            string fullUrl = relinkerUrl + "&output=64";
            string xmlContent = await fullUrl.GetStringAsync();

            var match = Regex.Match(xmlContent, "<!\\[CDATA\\[(.+?)\\]\\]");

            string masterPlaylist = match.Groups[1].Value;

            return masterPlaylist;
        }
    }
}
