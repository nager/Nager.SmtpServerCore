using Nager.SmtpServerCore.Protocol;
using Nager.SmtpServerCore.Text;
using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Nager.SmtpServerCore.IO
{
    internal static class PipeReaderExtensions
    {
        // ReSharper disable once InconsistentNaming
        static readonly byte[] CRLF = { 13, 10 };
        static readonly byte[] DotBlock = { 13, 10, 46, 13, 10 };
        static readonly byte[] DotBlockStuffing = { 13, 10, 46, 46 };

        /// <summary>
        /// Read from the reader until the sequence is found.
        /// </summary>
        /// <param name="reader">The reader to read from.</param>
        /// <param name="sequence">The sequence to find to terminate the read operation.</param>
        /// <param name="func">The callback to execute to process the buffer.</param>
        /// <param name="maxMessageSizeOptions">Handling of MaxMessageSize.</param>
        /// <param name="maxLength">The maximum number of bytes that may precede the sequence, 0 for no limit.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The value that was read from the buffer.</returns>
        static async ValueTask ReadUntilAsync(PipeReader reader, byte[] sequence, Func<ReadOnlySequence<byte>, Task> func, IMaxMessageSizeOptions maxMessageSizeOptions, long maxLength, CancellationToken cancellationToken)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            var read = await reader.ReadAsync(cancellationToken);
            var head = read.Buffer.Start;

            while (read.IsCanceled == false && read.IsCompleted == false && read.Buffer.IsEmpty == false)
            {
                if (maxMessageSizeOptions.Handling == MaxMessageSizeHandling.Strict &&
                    maxMessageSizeOptions.Length > 0 &&
                    read.Buffer.Length > maxMessageSizeOptions.Length)
                {
                    throw new SmtpResponseException(SmtpResponse.MaxMessageSizeExceeded, true);
                }

                if (read.Buffer.TryFind(sequence, ref head, out var tail))
                {
                    if (maxLength > 0 && read.Buffer.Slice(read.Buffer.Start, head).Length > maxLength)
                    {
                        throw new SmtpResponseException(SmtpResponse.LineTooLong, true);
                    }

                    try
                    {
                        await func(read.Buffer.Slice(read.Buffer.Start, head));
                    }
                    finally
                    {
                        reader.AdvanceTo(tail);
                    }

                    return;
                }

                if (maxLength > 0 && read.Buffer.Length > maxLength)
                {
                    // the sequence has not been found yet and the buffered data already exceeds the limit,
                    // the data is not consumed any further otherwise a client could make the server buffer an unbounded amount of data
                    throw new SmtpResponseException(SmtpResponse.LineTooLong, true);
                }

                reader.AdvanceTo(read.Buffer.Start, read.Buffer.End);

                read = await reader.ReadAsync(cancellationToken);
            }
        }

        /// <summary>
        /// Reads a line from the reader.
        /// </summary>
        /// <param name="reader">The reader to read from.</param>
        /// <param name="func">The action to process the buffer.</param>
        /// <param name="maxMessageSizeOptions">Handling of MaxMessageSize.</param>
        /// <param name="maxLineLength">The maximum number of bytes allowed in a line (excluding the CRLF), 0 for no limit.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task that can be used to wait on the operation on complete.</returns>
        internal static ValueTask ReadLineAsync(this PipeReader reader, Func<ReadOnlySequence<byte>, Task> func, IMaxMessageSizeOptions maxMessageSizeOptions, int maxLineLength, CancellationToken cancellationToken = default)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            return ReadUntilAsync(reader, CRLF, func, maxMessageSizeOptions, maxLineLength, cancellationToken);
        }

        /// <summary>
        /// Reads a line from the reader.
        /// </summary>
        /// <param name="reader">The reader to read from.</param>
        /// <param name="maxMessageSizeOptions">Handling of MaxMessageSize.</param>
        /// <param name="maxLineLength">The maximum number of bytes allowed in a line (excluding the CRLF), 0 for no limit.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task that can be used to wait on the operation on complete.</returns>
        internal static ValueTask<string> ReadLineAsync(this PipeReader reader, IMaxMessageSizeOptions maxMessageSizeOptions, int maxLineLength, CancellationToken cancellationToken = default)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            return reader.ReadLineAsync(Encoding.ASCII, maxMessageSizeOptions, maxLineLength, cancellationToken);
        }

        /// <summary>
        /// Reads a line from the reader.
        /// </summary>
        /// <param name="reader">The reader to read from.</param>
        /// <param name="encoding">The encoding to use when converting the input.</param>
        /// <param name="maxMessageSizeOptions"> Handling of MaxMessageSize</param>
        /// <param name="maxLineLength">The maximum number of bytes allowed in a line (excluding the CRLF), 0 for no limit.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task that can be used to wait on the operation on complete.</returns>
        internal static async ValueTask<string> ReadLineAsync(this PipeReader reader, Encoding encoding, IMaxMessageSizeOptions maxMessageSizeOptions, int maxLineLength, CancellationToken cancellationToken = default)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            var text = string.Empty;

            await reader.ReadLineAsync(
                buffer =>
                {
                    text = StringUtil.Create(buffer, encoding);

                    return Task.CompletedTask;
                },
                maxMessageSizeOptions,
                maxLineLength,
                cancellationToken);

            return text;
        }

        /// <summary>
        /// Reads a dot terminated block (the message content) from the reader.
        /// </summary>
        /// <param name="reader">The reader to read from.</param>
        /// <param name="func">The action to process the buffer.</param>
        /// <param name="maxMessageSizeOptions">Handling of MaxMessageSize, this is the only limit that applies to the block.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The value that was read from the buffer.</returns>
        internal static async ValueTask ReadDotBlockAsync(this PipeReader reader, Func<ReadOnlySequence<byte>, Task> func, IMaxMessageSizeOptions maxMessageSizeOptions, CancellationToken cancellationToken = default)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            await ReadUntilAsync(
                reader,
                DotBlock,
                buffer =>
                {
                    buffer = Unstuff(buffer);

                    return func(buffer);
                },
                maxMessageSizeOptions,
                0,
                cancellationToken);

            static ReadOnlySequence<byte> Unstuff(ReadOnlySequence<byte> buffer)
            {
                var head = buffer.GetPosition(0);
                var start = head;

                var segments = new ByteArraySegmentList();

                while (buffer.TryFind(DotBlockStuffing, ref head, out var tail))
                {
                    var slice = buffer.Slice(start, buffer.GetPosition(3, head));

                    segments.Append(ref slice);

                    start = tail;
                    head = tail;
                }

                var remaining = buffer.Slice(start);
                segments.Append(ref remaining);

                return segments.Build();
            }
        }
    }
}
