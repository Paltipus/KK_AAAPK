using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

using UnityEngine;

using HarmonyLib;

using KKAPI.Chara;

namespace AAAPK
{
	public partial class AAAPK
	{
		internal static partial class Hooks
		{
			[HarmonyPrefix, HarmonyPatch(typeof(ChaControl), nameof(ChaControl.ChangeCoordinateType), new[] { typeof(ChaFileDefine.CoordinateType), typeof(bool) })]
			private static void ChaControl_ChangeCoordinateType_Prefix(ChaControl __instance)
			{
				AAAPKController _pluginCtrl = GetController(__instance);
				if (_pluginCtrl == null) return;

				_pluginCtrl._triggerSlots.Clear();
				_pluginCtrl._queueSlots.Clear();
				_pluginCtrl._rulesCache.Clear();
			}

			[HarmonyPostfix, HarmonyPatch(typeof(ChaControl), nameof(ChaControl.ChangeCoordinateType), new[] { typeof(ChaFileDefine.CoordinateType), typeof(bool) })]
			private static void ChaControl_ChangeCoordinateType_Postfix(ChaControl __instance)
			{
				AAAPKController _pluginCtrl = GetController(__instance);
				if (_pluginCtrl == null) return;

				_pluginCtrl.UpdatePartsInfoList();
				_pluginCtrl.RefreshCache();
				_pluginCtrl.ApplyParentRuleList("ChangeCoordinateType");
			}

			internal static void MaterialEditorCharaController_CorrectTongue_Postfix(CharaCustomFunctionController __instance)
			{
				AAAPKController _pluginCtrl = GetController(__instance.ChaControl);
				if (_pluginCtrl == null) return;

				_pluginCtrl._duringLoad = false;
				_pluginCtrl.ApplyParentRuleList("MaterialEditorCharaController_CorrectTongue_Postfix");
			}

			internal static void MaterialAPI_GetRendererList_Postfix(ref IEnumerable<Renderer> __result, GameObject gameObject)
			{
				if (gameObject == null)
					return;

				List<Renderer> _filter = __result.ToList();
				foreach (GameObject _gameObject in ListObjAccessory(gameObject))
				{
					if (_gameObject == gameObject)
						continue;

					List<Renderer> _remove = _gameObject.GetComponentsInChildren<Renderer>(true)?.ToList();
					if (_remove?.Count > 0)
						_filter.RemoveAll(x => _remove.Contains(x));
				}

				__result = _filter.AsEnumerable();
			}

			internal static IEnumerable<CodeInstruction> CharaController_ApplyData_Transpiler(IEnumerable<CodeInstruction> _instructions)
			{
				MethodInfo _getComponentsInChildrenMethod = AccessTools.Method(typeof(GameObject), nameof(GameObject.GetComponentsInChildren), generics: new Type[] { typeof(DynamicBone) });
				if (_getComponentsInChildrenMethod == null)
				{
					_logger.LogError("Failed to get methodinfo for UnityEngine.GameObject.GetComponentsInChildren<DynamicBone>, CharaController_ApplyData_Transpiler will not patch");
					return _instructions;
				}

				CodeMatcher _codeMatcher = new CodeMatcher(_instructions)
					.MatchForward(useEnd: false,
						new CodeMatch(OpCodes.Callvirt, operand: _getComponentsInChildrenMethod),
						new CodeMatch(OpCodes.Stloc_S))
					.Advance(1)
					.InsertAndAdvance(new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(Hooks), nameof(CharaController_ApplyData_Method))));

				_codeMatcher.ReportFailure(MethodBase.GetCurrentMethod(), error => _logger.LogError(error));
#if DEBUG
				System.IO.File.WriteAllLines($"{nameof(CharaController_ApplyData_Transpiler)}.txt", _codeMatcher.Instructions().Select(x => x.ToString()).ToArray());
#endif
				return _codeMatcher.Instructions();
			}

			internal static DynamicBone[] CharaController_ApplyData_Method(DynamicBone[] _getFromStack)
			{
				List<DynamicBone> _result = _getFromStack?.ToList() ?? new List<DynamicBone>();

				if (_result?.Count == 0)
					return _result.ToArray();

				ChaControl _chaCtrl = _result[0].GetComponentsInParent<ChaControl>(true)?.FirstOrDefault();
				GameObject _ca_slot = _result[0].GetComponentsInParent<ListInfoComponent>(true)?.FirstOrDefault().gameObject;

				foreach (GameObject _gameObject in ListObjAccessory(_ca_slot))
				{
					if (_gameObject == _ca_slot) continue;
					List<DynamicBone> _remove = _gameObject.GetComponentsInChildren<DynamicBone>(true)?.Where(x => x.m_Root != null).ToList();

					if (_remove?.Count > 0)
						_result.RemoveAll(x => _remove.Contains(x));
				}

				return _result.ToArray();
			}

			internal static IEnumerable<CodeInstruction> UI_ShowUI_Transpiler(IEnumerable<CodeInstruction> _instructions)
			{
				MethodInfo _toListMethod = AccessTools.Method(typeof(Enumerable), nameof(Enumerable.ToList), generics: new Type[] { typeof(DynamicBone) });
				if (_toListMethod == null)
				{
					_logger.LogError("Failed to get methodinfo for System.Linq.Enumerable.ToList<DynamicBone>, UI_ShowUI_Transpiler will not patch");
					return _instructions;
				}

				CodeMatcher _codeMatcher = new CodeMatcher(_instructions)
					.MatchForward(useEnd: false,
						new CodeMatch(OpCodes.Call, operand: _toListMethod),
						new CodeMatch(OpCodes.Stloc_2),
						new CodeMatch(OpCodes.Ldloc_2))
					.Advance(1)
					.InsertAndAdvance(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Hooks), nameof(UI_ShowUI_Method))));

				_codeMatcher.ReportFailure(MethodBase.GetCurrentMethod(), error => _logger.LogError(error));
#if DEBUG
				System.IO.File.WriteAllLines($"{nameof(UI_ShowUI_Transpiler)}.txt", _codeMatcher.Instructions().Select(x => x.ToString()).ToArray());
#endif
				return _codeMatcher.Instructions();
			}

			internal static List<DynamicBone> UI_ShowUI_Method(List<DynamicBone> _getFromStack)
			{
				List<DynamicBone> _result = _getFromStack.ToList();

				if (_result.Count == 0)
					return _getFromStack;

				ChaControl _chaCtrl = _result[0].GetComponentsInParent<ChaControl>(true)?.FirstOrDefault();
				GameObject _ca_slot = _result[0].GetComponentsInParent<ListInfoComponent>(true)?.FirstOrDefault().gameObject;

				if (_result.Count > 0)
				{
					foreach (GameObject _gameObject in ListObjAccessory(_ca_slot))
					{
						if (_gameObject == _ca_slot) continue;
						List<DynamicBone> _remove = _gameObject.GetComponentsInChildren<DynamicBone>(true)?.Where(x => x.m_Root != null).ToList();

						if (_remove?.Count > 0)
							_result.RemoveAll(x => _remove.Contains(x));
					}
				}

				return _result;
			}
		}
	}
}
