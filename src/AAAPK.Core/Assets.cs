using System;
using System.Linq;

using UnityEngine;
using UniRx;

using BepInEx.Logging;

using KKAPI.Utilities;

namespace AAAPK
{
	public partial class AAAPK
	{
		internal static GameObject _assetSphere = null;
		internal static Material _assetBonelyfans = null;

		internal static void LoadAsset_sphere()
		{
			if (_assetSphere != null) return;

			AssetBundle _assetBundle = null;
			try
			{
				byte[] _resource = ResourceUtils.GetEmbeddedResource("sphere.unity3d") ?? throw new ArgumentNullException("GetEmbeddedResource");
				_assetBundle = AssetBundle.LoadFromMemory(_resource) ?? throw new ArgumentNullException("LoadFromMemory");
				string _name = _assetBundle.GetAllAssetNames().First(x => x.Contains("sphere"));
				DebugMsg(LogLevel.Warning, $"assetName: {_name}");
				_assetSphere = _assetBundle.LoadAsset<GameObject>(_name)?.GetComponentInChildren<MeshRenderer>()?.gameObject ?? throw new ArgumentNullException("LoadAsset");
				_assetBundle.Unload(false);

				_assetSphere.name = _boneInicatorName;
				_assetSphere.SetActive(false);
			}
			catch (Exception)
			{
				if (_assetBundle != null) _assetBundle.Unload(true);
				throw;
			}
		}

		internal static void LoadAsset_bonelyfans()
		{
			if (_assetBonelyfans != null) return;

			AssetBundle _assetBundle = null;
			try
			{
				byte[] _resource = ResourceUtils.GetEmbeddedResource("bonelyfans.unity3d") ?? throw new ArgumentNullException("GetEmbeddedResource");
				_assetBundle = AssetBundle.LoadFromMemory(_resource) ?? throw new ArgumentNullException("LoadFromMemory");
				string _name = _assetBundle.GetAllAssetNames().First(x => x.Contains("bonelyfans"));
				DebugMsg(LogLevel.Warning, $"assetName: {_name}");
				Shader _shader = _assetBundle.LoadAsset<Shader>(_name) ?? throw new ArgumentNullException("LoadAsset");
				_assetBundle.Unload(false);

				_assetBonelyfans = new Material(_shader);
				_assetBonelyfans.name = "Bonelyfans";
				_assetBonelyfans.SetColor("_Color", _cfgBonelyfanColor.Value);
				_assetBonelyfans.SetInt("_UseMaterialColor", 1);
			}
			catch (Exception)
			{
				if (_assetBundle != null) _assetBundle.Unload(true);
				throw;
			}
		}

		internal static void Init_Indicator()
		{
			if (JetPack.CharaStudio.Running) return;

			LoadAsset_sphere();
			LoadAsset_bonelyfans();
			_assetSphere.GetComponent<Renderer>().material = _assetBonelyfans;
			_assetSphere.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
		}
	}
}
