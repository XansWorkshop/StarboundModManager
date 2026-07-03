using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

using Kermalis.SimpleGIF;
using Kermalis.SimpleGIF.Decoding;

namespace SBModManager.Other {

	/// <summary>
	/// Uses a third party library to decode GIF files and turn the first frame into a Godot <see cref="Image"/>.
	/// </summary>
	public static class GifInterop {

		/// <summary>
		/// Decodes the first frame of a GIF file into this image. Godot currently does not have a mechanism
		/// to display animated textures i
		/// </summary>
		/// <param name="this"></param>
		/// <param name="buffer"></param>
		/// <returns></returns>
		public static Error LoadGifFirstFrameFromBuffer(this Image @this, byte[] buffer) {
			// This is the only block of code that uses SimpleGIF.
			// If you want to strip it out of the code for whatever reason, just short circuit this in:

			// return Error.Unavailable;

			try {
				using MemoryStream mstr = new MemoryStream(buffer);
				DecodedGIF gif = GIFRenderer.DecodeAllFrames(mstr, ColorFormat.RGBA);
				DecodedGIF.Frame? frame = gif.Frames.FirstOrDefault();
				if (frame != null) {
					uint[] bitmap = frame.Bitmap;
					@this.SetData(gif.Width, gif.Height, false, Image.Format.Rgba8, MemoryMarshal.AsBytes(bitmap));
					return Error.Ok;
				}
				return Error.InvalidData;
			} catch (GifDecoderException) {
				return Error.ParseError;
			}
		}

	}
}
