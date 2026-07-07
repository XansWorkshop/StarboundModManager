using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Design;
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

		// private static readonly ConcurrentDictionary<string, Task<Texture2D?>> CONCURRENT_IMAGE_LOADS = [];
		private static readonly Dictionary<string, ImageTexture> TEXTURE_SHARE = [];
		private static CancellationTokenSource? _currentImageCancellations;

		private static readonly Lock TEXTURE_SHARE_LOCK = new Lock();

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
		/// Prepares the system to load images by setting up a cancellation token and ensuring the correct state.
		/// </summary>
		public static void PrepareForImageLoading() {
			_currentImageCancellations ??= new CancellationTokenSource();
		}

		/// <summary>
		/// Loads images from disk in parallel ahead of time to prevent missing resource errors.
		/// </summary>
		/// <param name="hashes"></param>
		public static void PreloadImagesFromDisk(IEnumerable<string> hashes) {
			string imgCache = Directories.GetSteamImageCacheDirectory();
			Directory.CreateDirectory(imgCache);

			lock (TEXTURE_SHARE_LOCK) {
				foreach (string md5 in hashes) {
					string path = $"res://workshop_image_cache/{md5}";
					try {
						string cachePath = Path2.Combine(imgCache, $"{md5}.png");
						if (File.Exists(cachePath) && TEXTURE_SHARE.TryGetValue(md5, out ImageTexture? imageTexture)) {
							// ^ Test this anyway. Exception handling is slow and this needs to be fast.
							byte[] buffer = File.ReadAllBytes(cachePath);
							using Image image = Image.CreateEmpty(1, 1, false, Image.Format.Rgba8);
							image.LoadPngFromBuffer(buffer);
							imageTexture = ImageTexture.CreateFromImage(image);
							imageTexture.TakeOverPath(path);
							TEXTURE_SHARE[md5] = imageTexture;

							// RenderingServer.TextureReplace(imageTexture.GetRid(), RenderingServer.Texture2DCreate(image));
						}
					} catch (FileNotFoundException) {
					} catch (DirectoryNotFoundException) {
					}
				}
			}
		}

		/// <summary>
		/// Frees every allocated <see cref="Texture2D"/> for workshop descriptions, in order to save memory.
		/// <para/>
		/// <strong>Thread safety warning:</strong> This just generously assumes no loading tasks are active right now.
		/// </summary>
		/// <returns></returns>
		public static void PurgeImages() {
			if (_currentImageCancellations == null) throw new InvalidOperationException($"This method can only be called after {nameof(PrepareForImageLoading)}.");
			_currentImageCancellations.Cancel();
			_currentImageCancellations = null;
			lock (TEXTURE_SHARE_LOCK) {
				foreach (KeyValuePair<string, ImageTexture> shared in TEXTURE_SHARE) {
					if (GodotObject.IsInstanceValid(shared.Value)) {
						RID rid = shared.Value.GetRid();
						shared.Value.Dispose();
					}
				}
			}
		}

		/// <summary>
		/// Checks the <see cref="IMAGE_ACQUISITIONS"/> and, if a url is new, enqueues a download for that image.
		/// </summary>
		/// <param name="url"></param>
		private static string EnqueueImageDownloadIfNeeded(string url, List<string> hashesForImages) {
			if (_currentImageCancellations == null) throw new InvalidOperationException($"Cannot enqueue image loading until {nameof(PrepareForImageLoading)} is called.");
			string md5 = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(url)));
			hashesForImages.Add(md5);

			string path = $"res://workshop_image_cache/{md5}";
			if (!ResourceLoader.Exists(path)) {
				lock (TEXTURE_SHARE_LOCK) {
					if (!TEXTURE_SHARE.ContainsKey(md5)) {
						ImageTexture result = ImageTexture.CreateFromImage(Assets.PlaceholderWorkshopImageLoading);
						result.TakeOverPath(path);
						TEXTURE_SHARE[md5] = result;
						_ = DownloadImageIntoTexture2DImpl(result, url, md5, _currentImageCancellations.Token);
					}
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
		private static async Task<Texture2D?> DownloadImageIntoTexture2DImpl(ImageTexture result, string url, string md5, CancellationToken cancellationToken) {

			// FIXME:
			// The current implementation bogs down the task scheduler and grinds most of the downloads to a halt.
			// In The Conservatory (my game, where most of the threading code from here comes from) this is solved
			// with LimitedConcurrencyTaskScheduler.

			string path = $"res://workshop_image_cache/{md5}";

			using Image actualImage = Image.CreateEmpty(1, 1, false, Image.Format.Rgba8);
			actualImage.SetPixel(0, 0, Colors.Magenta);

			string imgCache = Directories.GetSteamImageCacheDirectory();
			Directory.CreateDirectory(imgCache);

			// This *should* fix it getting slowed down?
			while (!RATE_LIMITER.WaitOne(0)) {
				cancellationToken.ThrowIfCancellationRequested();
				await Task.Yield();
			}
			try {
				byte[]? buffer = null;
				try {
					string cachePath = Path2.Combine(imgCache, $"{md5}.png");
					if (File.Exists(cachePath)) {
						// ^ Test this anyway. Exception handling is slow and this needs to be fast.
						buffer = File.ReadAllBytes(cachePath);
					}
				} catch (FileNotFoundException) {
				} catch (DirectoryNotFoundException) {
				}

				if (buffer != null) {
					if (actualImage.LoadPngFromBuffer(buffer) == Error.Ok) {
						result.SetImage(actualImage);
					}
					return result;
				}

				Stream? download = null;
				int retries = 3;
				while (retries-- > 0) {
					try {
						cancellationToken.ThrowIfCancellationRequested();
						download = await SBModManagerGlobals.HTTP_CLIENT.GetStreamAsync(url, cancellationToken).ConfigureAwait(false);
						break;
					} catch (HttpRequestException request) {
						if (request.StatusCode == HttpStatusCode.TooManyRequests) {
							await Task.Delay(5000).ConfigureAwait(false);
						} else if (request.StatusCode == HttpStatusCode.NotFound) {
							download = null;
							break;
						} else {
							throw;
						}
					}
				}
				if (download == null) {
					cancellationToken.ThrowIfCancellationRequested();
					result.SetImage(Assets.PlaceholderWorkshopImageError);
					Assets.PlaceholderWorkshopImageError.SavePng(Path2.Combine(imgCache, $"{md5}.png"));
					return result;
				}

				// TODO: Security? MemoryStream will fail out after 2GB.
				cancellationToken.ThrowIfCancellationRequested();
				using MemoryStream imageBuffer = new MemoryStream();
				download.CopyTo(imageBuffer);
				buffer = imageBuffer.ToArray();
				imageBuffer.Dispose();

				cancellationToken.ThrowIfCancellationRequested();
				Error error = actualImage.LoadPngFromBuffer(buffer);
				if (error != Error.Ok) {
					// Try it as a jpg?
					error = actualImage.LoadJpgFromBuffer(buffer);

					if (error != Error.Ok) {
						// Nope :(
						cancellationToken.ThrowIfCancellationRequested();
						result.SetImage(Assets.PlaceholderWorkshopImageError);
						Assets.PlaceholderWorkshopImageError.SavePng(Path2.Combine(imgCache, $"{md5}.png"));
						return result;
					}
				}

				cancellationToken.ThrowIfCancellationRequested();
				result.SetImage(actualImage);
				actualImage.SavePng(Path2.Combine(imgCache, $"{md5}.png"));
				await Task.Delay(100).ConfigureAwait(false);

				cancellationToken.ThrowIfCancellationRequested();
				return result;
			} catch {
				Assets.PlaceholderWorkshopImageError.SavePng(Path2.Combine(imgCache, $"{md5}.png"));
				return null;
			} finally {
				RATE_LIMITER.Release();
			}
		}

	}
}
