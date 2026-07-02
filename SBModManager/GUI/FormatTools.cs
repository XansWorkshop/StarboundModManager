using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace SBModManager.GUI {

	/// <summary>
	/// Copied from The Conservatory, includes some string formatting tools.
	/// </summary>
	public static partial class FormatTools {

		private static FrozenDictionary<string, Color> COLORS = new Dictionary<string, Color> {
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
		/// An extremely shitty tool which lazily and destructively converts a starbound formatted string (with color codes) into one that works,
		/// albeit barely, in Godot. This bbcode is not at all valid and this abuses the fact that Godot allows it.
		/// </summary>
		/// <param name="sbString"></param>
		/// <returns></returns>
		public static string ShittyStarboundMarkupToBBCode(string sbString) {
			sbString = SBHexColorRegex().Replace(sbString, delegate (Match match) {
				if (match.Success) {
					return $"[color={match.Groups[1]}]";
				} else {
					return match.Value;
				}
			});
			foreach (KeyValuePair<string, Color> colorMapping in COLORS) {
				sbString = sbString.Replace($"^{colorMapping.Key};", $"[color=#{colorMapping.Value.ToHtml()}]", StringComparison.OrdinalIgnoreCase);
			}
			return sbString.Replace("^reset;", "[color=white]", StringComparison.OrdinalIgnoreCase);
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

		[GeneratedRegex(@"#\^([a-fA-F0-9]{3}|[a-fA-F0-9]{4}|[a-fA-F0-9]{6}|[a-fA-F0-9]{8});")]
		private static partial Regex SBHexColorRegex();
	}
}
