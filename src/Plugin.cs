using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
//using System.Reflection;

using UnityEngine;
using ChaCustom;

using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

using KKAPI.Chara;
using KKAPI.Maker;
using KKAPI.Maker.UI;
using KKAPI.Utilities;

namespace AAAPK
{
	[BepInPlugin(GUID, Name, Version)]
	[BepInDependency("madevil.JetPack")]
	[BepInDependency("marco.kkapi", "1.17")]
	[BepInDependency("KKABMX.Core")]
	[BepInDependency("com.deathweasel.bepinex.accessoryclothes")]
	[BepInDependency("com.joan6694.illusionplugins.moreaccessories", "1.0.9")]
	public partial class AAAPK : BaseUnityPlugin
	{
		public const string GUID = "madevil.kk.AAAPK";
		public const string Name = "Additional Accessory Advanced Parent Knockoff";
		public const string Version = "1.0.2.0";

		internal static ManualLogSource _logger;
		internal static Harmony _hooksMaker;
		internal static AAAPK _instance;
		internal static AAAPKUI _charaConfigWindow;

		internal static ConfigEntry<bool> _cfgDebugMode;
		internal static ConfigEntry<bool> _cfgDragPass;
		internal static ConfigEntry<float> _cfgMakerWinX;
		internal static ConfigEntry<float> _cfgMakerWinY;
		internal static ConfigEntry<bool> _cfgMakerWinResScale;

		internal static MakerButton _accWinCtrlEnable;

		private static string _savePath = Path.Combine(Paths.GameRootPath, "Temp");
		public const int ExtDataVer = 1;
		internal static Dictionary<string, Type> _typeList = new Dictionary<string, Type>();
		internal static Type ChaAccessoryClothes = null;

		private void Awake()
		{
			_logger = base.Logger;
			_instance = this;

			_cfgDebugMode = Config.Bind("Debug", "Debug Mode", false, new ConfigDescription("", null, new ConfigurationManagerAttributes { IsAdvanced = true, Order = 20 }));

			_cfgDragPass = Config.Bind("Maker", "Drag Pass Mode", false, new ConfigDescription("Setting window will not block mouse dragging", null, new ConfigurationManagerAttributes { Order = 15 }));

			_cfgMakerWinX = Config.Bind("Maker", "Config Window Startup X", 525f, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 19 }));
			_cfgMakerWinX.SettingChanged += (_sender, _args) =>
			{
				if (_charaConfigWindow == null) return;
				if (_charaConfigWindow._windowPos.x != _cfgMakerWinX.Value)
				{
					_charaConfigWindow._windowPos.x = _cfgMakerWinX.Value;
				}
			};
			_cfgMakerWinY = Config.Bind("Maker", "Config Window Startup Y", 80f, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 18 }));
			_cfgMakerWinY.SettingChanged += (_sender, _args) =>
			{
				if (_charaConfigWindow == null) return;
				if (_charaConfigWindow._windowPos.y != _cfgMakerWinY.Value)
				{
					_charaConfigWindow._windowPos.y = _cfgMakerWinY.Value;
				}
			};
			_cfgMakerWinResScale = Config.Bind("Maker", "Config Window Resolution Adjust", false, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 17 }));
			_cfgMakerWinResScale.SettingChanged += (_sender, _args) =>
			{
				if (_charaConfigWindow == null) return;
				_charaConfigWindow.ChangeRes();
			};
		}

		private void Start()
		{
			CharacterApi.RegisterExtraBehaviour<AAAPKController>(GUID);
			Harmony _hooksInstance = Harmony.CreateAndPatchAll(typeof(Hooks));

			{
				BaseUnityPlugin _instance = JetPack.Toolbox.GetPluginInstance("madevil.kk.MovUrAcc");
				if (_instance != null && !JetPack.Toolbox.PluginVersionCompare(_instance, "1.9.0.0"))
					_logger.LogError($"MovUrAcc 1.9+ is required to work properly, version {_instance.Info.Metadata.Version} detected");
			}

			{
				BaseUnityPlugin _instance = JetPack.Toolbox.GetPluginInstance("madevil.kk.ca");
				if (_instance != null && !JetPack.Toolbox.PluginVersionCompare(_instance, "1.3.0.0"))
					_logger.LogError($"Character Accessory 1.3+ is required to work properly, version {_instance.Info.Metadata.Version} detected");
			}

			{
				BaseUnityPlugin _instance = JetPack.Toolbox.GetPluginInstance("com.deathweasel.bepinex.materialeditor");
				Type MaterialEditorCharaController = _instance.GetType().Assembly.GetType("KK_Plugins.MaterialEditor.MaterialEditorCharaController");
				_hooksInstance.Patch(MaterialEditorCharaController.GetMethod("CorrectTongue", AccessTools.all), postfix: new HarmonyMethod(typeof(Hooks), nameof(Hooks.MaterialEditorCharaController_CorrectTongue_Postfix)));
				Type MaterialAPI = _instance.GetType().Assembly.GetType("MaterialEditorAPI.MaterialAPI");
				_hooksInstance.Patch(MaterialAPI.GetMethod("GetRendererList", AccessTools.all, null, new[] { typeof(GameObject) }, null), postfix: new HarmonyMethod(typeof(Hooks), nameof(Hooks.MaterialAPI_GetRendererList_Postfix)));
			}

			{
				BaseUnityPlugin _instance = JetPack.Toolbox.GetPluginInstance("com.deathweasel.bepinex.dynamicboneeditor");
				if (_instance != null)
				{
					Type UI = _instance.GetType().Assembly.GetType("KK_Plugins.DynamicBoneEditor.UI");
					_hooksInstance.Patch(UI.GetMethod("ShowUI", AccessTools.all, null, new[] { typeof(int) }, null), transpiler: new HarmonyMethod(typeof(Hooks), nameof(Hooks.UI_ShowUI_Transpiler)));

					Type CharaController = _instance.GetType().Assembly.GetType("KK_Plugins.DynamicBoneEditor.CharaController");
					//_hooksInstance.Patch(AccessTools.Method(CharaController.GetNestedType("<ApplyData>d__12", BindingFlags.NonPublic), "MoveNext"), transpiler: new HarmonyMethod(typeof(Hooks), nameof(Hooks.CharaController_ApplyData_Transpiler)));
					_hooksInstance.Patch(AccessTools.Method(AccessTools.Inner(CharaController, "<ApplyData>d__12"), "MoveNext"), transpiler: new HarmonyMethod(typeof(Hooks), nameof(Hooks.CharaController_ApplyData_Transpiler)));
				}
			}

			{
				BaseUnityPlugin _instance = JetPack.Toolbox.GetPluginInstance("com.deathweasel.bepinex.accessoryclothes");
				ChaAccessoryClothes = _instance.GetType().Assembly.GetType("KK_Plugins.ChaAccessoryClothes");
			}

			{
				BaseUnityPlugin _instance = JetPack.Toolbox.GetPluginInstance("KKABMX.Core");
				_typeList["BoneController"] = _instance.GetType().Assembly.GetType("KKABMX.Core.BoneController");
			}

			{
				BaseUnityPlugin _instance = JetPack.Toolbox.GetPluginInstance("keelhauled.draganddrop");
				if (_instance != null)
				{
					Type MakerHandler = _instance.GetType().Assembly.GetType("DragAndDrop.MakerHandler");
					_hooksInstance.Patch(MakerHandler.GetMethod("Coordinate_Load", AccessTools.all), prefix: new HarmonyMethod(typeof(HooksMaker), nameof(HooksMaker.MakerHandler_Coordinate_Load_Prefix)));
				}
			}

			{
				BaseUnityPlugin _instance = JetPack.Toolbox.GetPluginInstance("madevil.kk.AccGotHigh");
				if (_instance != null)
					_typeList["AccGotHigh"] = _instance.GetType();
			}

			AccessoriesApi.AccessoryTransferred += (_sender, _args) => GetController(CustomBase.Instance.chaCtrl).AccessoryTransferredHandler(_args.SourceSlotIndex, _args.DestinationSlotIndex);
			AccessoriesApi.AccessoriesCopied += (_sender, _args) => GetController(CustomBase.Instance.chaCtrl).AccessoriesCopiedHandler((int) _args.CopySource, (int) _args.CopyDestination, _args.CopiedSlotIndexes.ToList());

			MakerAPI.RegisterCustomSubCategories += (_sender, _args) =>
			{
				_charaConfigWindow = _instance.gameObject.AddComponent<AAAPKUI>();

				_hooksMaker = Harmony.CreateAndPatchAll(typeof(HooksMaker));

				MakerCategory _category = new MakerCategory("05_ParameterTop", "tglAAAPK", MakerConstants.Parameter.Attribute.Position + 1, "AAAPK");
				_args.AddSubCategory(_category);

				_args.AddControl(new MakerText("OutfitTriggers", _category, this));

				_args.AddControl(new MakerButton("Export", _category, this)).OnClick.AddListener(delegate { GetController(CustomBase.Instance.chaCtrl).ExportRules(); });
				_args.AddControl(new MakerButton("Import", _category, this)).OnClick.AddListener(delegate { GetController(CustomBase.Instance.chaCtrl).ImportRules(); });
				_args.AddControl(new MakerButton("Reset", _category, this)).OnClick.AddListener(delegate { GetController(CustomBase.Instance.chaCtrl).ResetRules(); });

				_accWinCtrlEnable = MakerAPI.AddAccessoryWindowControl(new MakerButton("AAAPK", null, _instance));
				_accWinCtrlEnable.OnClick.AddListener(() => _charaConfigWindow.enabled = true);
			};

			MakerAPI.MakerExiting += (_sender, _args) =>
			{
				_hooksMaker.UnpatchAll(_hooksMaker.Id);
				_hooksMaker = null;
				Destroy(_charaConfigWindow);
			};

			JetPack.CharaMaker.OnAccessoryKindChanged += (_sender, _args) =>
			{
				_instance.StartCoroutine(ToggleButtonVisibility());

				AAAPKController _pluginCtrl = GetController(CustomBase.Instance.chaCtrl);
				if (_pluginCtrl == null) return;

				_pluginCtrl.UpdatePartsInfoList();

				if (_pluginCtrl.ParentRules.Any(x => x.ParentSlot == _args.SlotIndex))
				{
					_pluginCtrl.InitCurOutfitTriggerInfo("OnAccessoryKindChanged");
					return;
				}

				if (_charaConfigWindow != null && _charaConfigWindow.enabled)
				{
					if (!_pluginCtrl._triggerSlots.Contains(_args.SlotIndex))
						_charaConfigWindow.MoveObjectToPlace();
				}
				else
					_pluginCtrl.InitCurOutfitTriggerInfo("OnAccessoryKindChanged");
			};

			JetPack.CharaMaker.OnCvsNavMenuClick += (_sender, _args) =>
			{
				AAAPKController _pluginCtrl = GetController(CustomBase.Instance.chaCtrl);

				if (_args.TopIndex == 4)
				{
					if (_args.SideToggle?.GetComponentInChildren<CvsAccessory>(true) == null)
					{
						_charaConfigWindow.enabled = false;
						return;
					}

					int _slotIndex = _args.SideToggle.GetComponentInChildren<CvsAccessory>(true).SlotIndex();
					_charaConfigWindow._onAccTab = true;
					StartCoroutine(ToggleButtonVisibility());
				}
				else
				{
					_charaConfigWindow._onAccTab = false;
					_charaConfigWindow.enabled = false;
				}
			};
		}

		internal static IEnumerator ToggleButtonVisibility()
		{
			yield return JetPack.Toolbox.WaitForEndOfFrame;
			yield return JetPack.Toolbox.WaitForEndOfFrame;

			ChaFileAccessory.PartsInfo _part = JetPack.Accessory.GetPartsInfo(CustomBase.Instance.chaCtrl, JetPack.CharaMaker.CurrentAccssoryIndex);
			_accWinCtrlEnable.Visible.OnNext(_part?.type > 120);
		}

		internal static GameObject GetObjAccessory(ChaControl _chaCtrl, int _slotIndex)
		{
			return _chaCtrl.GetComponentsInChildren<ListInfoComponent>(true)?.FirstOrDefault(x => x != null && x.gameObject != null && x.gameObject.name == $"ca_slot{_slotIndex:00}")?.gameObject;
		}

		internal static List<GameObject> ListObjAccessory(ChaControl _chaCtrl)
		{
			return _chaCtrl.GetComponentsInChildren<ListInfoComponent>(true)?.Where(x => x != null && x.gameObject != null && x.gameObject.name.StartsWith("ca_slot")).Select(x => x.gameObject).OrderBy(x => x.name).ToList() ?? new List<GameObject>();
		}

		internal static List<GameObject> ListObjAccessory(GameObject _gameObject)
		{
			return _gameObject?.GetComponentsInChildren<ListInfoComponent>(true)?.Where(x => x != null && x.gameObject != null && x.gameObject.name.StartsWith("ca_slot")).Select(x => x.gameObject).ToList() ?? new List<GameObject>();
		}

		internal static void AccGotHighRemoveEffect()
		{
			if (!_typeList.ContainsKey("AccGotHigh")) return;
			Traverse.Create(_typeList["AccGotHigh"]).Method("RemoveEffect").GetValue();
		}

		internal static void DebugMsg(LogLevel _level, string _meg)
		{
			if (_cfgDebugMode.Value)
				_logger.Log(_level, _meg);
			else
				_logger.Log(LogLevel.Debug, _meg);
		}
	}
}
