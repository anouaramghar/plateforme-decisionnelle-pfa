using System.Text;

namespace PlateformePFA.API.Services.Copilot
{
    /// <summary>
    /// Stateful incremental parser for Server-Sent Events. Fed raw UTF-8 byte
    /// chunks as they arrive from agent-service; yields complete events.
    ///
    /// Used by CopilotController to "tee" the SSE stream — bytes are written
    /// to the HTTP response unchanged AND parsed locally so we can capture
    /// the assistant's text from `token` events and persist it on `done`.
    ///
    /// Assumptions matching what agent-service emits (see agent-service/main.py
    /// _sse_format and schemas.py): each event is `event: NAME\ndata: JSON\n\n`,
    /// data is a single line of JSON, no comments, no retry directives.
    /// </summary>
    public sealed class SseParser
    {
        // UTF-8 decoder retains state across calls — critical because chunk
        // boundaries can split multi-byte chars (common with accented French).
        private readonly Decoder _utf8 = Encoding.UTF8.GetDecoder();
        private readonly StringBuilder _buffer = new();

        public readonly struct SseEvent
        {
            public string Name { get; init; }
            public string Data { get; init; }
        }

        /// <summary>
        /// Feed a chunk of bytes; yield any complete events that became
        /// available. A chunk may complete zero, one, or several events.
        /// </summary>
        public IEnumerable<SseEvent> Feed(ReadOnlySpan<byte> chunk)
        {
            // Decode (stateful — handles split UTF-8 sequences).
            var charBuf = new char[_utf8.GetCharCount(chunk, flush: false)];
            int charCount = _utf8.GetChars(chunk, charBuf, flush: false);
            _buffer.Append(charBuf, 0, charCount);

            var results = new List<SseEvent>();
            while (true)
            {
                var current = _buffer.ToString();
                int boundary = current.IndexOf("\n\n", StringComparison.Ordinal);
                if (boundary < 0) break;

                var block = current[..boundary];
                _buffer.Remove(0, boundary + 2);

                string? name = null;
                string? data = null;
                foreach (var line in block.Split('\n'))
                {
                    if (line.StartsWith("event: ", StringComparison.Ordinal))
                        name = line[7..];
                    else if (line.StartsWith("data: ", StringComparison.Ordinal))
                        data = line[6..];
                }
                if (name is not null && data is not null)
                    results.Add(new SseEvent { Name = name, Data = data });
            }
            return results;
        }
    }
}
