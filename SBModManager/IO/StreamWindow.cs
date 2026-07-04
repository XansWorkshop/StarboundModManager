using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace SBModManager.IO {

	/// <summary>
	/// A window around a stream. Note that this is not a copy of the underlying stream.
	/// That is, the position of the underlying stream affects the position of this stream, and so it is possible
	/// for <see cref="Position"/> to have out of range values. Use <see cref="IsOutOfRange"/> to check this.
	/// </summary>
	public sealed class StreamWindow : Stream {
		private readonly Stream _baseStream;
		private readonly long _length;
		private readonly long _baseStreamOffset;
		private readonly bool _keepOpen;
		private bool _disposed;

		[StackTraceHidden]
		private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

		public override long Length {
			get {
				ThrowIfDisposed();
				return _length;
			}
		}
		public override bool CanRead {
			get {
				ThrowIfDisposed();
				return _baseStream.CanRead;
			}
		}
		public override bool CanWrite {
			get {
				ThrowIfDisposed();
				return _baseStream.CanWrite;
			}
		}
		public override bool CanSeek {
			get {
				ThrowIfDisposed();
				return _baseStream.CanSeek;
			}
		}

		/// <summary>
		/// If <see langword="true"/>, the position of the base stream is out of range of the limits of this instance.
		/// </summary>
		/// <exception cref="ObjectDisposedException"></exception>
		public bool IsOutOfRange {
			get {
				ThrowIfDisposed();
				return _baseStream.Position < _baseStreamOffset || (_baseStream.Position > _baseStreamOffset + _length);
			}
		}

		/// <summary>
		/// The position in this window. Note that this may be less than zero or greater than <see cref="Length"/> for <see cref="StreamWindow"/>s.
		/// </summary>
		public override long Position {
			get {
				ThrowIfDisposed();
				return _baseStream.Position - _baseStreamOffset;
			}
			set {
				ThrowIfDisposed();
				Seek(value, SeekOrigin.Begin);
			}
		}

		/// <summary>
		/// Creates a <see cref="StreamWindow"/> using the current position in the <paramref name="stream"/> as its starting point.
		/// </summary>
		/// <param name="stream"></param>
		/// <param name="length"></param>
		/// <returns></returns>
		public static StreamWindow FromCurrentPositionIn(Stream stream, long length, bool keepOpen = true) => new StreamWindow(stream, stream.Position, length, keepOpen);

		/// <summary>
		/// Create a new <see cref="StreamWindow"/> around the provided <paramref name="baseStream"/> which begins at the provided
		/// <paramref name="offset"/> in the <paramref name="baseStream"/>, and includes up to the following <paramref name="length"/> bytes.
		/// </summary>
		/// <param name="baseStream">The <see cref="Stream"/> this provides a window into.</param>
		/// <param name="offset">The starting position in the <paramref name="baseStream"/> that this window exists at.</param>
		/// <param name="length">The amount of bytes available in this window.</param>
		/// <param name="keepOpen">If true, keep the <paramref name="baseStream"/> open even if this is closed.</param>
		public StreamWindow(Stream baseStream, long offset, long length, bool keepOpen = true) {
			ArgumentNullException.ThrowIfNull(baseStream);
			ArgumentOutOfRangeException.ThrowIfNegative(offset);
			ArgumentOutOfRangeException.ThrowIfNegative(length);
			_baseStream = baseStream;
			_baseStreamOffset = offset;
			_length = length;
			_keepOpen = keepOpen;
		}

		/// <inheritdoc/>
		public override int Read(byte[] buffer, int offset, int count) {
			ThrowIfDisposed();
			long remainingBytes = _length - (_baseStream.Position - _baseStreamOffset);
			if (remainingBytes < 0) return 0;
			if (remainingBytes > _length) remainingBytes = _length;

			if (count > remainingBytes) count = (int)remainingBytes;
			return _baseStream.Read(buffer, offset, count);
		}

		/// <inheritdoc/>
		public override void Write(byte[] buffer, int offset, int count) {
			ThrowIfDisposed();
			if (count == 0) return;

			Span<byte> window = buffer.AsSpan(offset, count);
			long remainingBytes = _length - (_baseStream.Position - _baseStreamOffset);
			if (remainingBytes < 0) return;
			if (remainingBytes > _length) remainingBytes = _length;
			if (remainingBytes > count) remainingBytes = count;
			int toWrite = int.Min(window.Length, (int)remainingBytes);

			_baseStream.Write(window[..toWrite]);
		}

		/// <inheritdoc/>
		public override long Seek(long offset, SeekOrigin origin) {
			ThrowIfDisposed();
			/*
			If offset is negative, the new position is required to precede the position specified by origin by the number 
			of bytes specified by offset. If offset is zero (0), the new position is required to be the position specified 
			by origin. If offset is positive, the new position is required to follow the position specified by origin by 
			the number of bytes specified by offset.

			Classes derived from Stream that support seeking must override this method to provide the functionality described above.

			Seeking to any location beyond the length of the stream is supported.
			*/

			// (How convenient!)
			if (origin == SeekOrigin.Begin) {
				return _baseStream.Seek(_baseStreamOffset + offset, SeekOrigin.Begin) - _baseStreamOffset;
			} else if (origin == SeekOrigin.End) {
				return _baseStream.Seek(_baseStreamOffset + _length + offset, SeekOrigin.Begin) - _baseStreamOffset;
			} else if (origin == SeekOrigin.Current) {
				return _baseStream.Seek(offset, SeekOrigin.Current) - _baseStreamOffset;
			} else {
				throw new ArgumentException();
			}
		}

		/// <inheritdoc/>
		public override void SetLength(long value) {
			ThrowIfDisposed();
			throw new NotSupportedException();
		}

		/// <inheritdoc/>
		protected override void Dispose(bool disposing) {
			if (_keepOpen) return;
			if (disposing) {
				_disposed = true;
				_baseStream.Dispose();
			}
		}

		public override void Flush() {
			_baseStream.Flush();
		}
	}
}
