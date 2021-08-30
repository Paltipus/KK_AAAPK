using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;
using ChaCustom;

using HarmonyLib;

using KKAPI.Maker;
using KKAPI.Maker.UI;

namespace AAAPK
{
	public partial class AAAPK
	{
		internal static partial class HooksMaker
		{
			[HarmonyPrefix, HarmonyPatch(typeof(CustomCoordinateFile), nameof(CustomCoordinateFile.CreateCoordinateFileBefore))]
			private static void ChaCustom_CustomCoordinateFile_CreateCoordinateFileBefore_Prefix()
			{
				AccGotHighRemoveEffect();

				if (_charaConfigWindow != null)
					_charaConfigWindow.enabled = false;

				ChaControl _chaCtrl = CustomBase.Instance.chaCtrl;
				AAAPKController _pluginCtrl = GetController(_chaCtrl);
				if (_pluginCtrl == null) return;

				_pluginCtrl.UpdatePartsInfoList();
				List<GameObject> _objAccessories = ListObjAccessory(_chaCtrl);
				foreach (int _slot in _pluginCtrl._triggerSlots)
				{
					GameObject _ca_slot = _objAccessories.FirstOrDefault(x => x.name == $"ca_slot{_slot:00}");
					if (_ca_slot == null) continue;

					ChaFileAccessory.PartsInfo _part = _pluginCtrl._listPartsInfo.ElementAtOrDefault(_slot);
					if (_part == null) continue;

					string _parentKey = _part.parentKey;
					GameObject _parentNode = _chaCtrl.GetReferenceInfo((ChaReference.RefObjKey) Enum.Parse(typeof(ChaReference.RefObjKey), _parentKey));
					_ca_slot.transform.SetParent(_parentNode.transform, false);
				}
			}

			[HarmonyPostfix, HarmonyPatch(typeof(CustomCoordinateFile), nameof(CustomCoordinateFile.CreateCoordinateFileCoroutine), new[] { typeof(string) })]
			private static void ChaCustom_CustomCoordinateFile_CreateCoordinateFileCoroutine_Postfix()
			{
				if (_charaConfigWindow != null)
					_charaConfigWindow.enabled = false;

				AAAPKController _pluginCtrl = GetController(CustomBase.Instance.chaCtrl);
				if (_pluginCtrl == null) return;

				_pluginCtrl.StartCoroutine(_pluginCtrl.ApplyParentRuleListHack("ChaCustom_CustomCoordinateFile_CreateCoordinateFileCoroutine_Postfix"));
			}

			[HarmonyPrefix, HarmonyPatch(typeof(ChaControl), nameof(ChaControl.ChangeCoordinateType), new[] { typeof(ChaFileDefine.CoordinateType), typeof(bool) })]
			private static void ChaControl_ChangeCoordinateType_Prefix()
			{
				if (_charaConfigWindow != null)
				{
					_charaConfigWindow.SetSelectedParent(null);
					_charaConfigWindow.SetSelectedBone(null);
					_charaConfigWindow._currentSlotRule = null;
					_charaConfigWindow._currentCoordinateIndex = -1;
					_charaConfigWindow._currentSlotIndex = -1;
					_charaConfigWindow._openedNodes.Clear();
					_charaConfigWindow.enabled = false;
				}
			}

			[HarmonyPostfix, HarmonyPatch(typeof(ChaControl), nameof(ChaControl.ChangeCoordinateType), new[] { typeof(ChaFileDefine.CoordinateType), typeof(bool) })]
			private static void ChaControl_ChangeCoordinateType_Postfix()
			{
				_instance.StartCoroutine(ToggleButtonVisibility());
			}

			[HarmonyPrefix, HarmonyPatch(typeof(ChaControl), "ChangeCoordinateTypeAndReload", new[] { typeof(bool) })]
			private static void ChaControl_ChangeCoordinateTypeAndReload_Prefix()
			{
				if (_charaConfigWindow != null)
				{
					_charaConfigWindow.SetSelectedParent(null);
					_charaConfigWindow.SetSelectedBone(null);
					_charaConfigWindow._currentSlotRule = null;
					_charaConfigWindow._currentCoordinateIndex = -1;
					_charaConfigWindow._currentSlotIndex = -1;
					_charaConfigWindow.enabled = false;
				}
			}

			[HarmonyPostfix, HarmonyPatch(typeof(CvsAccessory), nameof(CvsAccessory.UpdateSelectAccessoryType), new[] { typeof(int) })]
			private static void CvsAccessory_UpdateSelectAccessoryType_Postfix(CvsAccessory __instance, int index)
			{
				AAAPKController _pluginCtrl = GetController(CustomBase.Instance.chaCtrl);
				if (_pluginCtrl == null) return;

				_pluginCtrl.UpdatePartsInfoList();

				_instance.StartCoroutine(ToggleButtonVisibility());

				if (index == 0)
				{
					if (_charaConfigWindow != null)
					{
						_charaConfigWindow.enabled = false;
						if (_cfgRemoveUnassignedPart.Value)
							_pluginCtrl.RemoveRule(__instance.SlotIndex());
					}
				}
				else
				{
					if (_charaConfigWindow != null && _charaConfigWindow.enabled)
					{
						if (_pluginCtrl.ParentRuleList.Any(x => x.ParentType == ParentType.Accessory && x.ParentSlot == (__instance.SlotIndex())))
						{
							_pluginCtrl.ApplyParentRuleList("CvsAccessory_UpdateSelectAccessoryType_Postfix");
							return;
						}

						if (!_pluginCtrl._triggerSlots.Contains(__instance.SlotIndex()))
							_charaConfigWindow.MoveObjectToPlace();
					}
					else
						_pluginCtrl.ApplyParentRuleList("CvsAccessory_UpdateSelectAccessoryType_Postfix");
				}
			}

			[HarmonyPostfix, HarmonyPatch(typeof(CvsAccessory), nameof(CvsAccessory.UpdateSelectAccessoryParent), new[] { typeof(int) })]
			private static void CvsAccessory_UpdateSelectAccessoryParent_Postfix(CvsAccessory __instance)
			{
				AAAPKController _pluginCtrl = GetController(CustomBase.Instance.chaCtrl);
				if (_pluginCtrl == null) return;

				if (!_pluginCtrl._triggerSlots.Contains(__instance.SlotIndex())) return;

				if (_charaConfigWindow != null && _charaConfigWindow.enabled)
					_charaConfigWindow.MoveObjectToPlace();
				else
					_pluginCtrl.ApplyParentRuleList("CvsAccessory_UpdateSelectAccessoryParent_Postfix");
			}

			[HarmonyPriority(Priority.First)]
			[HarmonyPrefix, HarmonyPatch(typeof(CvsAccessory), nameof(CvsAccessory.UpdateSelectAccessoryKind), new[] { typeof(string), typeof(Sprite), typeof(int) })]
			private static void CvsAccessory_UpdateSelectAccessoryKind_Prefix(CvsAccessory __instance)
			{
				ChaControl _chaCtrl = CustomBase.Instance.chaCtrl;
				AAAPKController _pluginCtrl = GetController(_chaCtrl);
				if (_pluginCtrl == null) return;

				if (_pluginCtrl._duringLoadChange) return;

				if (_pluginCtrl.ParentRuleList.Any(x => x.ParentType == ParentType.Accessory && x.ParentSlot == __instance.SlotIndex()))
				{
					List<GameObject> _objAccessories = ListObjAccessory(_chaCtrl);
					foreach (int _slot in _pluginCtrl.ParentRuleList.Where(x => x.ParentType == ParentType.Accessory && x.ParentSlot == __instance.SlotIndex()).Select(x => x.Slot).ToList())
					{
						GameObject _ca_slot = _objAccessories.FirstOrDefault(x => x.name == $"ca_slot{_slot:00}");

						if (_ca_slot == null) continue;

						ChaFileAccessory.PartsInfo _part = _pluginCtrl._listPartsInfo.ElementAtOrDefault(_slot);
						if (_part == null) continue;

						string _parentKey = _part.parentKey;
						GameObject _parentNode = _chaCtrl.GetReferenceInfo((ChaReference.RefObjKey) Enum.Parse(typeof(ChaReference.RefObjKey), _parentKey));
						_ca_slot.transform.SetParent(_parentNode.transform, false);
					}
				}
			}

			[HarmonyPriority(Priority.Last)]
			[HarmonyPostfix, HarmonyPatch(typeof(CvsAccessory), nameof(CvsAccessory.UpdateSelectAccessoryKind), new[] { typeof(string), typeof(Sprite), typeof(int) })]
			private static void CvsAccessory_UpdateSelectAccessoryKind_Postfix(CvsAccessory __instance)
			{
				ChaControl _chaCtrl = CustomBase.Instance.chaCtrl;
				AAAPKController _pluginCtrl = GetController(_chaCtrl);
				if (_pluginCtrl == null) return;

				_pluginCtrl.ApplyParentRuleList("CvsAccessory_UpdateSelectAccessoryKind_Postfix");
			}

			internal static void MakerHandler_Coordinate_Load_Prefix()
			{
				if (_charaConfigWindow != null)
				{
					_charaConfigWindow.SetSelectedParent(null);
					_charaConfigWindow.SetSelectedBone(null);
					_charaConfigWindow._currentSlotRule = null;
					_charaConfigWindow._currentCoordinateIndex = -1;
					_charaConfigWindow._currentSlotIndex = -1;
					_charaConfigWindow._openedNodes.Clear();
					_charaConfigWindow.enabled = false;
				}
			}

			internal static bool UI_ToggleButtonVisibility_Prefix()
			{
				if (!MakerAPI.InsideMaker || CustomBase.Instance?.chaCtrl == null)
					return true;

				MakerButton DynamicBoneEditorButton = Traverse.Create(DynamicBoneEditorUI).Field("DynamicBoneEditorButton").GetValue<MakerButton>();

				if (DynamicBoneEditorButton == null)
					return true;

				GameObject _ca_slot = GetObjAccessory(CustomBase.Instance.chaCtrl, AccessoriesApi.SelectedMakerAccSlot);
				if (_ca_slot == null)
					return true;

				List<DynamicBone> _result = _ca_slot.GetComponentsInChildren<DynamicBone>(true)?.Where(x => x.m_Root != null).ToList();
				if (_result?.Count > 0)
				{
					foreach (GameObject _gameObject in ListObjAccessory(_ca_slot))
					{
						if (_gameObject == _ca_slot) continue;
						List<DynamicBone> _remove = _gameObject.GetComponentsInChildren<DynamicBone>(true)?.Where(x => x.m_Root != null).ToList();

						if (_remove?.Count > 0)
							_result.RemoveAll(x => _remove.Contains(x));
					}
					if (_result?.Count > 0)
						_result.RemoveAll(x => x.m_Root.name.StartsWith("cf_j_sk_") || x.m_Root.name.StartsWith("cf_d_sk_"));
				}

				DynamicBoneEditorButton.Visible.OnNext(_result?.Count > 0);

				return false;
			}
		}
	}
}
