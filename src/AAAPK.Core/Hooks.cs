using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

using UnityEngine;

using HarmonyLib;

namespace AAAPK
{
	public partial class AAAPK
	{
		internal static partial class Hooks
		{
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
				List<DynamicBone> _result = _getFromStack == null ? new List<DynamicBone>() : _getFromStack.Where(x => x.m_Root != null).ToList();

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
				if (_result?.Count > 0)
					_result.RemoveAll(x => x.m_Root.name.StartsWith("cf_j_sk_") || x.m_Root.name.StartsWith("cf_d_sk_"));

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
					if (_result?.Count > 0)
						_result.RemoveAll(x => x.m_Root.name.StartsWith("cf_j_sk_") || x.m_Root.name.StartsWith("cf_d_sk_"));
				}

				return _result;
			}
		}
	}
}
