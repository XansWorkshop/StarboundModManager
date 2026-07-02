using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SBModManager.ModInstances {

	/// <summary>
	/// A helper class which can read metadata from a mod archive without having to extract it.
	/// This also attempts to find and read the preview image of the mod into <c>"preview_image"</c> if it can.
	/// </summary>
	public static class MetadataReader {

		/// <summary>
		/// Reads the metadata of a mod from disk. The path can either be a mod directory, or a .pak file.
		/// Returns <see langword="null"/> if there is no metadata file, which is technically valid for Starbound,
		/// but I don't think anyone actually does it. Still, it's possible, so it has to be supported.
		/// </summary>
		/// <param name="modArchivePath"></param>
		/// <returns></returns>
		public static GDDictionary? ReadMetadataFromDisk(string modArchivePath) {
			if (File.GetAttributes(modArchivePath).HasFlag(FileAttributes.Directory)) { 
				try {
					DirectoryInfo archiveFolder = new DirectoryInfo(modArchivePath);
					GDDictionary? data = GetMetadataFromDirectory(archiveFolder);
					if (data != null) {
						FileInfo previewImage = new FileInfo(Path2.Combine(archiveFolder.FullName, "_previewimage"));
						if (previewImage.Exists) {
							try {
								data["preview_image"] = ImageTexture.CreateFromImage(Image.LoadFromFile(previewImage.FullName));
							} catch { }
						}
					}
					return data;
				} catch { }
			} else {
				try {
					FileInfo archiveFile = new FileInfo(modArchivePath);
					return GetMetadataFromPak(archiveFile, out _);
				} catch { }
			}

			return null;
		}

		public static GDDictionary? GetMetadataFromDirectory(DirectoryInfo directory) {
			if (!directory.Exists) return null;
			try {
				FileInfo metadata = new FileInfo(Path2.Combine(directory.FullName, "_metadata"));
				if (!metadata.Exists) metadata = new FileInfo(Path2.Combine(directory.FullName, ".metadata"));
				if (!metadata.Exists) return null;

				Variant json = Json.ParseString(File.ReadAllText(metadata.FullName));
				return (GDDictionary)json;
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Reads metadata from a .pak file. Includes a utility to see if the first 8 bytes of the file were correct,
		/// as this is used in mod source creation, and can prevent needing to open the file twice.
		/// </summary>
		/// <param name="packFile"></param>
		/// <param name="hadMalformedHeader"></param>
		/// <returns></returns>
		public static GDDictionary? GetMetadataFromPak(FileInfo packFile, out bool hadMalformedHeader) {
			hadMalformedHeader = false;
			try {
				if (!packFile.Exists) return null;

				using FileStream fs = File.OpenRead(packFile.FullName);
				using BinaryReader reader = new BinaryReader(fs);

				// And now: The most cursed way you've ever seen me read strings from a file.

				const ulong SBASSET6 = 3923872721875845715UL; // The sequence of characters "SBASSET6" as a little endian integer.
				if (reader.ReadUInt64() != SBASSET6) {
					hadMalformedHeader = true;
					return null;
				}

				long filePtr = reader.ReadInt64();
				filePtr = BinaryPrimitives.ReverseEndianness(filePtr); // Starbound writes in BE

				fs.Seek(filePtr, SeekOrigin.Begin);

				const ulong INDE = 1162104393; // The sequence of characters "INDE" as a little endian integer.
				const byte X = 88; // The letter "X" as a little endian integer.
				uint inde = reader.ReadUInt32();
				byte x = reader.ReadByte();
				if (inde != INDE || x != X) return null; // lmfao

				GDDictionary json = ReadNextJsonObject(reader);

				// Now the file index.
				try {
					long indexSize = ReadVlqU(reader);
					while (indexSize-- > 0) {
						string fileName = ReadNextDynLengthString(reader);
						long fileLocation = BinaryPrimitives.ReverseEndianness(reader.ReadInt64());
						long fileSize = BinaryPrimitives.ReverseEndianness(reader.ReadInt64());
						if (fileName == "/_previewimage") {
							byte[] buffer = new byte[fileSize];
							fs.Seek(fileLocation, SeekOrigin.Begin);
							fs.ReadExactly(buffer);

							Image image = Image.CreateEmpty(256, 256, false, Image.Format.Rgba8);
							Error loadError = image.LoadPngFromBuffer(buffer);
							if (loadError == Error.Ok) {
								json["preview_image"] = ImageTexture.CreateFromImage(image);
							} else if (loadError == Error.FileCorrupt) {
								// Hunch: Might be jpg
								loadError = image.LoadJpgFromBuffer(buffer);
								if (loadError == Error.Ok) {
									json["preview_image"] = ImageTexture.CreateFromImage(image);
								}
							}
							break;
						}
					}
				} catch { }

				return json;
			} catch {
				return null;
			}
		}


		private static string ReadNextDynLengthString(BinaryReader data) {
			long length = ReadVlqU(data);
			if (length > int.MaxValue) throw new NotSupportedException("String is too long. Is this data corrupted?");
			Span<byte> utf8 = length < 512 ? stackalloc byte[(int)length] : new byte[length];
			data.ReadExactly(utf8);
			return Encoding.UTF8.GetString(utf8);
		}

		private static GDDictionary ReadNextJsonObject(BinaryReader data) {
			long length = ReadVlqU(data);
			GDDictionary resultDict = [];
			for (long i = 0; i < length; i++) {
				resultDict[ReadNextDynLengthString(data)] = ReadNextBinJsonValue(data);
			}
			return resultDict;
		}

		private static Variant ReadNextBinJsonValue(BinaryReader data) {
			byte type = data.ReadByte();
			if (type > 0) type--;
			switch (type) {
				case 1:
					return data.ReadDouble();
				case 2:
					return data.ReadBoolean();
				case 3:
					return data.Read7BitEncodedInt64();
				case 4:
					return ReadNextDynLengthString(data); // Same code.
				case 5:
					long length = ReadVlqU(data);
					GDArray resultArr = [];
					for (long i = 0; i < length; i++) {
						resultArr.Add(ReadNextBinJsonValue(data));
					}
					return resultArr;
				case 6:
					return ReadNextJsonObject(data);
				default:
					return default;
			}

		}

		private static long ReadVlqU(BinaryReader data) {
			long x = 0;
			for (int i = 0; i < 10; ++i) {
				//uint8_t oct = *in++;
				byte oct = data.ReadByte();
				x = (x << 7) | (long)(oct & 127);
				if ((oct & 128) == 0) {
					return x;
				}
			}
			throw new InvalidDataException();
		}


	}
}
