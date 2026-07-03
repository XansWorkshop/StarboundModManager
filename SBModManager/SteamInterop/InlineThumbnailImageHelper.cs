using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using SBModManager.Other;

using HttpClient = System.Net.Http.HttpClient;
using Semaphore = System.Threading.Semaphore;

namespace SBModManager.SteamInterop {

	/// <summary>
	/// Inside of Steam Workshop descriptions, it's possible to use image files inline.
	/// This uses the [img] tag, which is the same as Godot. However, Godot expects its images to be a <c>res://</c>
	/// path, not a URL. This wreaks all sorts of havoc on the display and raises a comical amount of errors.
	/// <para/>
	/// This class exists to provide a stopgap for that issue by caching all images and replacing them on the fly.
	/// </summary>
	public static partial class InlineThumbnailImageHelper {

		/// <summary>
		/// Binds a hash to an asynchronous task which represents the operation of downloading the texture.
		/// </summary>
		private static readonly ConcurrentDictionary<string, Task<Texture2D>> IMAGE_ACQUISITIONS = [];

		/// <summary>
		/// Used to prevent a comical amount of downloads from occurring at once. Prevents you from getting blocked by Steam.
		/// </summary>
		private static readonly Semaphore RATE_LIMITER = new Semaphore(8, 8);

		/// <summary>
		/// Replace all image tags with a valid Godot resource on the fly. This may have a serious performance impact.
		/// </summary>
		/// <param name="bbcode"></param>
		/// <returns></returns>
		public static string ReplaceImages(string bbcode) {
			Regex regex = ImgTagRegex();
			return regex.Replace(bbcode, delegate (Match match) {
				if (!match.Success) return match.Value;

				string url = match.Groups[1].Value;
				string res = EnqueueImageDownloadIfNeeded(url);
				return res;
			});
		}

		/// <summary>
		/// Checks the <see cref="IMAGE_ACQUISITIONS"/> and, if a url is new, enqueues a download for that image.
		/// </summary>
		/// <param name="url"></param>
		private static string EnqueueImageDownloadIfNeeded(string url) {
			string md5 = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(url)));
			if (!IMAGE_ACQUISITIONS.TryGetValue(md5, out Task<Texture2D>? existingTask)) {
				IMAGE_ACQUISITIONS[md5] = DownloadImageIntoTexture2DImpl(url, md5);
			}
			return $"res://workshop_image_cache/{md5}";
		}

		/// <summary>
		/// The actual asynchronous implementation of a download.
		/// </summary>
		/// <param name="url"></param>
		/// <param name="md5"></param>
		/// <returns></returns>
		private static async Task<Texture2D> DownloadImageIntoTexture2DImpl(string url, string md5) {
			Image image = Image.CreateEmpty(1, 1, false, Image.Format.Rgba8);
			image.SetPixel(0, 0, Colors.Magenta);

			ImageTexture result = ImageTexture.CreateFromImage(image);
			result.TakeOverPath($"res://workshop_image_cache/{md5}");

			string imgCache = Directories.GetSteamImageCacheDirectory();
			Directory.CreateDirectory(imgCache);

			await Task.Yield();
			RATE_LIMITER.WaitOne();
			try {
				byte[]? buffer = null;
				try {
					buffer = File.ReadAllBytes(Path2.Combine(imgCache, $"{md5}.png"));
				} catch (FileNotFoundException) {
				} catch (DirectoryNotFoundException) {
				}

				if (buffer != null) {
					if (image.LoadPngFromBuffer(buffer) == Error.Ok) {
						result.SetImage(image);
					}
					return result;
				}

				using HttpClient client = new HttpClient();
				Stream? download = null;
				int retries = 10;
				while (retries-- > 0) {
					try {
						download = await client.GetStreamAsync(url, CancellationToken.None).ConfigureAwait(false);
					} catch (HttpRequestException request) {
						if (request.StatusCode == HttpStatusCode.TooManyRequests) {
							int rng = Random.Shared.Next();
							rng &= 0xFFF;
							await Task.Delay(5000 + rng).ConfigureAwait(false);
						} else {
							throw;
						}
					}
				}
				if (download == null) {
					return result;
				}

				// TODO: Security? MemoryStream will fail out after 2GB.
				using MemoryStream imageBuffer = new MemoryStream();
				download.CopyTo(imageBuffer);
				buffer = imageBuffer.ToArray();
				imageBuffer.Dispose();

				Error error = image.LoadPngFromBuffer(buffer);
				if (error != Error.Ok) {
					// Try it as a jpg?
					error = image.LoadJpgFromBuffer(buffer);

					if (error != Error.Ok) {
						// Nope :(
						image.SetData(1, 1, false, Image.Format.Rgba8, BitConverter.GetBytes(0xFF00FFFF));
					}
				}

				result.SetImage(image);
				image.SavePng(Path2.Combine(imgCache, $"{md5}.png"));
				await Task.Delay(1000).ConfigureAwait(false);
				return result;
			} catch {
				result.Dispose();
				image.Dispose();
				return new PlaceholderTexture2D();
			} finally {
				RATE_LIMITER.Release();
			}
		}


		[GeneratedRegex(@"\[img(?:\=[^\]]+)?\]([^\[\]]+)\[\/img\]", RegexOptions.IgnoreCase)]
		private static partial Regex ImgTagRegex();
	}
}
