using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace SBModManager.SteamInterop.Web {

	public struct PublishedFileDetails {

		public EResult result;

		public int resultCount;

		public PublishedFileDetailsEntry[] publishedFileDetails;

		public PublishedFileDetails(GDDictionary json) {
			result = (EResult)(int)json["result"];
			if (result == EResult.OK) {
				resultCount = (int)json["resultcount"];
				publishedFileDetails = ((GDArray)json["publishedfiledetails"]).Select(static element => new PublishedFileDetailsEntry((GDDictionary)element)).ToArray();
			} else {
				resultCount = 0;
				publishedFileDetails = [];
			}
		}
	}

	public struct PublishedFileDetailsEntry {

		/// <summary>
		/// This app ID will be in <see cref="creatorAppID"/> if the item is a collection.
		/// </summary>
		public const long CREATOR_APP_WORKSHOP = 766;

		public EResult result;

		public long publishedFileID;

		/*

		public long creatorSteamID;

		*/

		public long creatorAppID;

		public long consumerAppID;

		public string hcontentFile;

		/*

		public string filename;

		public long fileSize;

		public string fileUrl;

		public string hcontentFile;

		public string previewURL;

		public string hcontentPreview;

		public DateTimeOffset timeCreated;

		*/

		public string title;

		public string description;

		public long timeUpdated;

		/*

		public int visibility;

		public int banned;

		public string banReason;

		public long subscriptions;

		public long favorited;

		public long lifetime_subscriptions;

		public long lifetime_favorited;

		public long views;

		*/

		public static PublishedFileDetailsEntry CreateVirtualTestItem() {
			return default(PublishedFileDetailsEntry) with {
				title = "Debug Fake Workshop Item",
				description = "This item isn't real, it's entirely virtual and used for testing.",
				publishedFileID = long.MinValue,
				consumerAppID = 211820,
				creatorAppID = 211820,
				timeUpdated = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
				hcontentFile = long.MaxValue.ToString()
			};
		}

		public PublishedFileDetailsEntry(GDDictionary json) {
			result = (EResult)(int)json["result"];
			if (result == EResult.OK) {
				title = (string)json["title"];
				description = (string)json["description"];

				publishedFileID = long.Parse((string)json["publishedfileid"]);
				consumerAppID = (long)json["consumer_app_id"];
				creatorAppID = (long)json["creator_app_id"];
				
				timeUpdated = (long)json["time_updated"];
				hcontentFile = (string)json["hcontent_file"];
			} else {
				title = string.Empty;
				description = string.Empty;
				hcontentFile = string.Empty;
			}
		}

	}
}
