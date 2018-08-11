using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace M3u8Parser
{
    public interface IM3u8Parser
    {
        List<M3u8Media> Parse();

        void Load(string text);
    }

    public class M3u8Media
    {
        public string Type { get; set; }
        public string Name { get; set; }
        public long Bandwidth { get; set; }
        public Resolution Resolution { get; set; }
        public string Codecs { get; set; }
        public string Video { get; set; }
        public string Url { get; set; }
    }

    public class Resolution : IComparable
    {
        public int Width { get; set; }
        public int Height { get; set; }

        public int CompareTo(object obj)
        {
            Resolution other = (Resolution)obj;

            if (this.Height > other.Height)
            {
                return 1;
            }
            else if (this.Height < other.Height)
            {
                return -1;
            }
            else
            {
                return 0;
            }
        }

        public override string ToString()
        {
            return $"{Width}x{Height}";
        }
    }
}