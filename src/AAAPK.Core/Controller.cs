using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;

using UnityEngine;
using MessagePack;
using ParadoxNotion.Serialization;

using BepInEx.Logging;
using HarmonyLib;

using ExtensibleSaveFormat;

using KKAPI;
using KKAPI.Chara;
using JetPack;

namespace AAAPK
{
	public partial class AAAPK
	{
		internal static AAAPKController GetController(ChaControl _chaCtrl) => _chaCtrl?.gameObject?.GetComponent<AAAPKController>();
		internal static CharaCustomFunctionController GetBoneController(ChaControl _chaCtrl) => _chaCtrl?.gameObject?.GetComponent<KKABMX.Core.BoneController>();

		public partial class AAAPKController : CharaCustomFunctionController
		{
			internal int _currentCoordinateIndex => ChaControl.fileStatus.coordinateType;

			internal bool _duringLoadChange = false;
			internal List<ParentRule> ParentRuleList = new List<ParentRule>();
			internal HashSet<int> _triggerSlots = new HashSet<int>();
			internal HashSet<int> _queueSlots = new HashSet<int>();
			internal List<ParentRule> _rulesCache = new List<ParentRule>();

			internal List<ChaFileAccessory.PartsInfo> _listPartsInfo = new List<ChaFileAccessory.PartsInfo>();
			internal HashSet<int> _usedSlots = new HashSet<int>();

			protected override void OnCardBeingSaved(GameMode currentGameMode)
			{
				if (ParentRuleList?.Count == 0)
				{
					SetExtendedData(null);
					return;
				}

				PluginData _pluginData = new PluginData();
				_pluginData.data.Add("ParentRules", MessagePackSerializer.Serialize(ParentRuleList));
				_pluginData.version = ExtDataVer;
				SetExtendedData(_pluginData);
			}

			protected override void OnCoordinateBeingSaved(ChaFileCoordinate coordinate)
			{
				List<ParentRule> _data = ListCoordinateRule().JsonClone<List<ParentRule>>();
				if (_data?.Count == 0)
				{
					SetCoordinateExtendedData(coordinate, null);
					return;
				}
				_data.ForEach(x => x.Coordinate = -1);

				PluginData _pluginData = new PluginData();
				_pluginData.data.Add("ParentRules", MessagePackSerializer.Serialize(_data));
				_pluginData.version = ExtDataVer;
				SetCoordinateExtendedData(coordinate, _pluginData);
			}

			protected override void OnCoordinateBeingLoaded(ChaFileCoordinate coordinate)
			{
				_duringLoadChange = true;

				ParentRuleList.RemoveAll(x => x.Coordinate == _currentCoordinateIndex);
				PluginData _pluginData = GetCoordinateExtendedData(coordinate);
				if (_pluginData != null)
				{
					if (_pluginData.version > ExtDataVer)
						_logger.Log(LogLevel.Error | LogLevel.Message, $"[OnCoordinateBeingLoaded] ExtendedData.version: {_pluginData.version} is newer than your plugin");
					else
					{
						if (_pluginData.data.TryGetValue("ParentRules", out object _loadedParentRules) && _loadedParentRules != null)
						{
							List<ParentRule> _tempParentRules = MessagePackSerializer.Deserialize<List<ParentRule>>((byte[]) _loadedParentRules);
							if (_tempParentRules?.Count > 0)
							{
								_tempParentRules.ForEach(x => x.Coordinate = _currentCoordinateIndex);
								ParentRuleList.AddRange(_tempParentRules);
							}
						}
					}
				}
				StartCoroutine(OnCoordinateBeingLoadedCoroutine());
				base.OnCoordinateBeingLoaded(coordinate);
			}

			private IEnumerator OnCoordinateBeingLoadedCoroutine()
			{
				yield return JetPack.Toolbox.WaitForEndOfFrame;
				yield return JetPack.Toolbox.WaitForEndOfFrame;
				RefreshCache();
				_duringLoadChange = false;
			}

			protected override void OnReload(GameMode currentGameMode)
			{
				_duringLoadChange = true;

				ParentRuleList.Clear();
				PluginData _pluginData = GetExtendedData();
				if (_pluginData != null)
				{
					if (_pluginData.version > ExtDataVer)
						_logger.Log(LogLevel.Error | LogLevel.Message, $"[OnReload] ExtendedData.version: {_pluginData.version} is newer than your plugin");
					else
					{
						if (_pluginData.data.TryGetValue("ParentRules", out object _loadedParentRules) && _loadedParentRules != null)
						{
							List<ParentRule> _tempParentRules = MessagePackSerializer.Deserialize<List<ParentRule>>((byte[]) _loadedParentRules);
							if (_tempParentRules?.Count > 0)
								ParentRuleList.AddRange(_tempParentRules);
						}
					}
				}
				RefreshCache();
				base.OnReload(currentGameMode);
			}

			internal void AccessoriesCopiedHandler(int _srcCoordinateIndex, int _dstCoordinateIndex, List<int> _copiedSlotIndexes)
			{
				foreach (int _slotIndex in _copiedSlotIndexes)
					CloneRule(_slotIndex, _slotIndex, _srcCoordinateIndex, _dstCoordinateIndex);

				if (_dstCoordinateIndex == _currentCoordinateIndex)
				{
					UpdatePartsInfoList();
					RefreshCache();
					StartCoroutine(ApplyParentRuleListHack("AccessoriesCopiedHandler"));
				}
			}

			internal void AccessoryTransferredHandler(int _srcSlotIndex, int _dstSlotIndex)
			{
				CloneRule(_srcSlotIndex, _dstSlotIndex, _currentCoordinateIndex);

				UpdatePartsInfoList();
				RefreshCache();
				StartCoroutine(ApplyParentRuleListHack("AccessoryTransferredHandler"));
			}

			internal List<ParentRule> ListCoordinateRule() => ListCoordinateRule(_currentCoordinateIndex);
			internal List<ParentRule> ListCoordinateRule(int _coordinateIndex) => ParentRuleList.Where(x => x.Coordinate == _coordinateIndex).ToList();

			internal ParentRule GetSlotRule(int _slotIndex) => GetSlotRule(_currentCoordinateIndex, _slotIndex);
			internal ParentRule GetSlotRule(int _coordinateIndex, int _slotIndex) => ParentRuleList.FirstOrDefault(x => x.Coordinate == _coordinateIndex && x.Slot == _slotIndex);

			internal IEnumerator ApplyParentRuleListHack(string _caller)
			{
				if (_duringLoadChange)
					yield break;

				yield return JetPack.Toolbox.WaitForEndOfFrame;
				yield return JetPack.Toolbox.WaitForEndOfFrame;

				ApplyParentRuleList(_caller);
			}

			internal void ApplyParentRuleList(string _caller)
			{
				if (_duringLoadChange) return;

				AccGotHighRemoveEffect();
				_triggerSlots = new HashSet<int>(ListCoordinateRule().OrderBy(x => x.Slot).Select(x => x.Slot));
				if (_triggerSlots?.Count == 0) return;
				DebugMsg(LogLevel.Info, $"[ApplyParentRuleList][{_caller}][_currentCoordinateIndex: {_currentCoordinateIndex}][count: {_triggerSlots?.Count}]");
				StartCoroutine(ApplyParentRuleListCoroutine());
			}

			internal IEnumerator ApplyParentRuleListCoroutine()
			{
				if (_duringLoadChange)
					yield break;

				yield return JetPack.Toolbox.WaitForEndOfFrame;
				yield return JetPack.Toolbox.WaitForEndOfFrame;

				_queueSlots = new HashSet<int>(_triggerSlots);

				List<GameObject> _objAccessories = ListObjAccessory(ChaControl);
				List<ParentRule> _rules = _rulesCache; //ListCoordinateRule();

				foreach (int _slotIndex in _triggerSlots)
				{
					GameObject _ca_slot = _objAccessories.FirstOrDefault(x => x.name == $"ca_slot{_slotIndex:00}");

					ChaFileAccessory.PartsInfo _part = _listPartsInfo.ElementAtOrDefault(_slotIndex);
					if (_part == null || _part.type == 120)
					{
						_logger.LogMessage($"Slot{_slotIndex + 1:00} is not assigned or not exist");
						_queueSlots.Remove(_slotIndex);
						if (_cfgRemoveUnassignedPart.Value)
							RemoveRule(_slotIndex);
						continue;
					}

					if (_ca_slot == null)
					{
						DebugMsg(LogLevel.Error, $"[ApplyParentRuleListCoroutine][Slot{_slotIndex + 1:00}] GameObject not found");
						_queueSlots.Remove(_slotIndex);
						continue;
					}

					if (_ca_slot.GetComponent(ChaAccessoryClothes) != null)
					{
						DebugMsg(LogLevel.Error, $"[ApplyParentRuleListCoroutine][Slot{_slotIndex + 1:00}] Cannot use this function on Accessory Clothes");
						_queueSlots.Remove(_slotIndex);
						continue;
					}

					ParentRule _rule = _rules.FirstOrDefault(x => x.Slot == _slotIndex);
					if (_rule == null)
					{
						DebugMsg(LogLevel.Error, $"[ApplyParentRuleListCoroutine][Slot{_slotIndex + 1:00}] rule not found");
						_queueSlots.Remove(_slotIndex);
						continue;
					}

					Transform _parentNode = null;
					if (_rule.ParentType == ParentType.Character)
						_parentNode = ChaControl.transform.Find(_rule.ParentPath);
					else if (_rule.ParentType == ParentType.Accessory)
					{
						GameObject _parentNodeGameObject = _objAccessories.FirstOrDefault(x => x.name == $"ca_slot{_rule.ParentSlot:00}");
						if (_parentNodeGameObject?.GetComponent(ChaAccessoryClothes) != null)
						{
							DebugMsg(LogLevel.Error, $"[ApplyParentRuleListCoroutine][Slot{_slotIndex + 1:00}] Cannot use this function on Accessory Clothes");
							_queueSlots.Remove(_slotIndex);
							continue;
						}
						_parentNode = _parentNodeGameObject?.transform?.Find(_rule.ParentPath);
					}
					else if (_rule.ParentType == ParentType.Hair)
						_parentNode = ChaControl.objHair.ElementAtOrDefault(_rule.ParentSlot)?.transform?.Find(_rule.ParentPath);

					if (_parentNode == null)
					{
						_logger.LogMessage($"Slot{_slotIndex + 1:00} parent node not found [{_rule.ParentType}][{_rule.ParentSlot}]");
						_logger.LogError($"[ApplyParentRuleListCoroutine][Slot{_slotIndex + 1:00}] parent node not found\n{JSONSerializer.Serialize(_rule.GetType(), _rule, true)}");
						_queueSlots.Remove(_slotIndex);
						continue;
					}

					if (_ca_slot.transform.parent == _parentNode)
					{
						_queueSlots.Remove(_slotIndex);
						continue;
					}

					_ca_slot.transform.SetParent(_parentNode, false);
					_ca_slot.transform.localPosition = Vector3.zero;
					_ca_slot.transform.localEulerAngles = Vector3.zero;
					_ca_slot.transform.localScale = Vector3.one;
					DebugMsg(LogLevel.Info, $"[ApplyParentRuleListCoroutine][Slot{_slotIndex + 1:00}][Parent: {_rule.ParentType} {_rule.ParentSlot}] moved");

					_queueSlots.Remove(_slotIndex);
				}

				Traverse.Create(GetBoneController(ChaControl)).Property("NeedsBaselineUpdate").SetValue(true);
			}

			internal void UpdatePartsInfoList()
			{
				_listPartsInfo = JetPack.Accessory.ListPartsInfo(ChaControl);
				_usedSlots.Clear();
				for (int i = 0; i < _listPartsInfo.Count; i++)
				{
					if (_listPartsInfo.ElementAtOrDefault(i)?.type > 120)
						_usedSlots.Add(i);
				}
			}

			internal Transform GetParentNodeGameObject(ParentRule _rule)
			{
				if (_rule == null) return null;

				Transform _parentNode = null;
				if (_rule.ParentType == ParentType.Character)
					_parentNode = ChaControl.transform.Find(_rule.ParentPath);
				else if (_rule.ParentType == ParentType.Accessory)
					_parentNode = GetObjAccessory(ChaControl, _rule.ParentSlot)?.transform?.Find(_rule.ParentPath);
				/*
				else if (_rule.ParentType == ParentType.Clothing)
					_parentNode = ChaControl.objClothes.ElementAtOrDefault(_rule.ParentSlot)?.transform?.Find(_rule.ParentPath);
				*/
				else if (_rule.ParentType == ParentType.Hair)
					_parentNode = ChaControl.objHair.ElementAtOrDefault(_rule.ParentSlot)?.transform?.Find(_rule.ParentPath);

				return _parentNode;
			}

			internal void CloneRule(int _srcSlotIndex, int _dstSlotIndex) => CloneRule(_srcSlotIndex, _dstSlotIndex, _currentCoordinateIndex, _currentCoordinateIndex);
			internal void CloneRule(int _srcSlotIndex, int _dstSlotIndex, int _coordinateIndex) => CloneRule(_srcSlotIndex, _dstSlotIndex, _coordinateIndex, _coordinateIndex);
			internal void CloneRule(int _srcSlotIndex, int _dstSlotIndex, int _srcCoordinateIndex, int _dstCoordinateIndex)
			{
				ParentRuleList.RemoveAll(x => x.Coordinate == _dstCoordinateIndex && x.Slot == _dstSlotIndex);
				ParentRule _rule = GetSlotRule(_srcCoordinateIndex, _srcSlotIndex).JsonClone<ParentRule>();
				if (_rule == null) return;
				_rule.Coordinate = _dstCoordinateIndex;
				_rule.Slot = _dstSlotIndex;
				ParentRuleList.Add(_rule);
			}

			internal void MoveRule(int _srcSlotIndex, int _dstSlotIndex) => MoveRule(_srcSlotIndex, _dstSlotIndex, _currentCoordinateIndex);
			internal void MoveRule(int _srcSlotIndex, int _dstSlotIndex, int _coordinateIndex)
			{
				ParentRuleList.RemoveAll(x => x.Coordinate == _coordinateIndex && x.Slot == _dstSlotIndex);
				ParentRule _rule = GetSlotRule(_coordinateIndex, _srcSlotIndex).JsonClone<ParentRule>();
				RemoveRule(_coordinateIndex, _srcSlotIndex);
				if (_rule != null)
				{
					_rule.Slot = _dstSlotIndex;
					ParentRuleList.Add(_rule);
				}
				foreach (ParentRule i in ParentRuleList.Where(x => x.Coordinate == _coordinateIndex && x.ParentType == ParentType.Accessory && x.ParentSlot == _srcSlotIndex).ToList())
					i.ParentSlot = _dstSlotIndex;
			}

			internal void RemoveRule(int _slotIndex) => RemoveRule(_currentCoordinateIndex, _slotIndex);
			internal void RemoveRule(int _coordinateIndex, int _slotIndex)
			{
				ParentRuleList.RemoveAll(x => x.Coordinate == _coordinateIndex && x.Slot == _slotIndex);
			}

			internal void ResetRules()
			{
				ParentRuleList.RemoveAll(x => x.Coordinate == _currentCoordinateIndex);
				RefreshCache();
			}

			internal void ExportRules()
			{
				List<ParentRule> _data = ListCoordinateRule().OrderBy(x => x.Slot).ToList().JsonClone<List<ParentRule>>();
				if (_data?.Count == 0)
				{
					_logger.LogMessage($"[ExportRules] no rule to export");
					return;
				}
				if (!Directory.Exists(_savePath))
					Directory.CreateDirectory(_savePath);
				string _filePath = Path.Combine(_savePath, "AAAPK.json");
				string _json = JSONSerializer.Serialize(typeof(List<ParentRule>), _data, true);
				File.WriteAllText(_filePath, _json);
				_logger.LogMessage($"[ExportRules] {_data?.Count} rule(s) exported to {_filePath}");
			}

			internal void ImportRules()
			{
				string _filePath = Path.Combine(_savePath, "AAAPK.json");
				if (!File.Exists(_filePath))
				{
					_logger.LogMessage($"[ImportRules] {_filePath} file doesn't exist");
					return;
				}
				List<ParentRule> _data = JSONSerializer.Deserialize<List<ParentRule>>(File.ReadAllText(_filePath));
				if (_data?.Count == 0)
				{
					_logger.LogMessage($"[ImportRules] no rule to import");
					return;
				}

				int _skipped = 0;
				foreach (ParentRule _rule in _data)
				{
					if (ParentRuleList.Any(x => x.Coordinate == _currentCoordinateIndex && x.Slot == _rule.Slot))
					{
						_skipped++;
						continue;
					}
					_rule.Coordinate = _currentCoordinateIndex;
					ParentRuleList.Add(_rule);
				}

				_logger.LogMessage($"[ImportRules] {_data?.Count - _skipped} rule(s) imported, {_skipped} rule(s) skipped");
				RefreshCache();
			}

			internal void RefreshCache()
			{
				_rulesCache = ListCoordinateRule().JsonClone<List<ParentRule>>();
			}
		}
	}
}
