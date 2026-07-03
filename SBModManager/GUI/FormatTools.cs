using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

using SBModManager.SteamInterop;

namespace SBModManager.GUI {

	/// <summary>
	/// <strong>Copied from The Conservatory's codebase.</strong>
	/// <para/>
	/// 
	/// Various string formatting tools. Modified to have some Starbound stuff in it.
	/// </summary>
	public static partial class FormatTools {

		private static readonly FrozenDictionary<string, Color> COLORS = new Dictionary<string, Color> {
			{ "red", Color.Color8(255, 73, 66, 255) },
			{ "orange", Color.Color8(255, 180, 47, 255) },
			{ "yellow", Color.Color8(255, 239, 30, 255) },
			{ "green", Color.Color8(79, 230, 70, 255) },
			{ "blue", Color.Color8(38, 96, 255, 255) },
			{ "indigo", Color.Color8(75, 0, 130, 255) },
			{ "violet", Color.Color8(160, 119, 255, 255) },
			{ "black", Color.Color8(0, 0, 0, 255) },
			{ "white", Color.Color8(255, 255, 255, 255) },
			{ "magenta", Color.Color8(221, 92, 249, 255) },
			{ "darkmagenta", Color.Color8(142, 33, 144, 255) },
			{ "cyan", Color.Color8(0, 220, 233, 255) },
			{ "darkcyan", Color.Color8(0, 137, 165, 255) },
			{ "cornflowerblue", Color.Color8(100, 149, 237, 255) },
			{ "gray", Color.Color8(160, 160, 160, 255) },
			{ "lightgray", Color.Color8(192, 192, 192, 255) },
			{ "darkgray", Color.Color8(128, 128, 128, 255) },
			{ "darkgreen", Color.Color8(0, 128, 0, 255) },
			{ "pink", Color.Color8(255, 162, 187, 255) },
			{ "clear", Color.Color8(0, 0, 0, 0) }
		}.ToFrozenDictionary();

		/// <summary>
		/// I'm so sorry for the bullshit you're about to lay your eyes upon.
		/// </summary>
		/// <param name="sbOrWorkshopDesc">The description of a mod, using either Steam markup or Starbound markup.</param>
		/// <returns></returns>
		public static string ReparseStarboundIntoBBCode(string sbOrWorkshopDesc) {
			// Starbound markup:
			sbOrWorkshopDesc = StarboundMarkupToBBCode(sbOrWorkshopDesc);

			// Because some people like to use all caps bbcode...
			sbOrWorkshopDesc = sbOrWorkshopDesc.Replace("[B]", "[b]", StringComparison.OrdinalIgnoreCase).Replace("[/B]", "[/b]", StringComparison.OrdinalIgnoreCase)
												.Replace("[I]", "[i]", StringComparison.OrdinalIgnoreCase).Replace("[/I]", "[/i]", StringComparison.OrdinalIgnoreCase)
												.Replace("[U]", "[u]", StringComparison.OrdinalIgnoreCase).Replace("[/U]", "[/u]", StringComparison.OrdinalIgnoreCase)
												// .Replace("[IMG]", "[img]", StringComparison.OrdinalIgnoreCase).Replace("[/IMG]", "[/img]", StringComparison.OrdinalIgnoreCase)
												// .Replace("[URL]", "[url]", StringComparison.OrdinalIgnoreCase).Replace("[/URL]", "[/url]", StringComparison.OrdinalIgnoreCase)
												.Replace("[STRIKE]", "[s]", StringComparison.OrdinalIgnoreCase).Replace("[/STRIKE]", "[/s]", StringComparison.OrdinalIgnoreCase)
												.Replace("[LIST]", "[ul]", StringComparison.OrdinalIgnoreCase).Replace("[/LIST]", "[/ul]", StringComparison.OrdinalIgnoreCase)
												.Replace("[OLIST]", "[ol]", StringComparison.OrdinalIgnoreCase).Replace("[/OLIST]", "[/ol]", StringComparison.OrdinalIgnoreCase)
												.Replace("[*]", null, StringComparison.OrdinalIgnoreCase) // Used in lists
												.Replace("[/HR]", null, StringComparison.OrdinalIgnoreCase) // Godot doesn't use a closing tag.
												.Replace("[LI]", null, StringComparison.OrdinalIgnoreCase).Replace("[/LI]", null, StringComparison.OrdinalIgnoreCase);

			// For URL and IMG:
			sbOrWorkshopDesc = URLBBCodeResolver().Replace(sbOrWorkshopDesc, delegate (Match match) {
				if (!match.Success) return match.Value;
				return $"[color=#aff][url{match.Groups[1].Value}]{match.Groups[2].Value}[/url][/color]";
			});
			sbOrWorkshopDesc = IMGBBCodeResolver().Replace(sbOrWorkshopDesc, delegate (Match match) {
				if (!match.Success) return match.Value;
				return $"[img]{match.Groups[1].Value}[/img]";
			});

			// Steam Workshop formatting:
			sbOrWorkshopDesc = sbOrWorkshopDesc.Replace("[h1]", "[font_size=24]", StringComparison.OrdinalIgnoreCase).Replace("[/h1]", "[/font_size]", StringComparison.OrdinalIgnoreCase)
												.Replace("[h2]", "[font_size=20]", StringComparison.OrdinalIgnoreCase).Replace("[/h2]", "[/font_size]", StringComparison.OrdinalIgnoreCase)
												.Replace("[h3]", "[font_size=16]", StringComparison.OrdinalIgnoreCase).Replace("[/h3]", "[/font_size]", StringComparison.OrdinalIgnoreCase)
												.Replace("[h4]", "[font_size=14]", StringComparison.OrdinalIgnoreCase).Replace("[/h4]", "[/font_size]", StringComparison.OrdinalIgnoreCase)
												.Replace("[h5]", "[font_size=12]", StringComparison.OrdinalIgnoreCase).Replace("[/h5]", "[/font_size]", StringComparison.OrdinalIgnoreCase)
												.Replace("[h6]", "[font_size=10]", StringComparison.OrdinalIgnoreCase).Replace("[/h6]", "[/font_size]", StringComparison.OrdinalIgnoreCase);

			// Image Fixers
			// The idea here is to create a dummy texture and then download it in the background.
			return InlineThumbnailImageHelper.ReplaceImages(sbOrWorkshopDesc);
		}

		/// <summary>
		/// An extremely shitty tool which lazily and destructively converts a starbound formatted string (with color codes) into one that works,
		/// albeit barely, in Godot. This bbcode is not at all valid and this abuses the fact that Godot allows it.
		/// </summary>
		/// <param name="sbString"></param>
		/// <returns></returns>
		[NoDiscard]
		public static string StarboundMarkupToBBCode(string sbString, bool alsoEscapeBBCode = false) {
			if (alsoEscapeBBCode) {
				sbString = sbString.Replace("[", "[lb]");
			}

			// okay so check this out right:
			// replace every color with a closing tag, then the opening of the next tag
			string original = sbString;
			sbString = SBHexColorRegex().Replace(sbString, delegate (Match match) {
				if (match.Success) {
					return $"[/color][color={match.Groups[1].Value}]";
				} else {
					return match.Value;
				}
			});
			foreach (KeyValuePair<string, Color> colorMapping in COLORS) {
				sbString = sbString.Replace($"^{colorMapping.Key};", $"[/color][color=#{colorMapping.Value.ToHtml()}]", StringComparison.OrdinalIgnoreCase);
			}
			sbString = sbString.Replace("^reset;", "[/color][color=#fff]", StringComparison.OrdinalIgnoreCase);

			if (!ReferenceEquals(sbString, original)) {
				// All replacement methods used will return the input by reference if there were no edits.
				// But if there were edits, every edit begins with closing its previous tag, and opening the next tag
				// which means I can just surround the result with a tag and bang. it's valid.
				// This is so cursed lmfao
				return $"[color=#fff]{sbString}[/color]";
			}
			return original;
		}

		/// <summary>
		/// Provided with a number of bytes, this will find the largest SI unit (TB, GB, MB, kB, Bytes) that the amount of bytes
		/// can fit a whole number into, and then trims the rest down to three decimal places. Returns a number decorated
		/// with a unit i.e. 1000000 becomes "1 MB"
		/// </summary>
		/// <remarks>
		/// This method uses SI units based on powers of 10, not IEC units based on powers of 2.<br/>
		/// See: <see href="https://en.wikipedia.org/wiki/Byte#Multiple-byte_units"/><br/>
		/// See also: <see cref="ToLargestIECUnitByteSize(ulong)"/>
		/// <para/>
		/// For clarification: Yes, this uses a lowercase k for kilobytes (kB, not KB). This is part of the standard.
		/// </remarks>
		/// <param name="bytes"></param>
		/// <returns></returns>
		[NoDiscard]
		public static string ToLargestSIUnitByteSize(ulong bytes) {
			string bytesText;
			if (bytes >= 1_000_000_000_000) {
				bytesText = $"{(bytes / 1_000_000_000_000D):0.###} TB";
			} else if (bytes >= 1_000_000_000) {
				bytesText = $"{(bytes / 1_000_000_000D):0.###} GB";
			} else if (bytes >= 1_000_000) {
				bytesText = $"{(bytes / 1_000_000D):0.###} MB";
			} else if (bytes >= 1_000) {
				bytesText = $"{(bytes / 1_000D):0.###} kB";
			} else {
				bytesText = $"{bytes:N0} Bytes";
			}
			return bytesText;
		}
		/// <summary>
		/// Provided with a number of bytes, this will find the largest IEC unit (TiB, GiB, MiB, KiB, Bytes) that the amount of bytes
		/// can fit a whole number into, and then trims the rest down to three decimal places. Returns a number decorated
		/// with a unit i.e. 1048576 becomes "1 MiB"
		/// </summary>
		/// <remarks>
		/// This method uses IEC units based on powers of 2, not SI units based on powers of 10.<br/>
		/// See: <see href="https://en.wikipedia.org/wiki/Byte#Multiple-byte_units"/><br/>
		/// See also: <see cref="ToLargestSIUnitByteSize(ulong)"/>
		/// <para/>
		/// For clarification: Yes, this uses a capital K for kibibytes (KiB, not kiB). This is part of the standard.
		/// </remarks>
		/// <param name="bytes"></param>
		/// <returns></returns>
		[NoDiscard]
		public static string ToLargestIECUnitByteSize(ulong bytes) {
			string bytesText;
			if (bytes >= (1UL << 40)) {
				bytesText = $"{((double)bytes / (1UL << 40)):0.###} TiB";
			} else if (bytes >= (1UL << 30)) {
				bytesText = $"{((double)bytes / (1UL << 30)):0.###} GiB";
			} else if (bytes >= (1UL << 20)) {
				bytesText = $"{((double)bytes / (1UL << 20)):0.###} MiB";
			} else if (bytes >= (1UL << 10)) {
				bytesText = $"{((double)bytes / (1UL << 10)):0.###} KiB";
			} else {
				bytesText = $"{bytes:N0} Bytes";
			}
			return bytesText;
		}

		[GeneratedRegex(@"\^#([a-fA-F0-9]{3}|[a-fA-F0-9]{4}|[a-fA-F0-9]{6}|[a-fA-F0-9]{8});")]
		private static partial Regex SBHexColorRegex();

		[GeneratedRegex(@"\[url(\=[^\]]+)?\]([^\[\]]+)\[\/url\]", RegexOptions.IgnoreCase)]
		private static partial Regex URLBBCodeResolver();

		[GeneratedRegex(@"\[img(?:\=[^\]]+)?\]([^\[\]]+)\[\/img\]", RegexOptions.IgnoreCase)]
		public static partial Regex IMGBBCodeResolver();
	}
}
