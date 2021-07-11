using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;
using ChaCustom;

using HarmonyLib;

namespace AAAPK
{
	public partial class AAAPK
	{
		internal static partial class HooksMaker
		{
			[HarmonyPrefix, HarmonyPatch(typeof(CustomCoordinateFile), "CreateCoordinateFileBefore")]
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

			[HarmonyPostfix, HarmonyPatch(typeof(CustomCoordinateFile), "CreateCoordinateFileCoroutine")]
			private static void ChaCustom_CustomCoordinateFile_CreateCoordinateFileCoroutine_Postfix(string coordinateName)
			{
				if (_charaConfigWindow != null)
					_charaConfigWindow.enabled = false;

				AAAPKController _pluginCtrl = GetController(CustomBase.Instance.chaCtrl);
				if (_pluginCtrl == null) return;

				_pluginCtrl.StartCoroutine(_pluginCtrl.InitCurOutfitTriggerInfoHack("ChaCustom_CustomCoordinateFile_CreateCoordinateFileCoroutine_Postfix"));
			}

			[HarmonyPrefix, HarmonyPatch(typeof(ChaControl), "ChangeCoordinateType", new[] { typeof(ChaFileDefine.CoordinateType), typeof(bool) })]
			private static void ChaControl_ChangeCoordinateType_Prefix(ChaControl __instance)
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

			[HarmonyPostfix, HarmonyPatch(typeof(ChaControl), "ChangeCoordinateType", new[] { typeof(ChaFileDefine.CoordinateType), typeof(bool) })]
			private static void ChaControl_ChangeCoordinateType_Postfix(ChaControl __instance, ChaFileDefine.CoordinateType type)
			{
				_instance.StartCoroutine(ToggleButtonVisibility());
			}

			[HarmonyPrefix, HarmonyPatch(typeof(ChaControl), "ChangeCoordinateTypeAndReload", new[] { typeof(bool) })]
			private static void ChaControl_ChangeCoordinateTypeAndReload_Prefix(ChaControl __instance)
			{
				AAAPKController _pluginCtrl = GetController(__instance);
				if (_pluginCtrl == null) return;

				_pluginCtrl.UpdatePartsInfoList();

				if (_charaConfigWindow != null)
				{
					_charaConfigWindow.SetSelectedParent(null);
					_charaConfigWindow.SetSelectedBone(null);
					_charaConfigWindow._currentSlotRule = null;
					_charaConfigWindow._currentCoordinateIndex = -1;
					_charaConfigWindow._currentSlotIndex = -1;
					_charaConfigWindow.enabled = false;
				}

				_instance.StartCoroutine(ToggleButtonVisibility());
			}

			[HarmonyPostfix, HarmonyPatch(typeof(CvsAccessory), "UpdateSelectAccessoryType", new[] { typeof(int) })]
			private static void CvsAccessory_UpdateSelectAccessoryType_Postfix(CvsAccessory __instance, int index)
			{
				AAAPKController _pluginCtrl = GetController(CustomBase.Instance.chaCtrl);
				if (_pluginCtrl == null) return;

				_pluginCtrl.UpdatePartsInfoList();

				_instance.StartCoroutine(ToggleButtonVisibility());

				if (!_pluginCtrl._triggerSlots.Contains(__instance.SlotIndex())) return;

				if (index == 0)
				{
					if (_charaConfigWindow != null)
						_charaConfigWindow.enabled = false;
				}
				else
				{
					if (_charaConfigWindow != null && _charaConfigWindow.enabled)
						_charaConfigWindow.MoveObjectToPlace();
					else
						_pluginCtrl.InitCurOutfitTriggerInfo("CvsAccessory_UpdateSelectAccessoryKind_Postfix");
				}
			}

			[HarmonyPostfix, HarmonyPatch(typeof(CvsAccessory), "UpdateSelectAccessoryParent", new[] { typeof(int) })]
			private static void CvsAccessory_UpdateSelectAccessoryParent_Postfix(CvsAccessory __instance)
			{
				AAAPKController _pluginCtrl = GetController(CustomBase.Instance.chaCtrl);
				if (_pluginCtrl == null) return;

				if (!_pluginCtrl._triggerSlots.Contains(__instance.SlotIndex())) return;

				if (_charaConfigWindow != null && _charaConfigWindow.enabled)
					_charaConfigWindow.MoveObjectToPlace();
				else
					_pluginCtrl.InitCurOutfitTriggerInfo("CvsAccessory_UpdateSelectAccessoryKind_Postfix");
			}

			[HarmonyPostfix, HarmonyPatch(typeof(CvsAccessory), "UpdateSelectAccessoryKind", new[] { typeof(string), typeof(Sprite), typeof(int) })]
			private static void CvsAccessory_UpdateSelectAccessoryKind_Postfix(CvsAccessory __instance)
			{
				_instance.StartCoroutine(ToggleButtonVisibility());

				AAAPKController _pluginCtrl = GetController(CustomBase.Instance.chaCtrl);
				if (_pluginCtrl == null) return;

				if (!_pluginCtrl._triggerSlots.Contains(__instance.SlotIndex())) return;

				if (_charaConfigWindow != null && _charaConfigWindow.enabled)
					_charaConfigWindow.MoveObjectToPlace();
				else
					_pluginCtrl.InitCurOutfitTriggerInfo("CvsAccessory_UpdateSelectAccessoryKind_Postfix");
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
		}
	}
}
