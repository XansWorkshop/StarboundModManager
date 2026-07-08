using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
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

		private static Color GetColor(ReadOnlySpan<char> from) {
			if (from.Length == 0) return Colors.White;
			if (from[0] == '#') {
				return Color.FromHtml(from);
			} else {
				foreach (KeyValuePair<string, Color> binding in COLORS) {
					if (MemoryExtensions.Equals(binding.Key, from, StringComparison.Ordinal)) {
						return binding.Value;
					}
				}
			}
			return Colors.White;
		}

		/// <summary>
		/// Reparses the bbcode found in Steam Workshop pages such that it is compatible with Godot's formatting.
		/// </summary>
		/// <param name="sbOrWorkshopDesc"></param>
		/// <returns></returns>
		[NoDiscard]
		public static string TranslateSteamWorkshopBBCode(string sbOrWorkshopDesc) {
			// You know, in the past, no matter how hard I tried, I couldn't write a custom replacer that was faster than a wall of Replaces.
			// The ignored case is important because Godot requires lowercase tags. Steam does not.
			return sbOrWorkshopDesc.Replace("[B]", "[b]", StringComparison.OrdinalIgnoreCase).Replace("[/B]", "[/b]", StringComparison.OrdinalIgnoreCase)
					.Replace("[I]", "[i]", StringComparison.OrdinalIgnoreCase).Replace("[/I]", "[/i]", StringComparison.OrdinalIgnoreCase)
					.Replace("[U]", "[u]", StringComparison.OrdinalIgnoreCase).Replace("[/U]", "[/u]", StringComparison.OrdinalIgnoreCase)
					// .Replace("[IMG]", "[img]", StringComparison.OrdinalIgnoreCase).Replace("[/IMG]", "[/img]", StringComparison.OrdinalIgnoreCase)
					// .Replace("[URL]", "[url]", StringComparison.OrdinalIgnoreCase).Replace("[/URL]", "[/url]", StringComparison.OrdinalIgnoreCase)
					.Replace("[STRIKE]", "[s]", StringComparison.OrdinalIgnoreCase).Replace("[/STRIKE]", "[/s]", StringComparison.OrdinalIgnoreCase)
					.Replace("[LIST]", "[ul]", StringComparison.OrdinalIgnoreCase).Replace("[/LIST]", "[/ul]", StringComparison.OrdinalIgnoreCase)
					.Replace("[OLIST]", "[ol]", StringComparison.OrdinalIgnoreCase).Replace("[/OLIST]", "[/ol]", StringComparison.OrdinalIgnoreCase)
					.Replace("[*]", null, StringComparison.OrdinalIgnoreCase) // Used in lists
					.Replace("[/HR]", null, StringComparison.OrdinalIgnoreCase) // Godot doesn't use a closing tag.
					.Replace("[LI]", null, StringComparison.OrdinalIgnoreCase).Replace("[/LI]", null, StringComparison.OrdinalIgnoreCase)
					.Replace("[h1]", "[font_size=24]", StringComparison.OrdinalIgnoreCase).Replace("[/h1]", "[/font_size]", StringComparison.OrdinalIgnoreCase)
					.Replace("[h2]", "[font_size=20]", StringComparison.OrdinalIgnoreCase).Replace("[/h2]", "[/font_size]", StringComparison.OrdinalIgnoreCase)
					.Replace("[h3]", "[font_size=16]", StringComparison.OrdinalIgnoreCase).Replace("[/h3]", "[/font_size]", StringComparison.OrdinalIgnoreCase)
					.Replace("[h4]", "[font_size=14]", StringComparison.OrdinalIgnoreCase).Replace("[/h4]", "[/font_size]", StringComparison.OrdinalIgnoreCase)
					.Replace("[h5]", "[font_size=12]", StringComparison.OrdinalIgnoreCase).Replace("[/h5]", "[/font_size]", StringComparison.OrdinalIgnoreCase)
					.Replace("[h6]", "[font_size=10]", StringComparison.OrdinalIgnoreCase).Replace("[/h6]", "[/font_size]", StringComparison.OrdinalIgnoreCase);
		}

		/// <summary>
		/// I'm so sorry for the bullshit you're about to lay your eyes upon.
		/// </summary>
		/// <param name="sbOrWorkshopDesc">The description of a mod, using either Steam markup or Starbound markup.</param>
		/// <param name="hashesForImages">Loaded by the image loader, this stores the md5 hashes for every inline image so that they can be quickly reloaded.</param>
		/// <returns></returns>
		public static string ReparseStarboundIntoBBCode(string sbOrWorkshopDesc, List<string> hashesForImages) {
			// Starbound markup:
			sbOrWorkshopDesc = StarboundMarkupToBBCode(sbOrWorkshopDesc);

			// Steam markup:
			sbOrWorkshopDesc = TranslateSteamWorkshopBBCode(sbOrWorkshopDesc);

			// For URL and IMG:
			sbOrWorkshopDesc = URLBBCodeResolver().Replace(sbOrWorkshopDesc, delegate (Match match) {
				if (!match.Success) return match.Value;
				return $"[color=#aff][url{match.Groups[1].Value}]{match.Groups[2].Value}[/url][/color]";
			});
			sbOrWorkshopDesc = IMGBBCodeResolver().Replace(sbOrWorkshopDesc, delegate (Match match) {
				if (!match.Success) return match.Value;
				return $"[img]{match.Groups[1].Value}[/img]";
			});

			// Image Fixers
			// The idea here is to create a dummy texture and then download it in the background.
			return InlineThumbnailImageHelper.ReplaceImages(sbOrWorkshopDesc, hashesForImages);
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

			// Something built into sb.
			sbString = sbString.Replace("¤", "[img height=1em]res://icons/starbound.png[/img]");
			Span<char> commandBuffer = stackalloc char[64];
			ReadOnlySpan<char> contents = sbString;
			StringBuilder result = new StringBuilder();
			// ^ It is impossible for the result to be longer than the original.

			// Starbound has multiple format tags.
			// The ones in common knowledge are:
			//		^#hexhex; for custom colors
			//		^named; (see the top of this file) for named colors
			//		^reset; to go back to default settings

			// Less common:
			//		^shadow; to add a dropshadow

			// And beyond the knowledge of most SB modders:
			//		^set; to change what ^reset; considers to be "default" settings.
			//		^shadow=COLOR; to change the shadow to a specific color (hex or named)
			//		^noshadow;
			//		NOTE: ^set; sets the alpha of the color, and the alpha of the set settings multiplies with the alpha of any color that is set now.
			//					* Unless it's the shadow color, which has an alpha set explicitly no matter what.
			//		^directives=; and ^backdirectives=; (yes, like the ones you use for sprite colors)

			// For writing to commandBuffer; -1 indicates that there is no command being written.
			int commandWriteHead = -1;

			// The character index in contents where a command started.
			int commandEnteredCheckpoint = -1;

			Color savedColor = Colors.White;
			Color savedShadowColor = Colors.Transparent;
			string? savedFont = null;
			// ^ TODO: Support fonts?
			Color currentColor = Colors.White;
			Color currentShadowColor = Colors.Transparent;
			string? currentFont = null;

			result.Append("[outline_size=8]"); // This is a hack which allows shadows to work.
			result.Append("[outline_color=#0000]"); // Just make it transparent.
			result.Append("[color=#fff]");

			Stack<string> bbCodeEffectStack = [];
			// bbCodeEffectStack.Push("outline_size");
			bbCodeEffectStack.Push("outline_color");
			bbCodeEffectStack.Push("color");

			for (int i = 0; i < contents.Length; i++) {
				char c = contents[i];
				if (c == '^') {
					// A command was started.
					if (commandWriteHead != -1) {
						// But we were in one already, which means the previous one is invalid.
						// The command buffer does not include the leading ^:
						result.Append('^');
						result.Append(commandBuffer);
						commandWriteHead = -1;
					}

					commandEnteredCheckpoint = i;
					commandWriteHead = 0;
				} else {
					if (commandWriteHead != -1) {
						if (commandWriteHead == 63) {
							// Overflowed the command buffer. No command is this long.
							// Except for directives, maybe, but I don't support those.
							result.Append('^');
							result.Append(commandBuffer);
							result.Append(c); // Also append the current character, we care about that one too.
							commandWriteHead = -1;
							continue;
						}

						bool beginMultiCommand = false;
						if (c == ',') {
							// Multiple commands.
							beginMultiCommand = true;
						}

						if (c == ';' || beginMultiCommand) {
							// Command terminator
							Color inlineAlphaMultipliedColor = currentColor;
							// ^ Used because ^set;'s stored color multiplies with currentColor when setting a color.
							// But it doesn't store it in currentColor so it should not be remembered.
							
							bool shouldPushRendering = true;
							if (MemoryExtensions.Equals(commandBuffer, "reset", StringComparison.Ordinal)) {
								while (bbCodeEffectStack.TryPop(out string? tag)) {
									result.Append($"[/{tag}]");
								}
								currentColor = savedColor;
								currentShadowColor = savedShadowColor;
								currentFont = savedFont;
								inlineAlphaMultipliedColor = currentColor; // Reassign this
							} else if (MemoryExtensions.Equals(commandBuffer, "set", StringComparison.Ordinal)) {
								savedColor = currentColor;
								savedShadowColor = currentShadowColor;
								savedFont = currentFont;
								shouldPushRendering = false;
							} else if (MemoryExtensions.StartsWith(commandBuffer, "shadow", StringComparison.Ordinal)) {
								// Remember: writeHead doubles as length.
								if (commandWriteHead > 6 && commandBuffer[6] == '=') {
									// Shadow with color directive.
									currentShadowColor = GetColor(commandBuffer[7..commandWriteHead]);
								} else {
									// Plain shadow.
									currentShadowColor = Colors.Black;
								}
							} else if (MemoryExtensions.Equals(commandBuffer, "noshadow", StringComparison.Ordinal)) {
								currentShadowColor = Colors.Transparent;
							} else if (MemoryExtensions.StartsWith(commandBuffer, "font=", StringComparison.Ordinal)) {
								shouldPushRendering = false;
							} else if (MemoryExtensions.StartsWith(commandBuffer, "directives=", StringComparison.Ordinal)) {
								shouldPushRendering = false;
							} else if (MemoryExtensions.StartsWith(commandBuffer, "subdirectives=", StringComparison.Ordinal)) {
								shouldPushRendering = false;
							} else {
								currentColor = GetColor(commandBuffer[..commandWriteHead]);
								// Rewrite this and do that special alpha multiplication.
								inlineAlphaMultipliedColor = currentColor with { A = currentColor.A * savedColor.A };
							}

							if (shouldPushRendering) {
								while (bbCodeEffectStack.TryPop(out string? tag)) {
									result.Append($"[/{tag}]");
								}
								result.Append($"[outline_color=#{currentShadowColor.ToHtml()}]");
								result.Append($"[color=#{inlineAlphaMultipliedColor.ToHtml()}]");
								bbCodeEffectStack.Push("outline_color");
								bbCodeEffectStack.Push("color");
							}

							if (beginMultiCommand) {
								commandWriteHead = 0; // Just move it back to the start and start writing again.
							} else {
								commandWriteHead = -1;
							}

						} else {
							commandBuffer[commandWriteHead++] = c;
						}
					} else {
						result.Append(c);	
					}
				}
			}

			// Don't forget to pop some tags.
			// ($20 not included)
			while (bbCodeEffectStack.TryPop(out string? tag)) {
				result.Append($"[/{tag}]");
			}
			result.Append("[/outline_size]"); // Guaranteed to be present.

			return result.ToString();

			/*

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
			*/

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
