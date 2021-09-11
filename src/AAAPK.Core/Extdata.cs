using System;
using System.Collections.Generic;
using System.Linq;

using MessagePack;

using ExtensibleSaveFormat;

using JetPack;

namespace AAAPK
{
	public partial class AAAPK
	{
		public enum ParentType
		{
			Unknown,
			Clothing,
			Accessory,
			Hair,
			Character,
		}

		[Serializable]
		[MessagePackObject]
		public class ParentRule
		{
			[Key("Coordinate")]
			public int Coordinate { get; set; }
			[Key("Slot")]
			public int Slot { get; set; }
			[Key("ParentPath")]
			public string ParentPath { get; set; }
			[Key("ParentType")]
			public ParentType ParentType { get; set; }
			[Key("ParentSlot")]
			public int ParentSlot { get; set; }
		}
#if KKS
		internal static void InitCardImport()
		{
			ExtendedSave.CardBeingImported += CardBeingImported;
		}

		internal static void CardBeingImported(Dictionary<string, PluginData> _importedExtData, Dictionary<int, int?> _coordinateMapping)
		{
			if (_importedExtData.TryGetValue(ExtDataKey, out PluginData _pluginData))
			{
				List<ParentRule> ParentRules = new List<ParentRule>();

				if (_pluginData != null)
				{
					if (_pluginData.data.TryGetValue("ParentRules", out object _loadedParentRules) && _loadedParentRules != null)
					{
						List<ParentRule> _tempParentRules = MessagePackSerializer.Deserialize<List<ParentRule>>((byte[]) _loadedParentRules);
						if (_tempParentRules?.Count > 0)
						{
							for (int i = 0; i < _coordinateMapping.Count; i++)
							{
								if (_coordinateMapping[i] == null) continue;

								List<ParentRule> _copy = _tempParentRules.Where(x => x.Coordinate == i).ToList().JsonClone<List<ParentRule>>();
								if (_copy.Count == 0) continue;

								_copy.ForEach(x => x.Coordinate = (int) _coordinateMapping[i]);
								ParentRules.AddRange(_copy);
							}
						}
					}
				}

				_importedExtData.Remove(ExtDataKey);

				if (ParentRules?.Count > 0)
				{
					PluginData _pluginDataNew = new PluginData() { version = ExtDataVer };
					_pluginDataNew.data.Add("ParentRules", MessagePackSerializer.Serialize(ParentRules));
					_importedExtData[ExtDataKey] = _pluginDataNew;
				}
			}
		}
#endif
	}
}
