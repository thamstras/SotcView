using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NicoLib
{
    public class MultiStream : Stream
    {
        private readonly List<Stream> theStreams;
        private readonly List<Tuple<long, long>> streamRanges;
        private readonly long totalLength;
        private long position;
        private Stream? currStream;
        private int currStreamIdx;
        private bool disposed = false;

        public MultiStream()
        {
            theStreams = [];
            streamRanges = [];
            totalLength = 0;
            position = 0;
            currStream = null;
            currStreamIdx = 0;
        }

        public MultiStream(IEnumerable<Stream> streams)
            : this()
        {
            long streamStart = 0, streamEnd = 0;
            foreach (Stream stream in streams)
            {
                // set start to last stream end
                streamStart = streamEnd;

                // Add stream to list
                theStreams.Add(stream);

                // Set stream end
                streamEnd = streamStart + stream.Length;

                streamRanges.Add(Tuple.Create(streamStart, streamEnd));
            }
            totalLength = streamEnd;
            if (totalLength > 0)
                currStream = theStreams[0];
        }

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length => totalLength;

        public override long Position { get => position; set => Seek(value, SeekOrigin.Begin); }

        public override void Flush() {}

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (currStream == null)
                return 0;

            int leftToRead = count;
            while (leftToRead > 0)
            {
                // Read what we can from the current stream
                int numBytesRead = currStream.Read(buffer, offset, count);
                leftToRead -= numBytesRead;
                offset += numBytesRead;
                Advance(numBytesRead);

                // If we haven't satisfied the read request, we have exhausted the child stream.
                // Move on to the next stream and loop around to read more data.
                if (leftToRead > 0)
                {
                    if (currStream == null)
                        break;
                }
            }

            return count - leftToRead;
        }

        private void Advance(long bytes)
        {
            (long start, long end) = streamRanges[currStreamIdx];
            position += bytes;
            if (bytes > 0)
            {
                while (position >= end)
                {
                    currStreamIdx++;
                    if (currStreamIdx < theStreams.Count)
                    {
                        currStream = theStreams[currStreamIdx];
                        (_, end) = streamRanges[currStreamIdx];
                    }
                    else
                    {
                        currStream = null;
                        position = totalLength;
                        break;
                    }
                }
            }
            else
            {
                while (position < start)
                {
                    currStreamIdx--;
                    currStream = theStreams[currStreamIdx];
                    (start, _) = streamRanges[currStreamIdx];
                }
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            if (origin == SeekOrigin.Current)
            {
                if (position + offset >= totalLength || position + offset < 0)
                    throw new ArgumentOutOfRangeException(nameof(offset));
                Advance(offset);
                return position;
            }

            if (origin == SeekOrigin.End)
            {
                // TODO: Check this isn't off by one
                offset = totalLength - offset;
                return Seek(offset, SeekOrigin.Begin);
            }

            int newStreamIdx = streamRanges.FindIndex(r => r.Item1 <= offset && r.Item2 > offset);
            if (newStreamIdx == -1)
                throw new ArgumentOutOfRangeException(nameof(offset));
            currStreamIdx = newStreamIdx;
            currStream = theStreams[newStreamIdx];
            position = offset;
            return position;
        }

        public override void SetLength(long value)
        {
            throw new InvalidOperationException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new InvalidOperationException();
        }

        protected override void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    foreach (var stream in theStreams)
                        stream.Dispose();
                }
                disposed = true;
            }
            base.Dispose(disposing);
        }
    }
}
