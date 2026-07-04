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

using SBModManager.GUI;
using SBModManager.Other;

using static Godot.HttpRequest;

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

		private static readonly ConcurrentDictionary<string, Task<Texture2D>> CONCURRENT_IMAGE_LOADS = [];

		/// <summary>
		/// Used to prevent a comical amount of downloads from occurring at once. Prevents you from getting blocked by Steam.
		/// </summary>
		private static readonly Semaphore RATE_LIMITER = new Semaphore(16, 16);

		/// <summary>
		/// Replace all image tags with a valid Godot resource on the fly. This may have a serious performance impact.
		/// </summary>
		/// <param name="bbcode"></param>
		/// <returns></returns>
		public static string ReplaceImages(string bbcode, List<string> hashesForImages) {
			Regex regex = FormatTools.IMGBBCodeResolver();
			return regex.Replace(bbcode, delegate (Match match) {
				if (!match.Success) return match.Value;

				string url = match.Groups[1].Value;
				if (!url.StartsWith("res://")) {
					string res = EnqueueImageDownloadIfNeeded(url, hashesForImages);
					return $"[img]{res}[/img]";
				}
				return $"[img]{url}[/img]";
			});
		}

		/// <summary>
		/// Checks the <see cref="IMAGE_ACQUISITIONS"/> and, if a url is new, enqueues a download for that image.
		/// </summary>
		/// <param name="url"></param>
		private static string EnqueueImageDownloadIfNeeded(string url, List<string> hashesForImages) {
			string md5 = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(url)));
			hashesForImages.Add(md5);

			string path = $"res://workshop_image_cache/{md5}";
			if (!ResourceLoader.Exists(path)) {
				if (!CONCURRENT_IMAGE_LOADS.ContainsKey(md5)) {
					CONCURRENT_IMAGE_LOADS[md5] = DownloadImageIntoTexture2DImpl(url, md5);
				}
			}
			return path;
		}

		/// <summary>
		/// The actual asynchronous implementation of a download.
		/// </summary>
		/// <param name="url"></param>
		/// <param name="md5"></param>
		/// <returns></returns>
		private static async Task<Texture2D> DownloadImageIntoTexture2DImpl(string url, string md5) {

			// FIXME:
			// The current implementation bogs down the task scheduler and grinds most of the downloads to a halt.
			// In The Conservatory (my game, where most of the threading code from here comes from) this is solved
			// with LimitedConcurrencyTaskScheduler.

			string path = $"res://workshop_image_cache/{md5}";

			Image actualImage = Image.CreateEmpty(1, 1, false, Image.Format.Rgba8);
			actualImage.SetPixel(0, 0, Colors.Magenta);

			ImageTexture result = ImageTexture.CreateFromImage(Assets.PlaceholderWorkshopImageLoading);
			result.TakeOverPath(path);

			string imgCache = Directories.GetSteamImageCacheDirectory();
			Directory.CreateDirectory(imgCache);

			// This *should* fix it getting slowed down?
			while (!RATE_LIMITER.WaitOne(0)) {
				await Task.Yield();
			}
			try {
				byte[]? buffer = null;
				try {
					buffer = File.ReadAllBytes(Path2.Combine(imgCache, $"{md5}.png"));
				} catch (FileNotFoundException) {
				} catch (DirectoryNotFoundException) {
				}

				if (buffer != null) {
					if (actualImage.LoadPngFromBuffer(buffer) == Error.Ok) {
						result.SetImage(actualImage);
					}
					return result;
				}

				using HttpClient client = new HttpClient();
				Stream? download = null;
				int retries = 10;
				while (retries-- > 0) {
					try {
						download = await client.GetStreamAsync(url, CancellationToken.None).ConfigureAwait(false);
						break;
					} catch (HttpRequestException request) {
						if (request.StatusCode == HttpStatusCode.TooManyRequests) {
							int rng = Random.Shared.Next();
							rng &= 0xFFF;
							await Task.Delay(5000 + rng).ConfigureAwait(false);
						} else if (request.StatusCode == HttpStatusCode.NotFound) {
							download = null;
							break;
						} else {
							throw;
						}
					}
				}
				if (download == null) {
					result.SetImage(Assets.PlaceholderWorkshopImageError);
					Assets.PlaceholderWorkshopImageError.SavePng(Path2.Combine(imgCache, $"{md5}.png"));
					return result;
				}

				// TODO: Security? MemoryStream will fail out after 2GB.
				using MemoryStream imageBuffer = new MemoryStream();
				download.CopyTo(imageBuffer);
				buffer = imageBuffer.ToArray();
				imageBuffer.Dispose();

				Error error = actualImage.LoadPngFromBuffer(buffer);
				if (error != Error.Ok) {
					// Try it as a jpg?
					error = actualImage.LoadJpgFromBuffer(buffer);

					if (error != Error.Ok) {
						// Nope :(
						result.SetImage(Assets.PlaceholderWorkshopImageError);
						Assets.PlaceholderWorkshopImageError.SavePng(Path2.Combine(imgCache, $"{md5}.png"));
						return result;
					}
				}

				result.SetImage(actualImage);
				actualImage.SavePng(Path2.Combine(imgCache, $"{md5}.png"));
				await Task.Delay(100).ConfigureAwait(false);
				return result;
			} catch {
				result.Dispose();
				actualImage.Dispose();
				Assets.PlaceholderWorkshopImageError.SavePng(Path2.Combine(imgCache, $"{md5}.png"));
				return new PlaceholderTexture2D();
			} finally {
				RATE_LIMITER.Release();
			}
		}

	}
}
