using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SBModManager.SteamInterop.Web {

	public struct CollectionDetails {
		public EResult result;

		public int resultCount;

		public CollectionDetailsEntry[] collectionDetails;

		public CollectionDetails(GDDictionary json) {
			result = (EResult)(int)json["result"];
			if (result == EResult.OK) {
				resultCount = (int)json["resultcount"];
				collectionDetails = ((GDArray)json["collectiondetails"]).Select(static element => new CollectionDetailsEntry((GDDictionary)element)).ToArray();
			} else {
				resultCount = 0;
				collectionDetails = [];
			}
		}
	}

	/// <summary>
	/// An entry in the <c>collectiondetails</c> array of a request to get collection information from Steam.
	/// </summary>
	public struct CollectionDetailsEntry {

		public long publishedFileID;

		public EResult result;

		public CollectionDetailsEntryChild[] children;

		public CollectionDetailsEntry(GDDictionary json) {
			publishedFileID = long.Parse((string)json["publishedfileid"]);
			result = (EResult)(int)json["result"];
			if (result == EResult.OK) {
				children = ((GDArray)json["children"]).Select(static element => new CollectionDetailsEntryChild((GDDictionary)element)).ToArray();
			} else {
				children = [];
			}
		}

	}

	public struct CollectionDetailsEntryChild {

		public long publishedFileID;

		public long sortOrder;

		public EWorkshopFileType fileType;

		public CollectionDetailsEntryChild(GDDictionary json) {
			publishedFileID = long.Parse((string)json["publishedfileid"]);
			sortOrder = (int)json["sortorder"];
			fileType = (EWorkshopFileType)(int)json["filetype"];
		}

	}
}
