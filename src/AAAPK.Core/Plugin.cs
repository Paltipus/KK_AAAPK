using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;

using UnityEngine;
using ChaCustom;

using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

using ExtensibleSaveFormat;

using KKABMX.Core;

using KKAPI;
using KKAPI.Chara;
using KKAPI.Maker;
using KKAPI.Maker.UI;
using KKAPI.Utilities;

namespace AAAPK
{
	[BepInPlugin(GUID, Name, Version)]
	[BepInDependency(KoikatuAPI.GUID, KoikatuAPI.VersionConst)]
	[BepInDependency(ExtendedSave.GUID, ExtendedSave.Version)]
	[BepInDependency("madevil.JetPack", JetPack.Core.Version)]
	[BepInDependency("KKABMX.Core", KKABMX_Core.Version)]
	[BepInDependency("com.deathweasel.bepinex.accessoryclothes")]
#if KK
	[BepInDependency("com.joan6694.illusionplugins.moreaccessories", "1.1.0")]
#endif
	public partial class AAAPK : BaseUnityPlugin
	{
		public const string GUID = "madevil.kk.AAAPK";
		public const string Name = "AAAPK";
		public const string Version = "1.3.0.0";

		internal static ManualLogSource _logger;
		internal static Harmony _hooksMaker;
		internal static AAAPK _instance;
		internal static AAAPKUI _charaConfigWindow;

		internal static ConfigEntry<bool> _cfgDebugMode;
		internal static ConfigEntry<bool> _cfgDragPass;
		internal static ConfigEntry<float> _cfgMakerWinX;
		internal static ConfigEntry<float> _cfgMakerWinY;
		internal static ConfigEntry<bool> _cfgMakerWinResScale;
		internal static ConfigEntry<bool> _cfgRemoveUnassignedPart;

		internal static MakerButton _accWinCtrlEnable;

		private static readonly string _savePath = Path.Combine(Paths.GameRootPath, "Temp");
		public const int ExtDataVer = 1;
		public const string ExtDataKey = "madevil.kk.AAAPK";
		internal static Dictionary<string, Type> _typeList = new Dictionary<string, Type>();
		internal static Type ChaAccessoryClothes = null;
		internal static Type DynamicBoneEditorUI = null;

		private void Awake()
		{
			_logger = base.Logger;
			_instance = this;

			_cfgDebugMode = Config.Bind("Debug", "Debug Mode", false, new ConfigDescription("", null, new ConfigurationManagerAttributes { IsAdvanced = true, Order = 20 }));

			_cfgRemoveUnassignedPart = Config.Bind("Maker", "Remove Unassigned Part", true, new ConfigDescription("Remove rules for missing or unassigned accesseries", null, new ConfigurationManagerAttributes { Order = 20 }));

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
			CharacterApi.RegisterExtraBehaviour<AAAPKController>(ExtDataKey);
			Harmony _hooksInstance = Harmony.CreateAndPatchAll(typeof(Hooks), GUID);
#if KKS
			InitCardImport();
#endif
			if (JetPack.Game.HasDarkness)
			{
				_hooksInstance.Patch(Type.GetType("ChaControl, Assembly-CSharp").GetMethod("ChangeShakeAccessory", AccessTools.all, null, new[] { typeof(int) }, null), prefix: new HarmonyMethod(typeof(Hooks), nameof(Hooks.ChaControl_ChangeShakeAccessory_Prefix)));

				if (JetPack.MoreAccessories.Installed)
				{
					_hooksInstance.Patch(JetPack.MoreAccessories.Instance.GetType().Assembly.GetType("MoreAccessoriesKOI.ChaControl_ChangeShakeAccessory_Patches").GetMethod("Prefix", AccessTools.all), prefix: new HarmonyMethod(typeof(Hooks), nameof(Hooks.ReturnFalse)));
				}
			}

			{
				BaseUnityPlugin _instance = JetPack.Toolbox.GetPluginInstance("madevil.kk.MovUrAcc");
				if (_instance != null && !JetPack.Toolbox.PluginVersionCompare(_instance, "1.9.0.0"))
					_logger.LogError($"MovUrAcc 1.9+ is required to work properly, version {_instance.Info.Metadata.Version} detected");
			}

			{
				BaseUnityPlugin _instance = JetPack.Toolbox.GetPluginInstance("madevil.kk.ca");
				if (_instance != null && !JetPack.Toolbox.PluginVersionCompare(_instance, "1.4.0.0"))
					_logger.LogError($"Character Accessory 1.4+ is required to work properly, version {_instance.Info.Metadata.Version} detected");
			}

			{
				BaseUnityPlugin _instance = JetPack.Toolbox.GetPluginInstance("com.deathweasel.bepinex.materialeditor");
				Type MaterialAPI = _instance.GetType().Assembly.GetType("MaterialEditorAPI.MaterialAPI");
				_hooksInstance.Patch(MaterialAPI.GetMethod("GetRendererList", AccessTools.all, null, new[] { typeof(GameObject) }, null), postfix: new HarmonyMethod(typeof(Hooks), nameof(Hooks.MaterialAPI_GetRendererList_Postfix)));
			}

			{
				BaseUnityPlugin _instance = JetPack.Toolbox.GetPluginInstance("com.deathweasel.bepinex.dynamicboneeditor");
				if (_instance != null)
				{
					DynamicBoneEditorUI = _instance.GetType().Assembly.GetType("KK_Plugins.DynamicBoneEditor.UI");
					_hooksInstance.Patch(DynamicBoneEditorUI.GetMethod("ShowUI", AccessTools.all, null, new[] { typeof(int) }, null), transpiler: new HarmonyMethod(typeof(Hooks), nameof(Hooks.UI_ShowUI_Transpiler)));
					_hooksInstance.Patch(DynamicBoneEditorUI.GetMethod("ToggleButtonVisibility", AccessTools.all), prefix: new HarmonyMethod(typeof(HooksMaker), nameof(HooksMaker.UI_ToggleButtonVisibility_Prefix)));

					Type CharaController = _instance.GetType().Assembly.GetType("KK_Plugins.DynamicBoneEditor.CharaController");
					//_hooksInstance.Patch(AccessTools.Method(CharaController.GetNestedType("<ApplyData>d__12", System.Reflection.BindingFlags.NonPublic), "MoveNext"), transpiler: new HarmonyMethod(typeof(Hooks), nameof(Hooks.CharaController_ApplyData_Transpiler)));
					_hooksInstance.Patch(AccessTools.Method(AccessTools.Inner(CharaController, "<ApplyData>d__12"), "MoveNext"), transpiler: new HarmonyMethod(typeof(Hooks), nameof(Hooks.CharaController_ApplyData_Transpiler)));
				}
			}

			{
				BaseUnityPlugin _instance = JetPack.Toolbox.GetPluginInstance("com.deathweasel.bepinex.accessoryclothes");
				ChaAccessoryClothes = _instance.GetType().Assembly.GetType("KK_Plugins.ChaAccessoryClothes");
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

			JetPack.CharaMaker.OnMakerExiting += (_sender, _args) =>
			{
				_hooksMaker.UnpatchAll(_hooksMaker.Id);
				_hooksMaker = null;
				Destroy(_charaConfigWindow);
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
					if (_pluginCtrl.GetSlotRule(_slotIndex) != null)
						_pluginCtrl.ApplyParentRuleList("OnCvsNavMenuClick");
				}
				else
				{
					_charaConfigWindow._onAccTab = false;
					_charaConfigWindow.enabled = false;
				}
			};

			JetPack.Chara.OnChangeCoordinateType += (_sender, _args) => OnChangeCoordinateType(_args);

			JetPack.MaterialEditor.OnDataApply += (_sender, _args) => OnDataApply(_args);
		}

		internal static IEnumerator ToggleButtonVisibility()
		{
			yield return JetPack.Toolbox.WaitForEndOfFrame;
			yield return JetPack.Toolbox.WaitForEndOfFrame;

			ChaFileAccessory.PartsInfo _part = JetPack.Accessory.GetPartsInfo(CustomBase.Instance.chaCtrl, JetPack.CharaMaker.CurrentAccssoryIndex);
			_accWinCtrlEnable.Visible.OnNext(_part?.type != 120);
		}

		internal static GameObject GetObjAccessory(ChaControl _chaCtrl, int _slotIndex) => JetPack.Accessory.GetObjAccessory(_chaCtrl, _slotIndex);

		internal static List<GameObject> ListObjAccessory(ChaControl _chaCtrl) => JetPack.Accessory.ListObjAccessory(_chaCtrl);

		internal static List<GameObject> ListObjAccessory(GameObject _gameObject) => JetPack.Accessory.ListObjAccessory(_gameObject);

		internal static void AccGotHighRemoveEffect()
		{
			if (!_typeList.ContainsKey("AccGotHigh"))
				return;
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
