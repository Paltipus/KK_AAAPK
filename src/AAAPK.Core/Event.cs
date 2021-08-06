using KKAPI.Chara;

namespace AAAPK
{
	public partial class AAAPK
	{
		internal static void OnChangeCoordinateType(JetPack.Chara.ChangeCoordinateTypeEventArgs _args)
		{
			if (_args.State == "Prefix")
				OnChangeCoordinateType_Prefix(_args.ChaControl);
			else if (_args.State == "Postfix")
				OnChangeCoordinateType_Postfix(_args.ChaControl);
			else if (_args.State == "Coroutine")
				OnChangeCoordinateType_Coroutine(_args.ChaControl);
		}

		internal static void OnChangeCoordinateType_Prefix(ChaControl _chaCtrl)
		{
			AAAPKController _pluginCtrl = GetController(_chaCtrl);
			if (_pluginCtrl == null) return;

			_pluginCtrl._duringLoadChange = true;
			_pluginCtrl._triggerSlots.Clear();
			_pluginCtrl._queueSlots.Clear();
			_pluginCtrl._rulesCache.Clear();
		}

		internal static void OnChangeCoordinateType_Postfix(ChaControl _chaCtrl)
		{
			AAAPKController _pluginCtrl = GetController(_chaCtrl);
			if (_pluginCtrl == null) return;

			_pluginCtrl.UpdatePartsInfoList();
			_pluginCtrl.RefreshCache();
		}

		internal static void OnChangeCoordinateType_Coroutine(ChaControl _chaCtrl)
		{
			AAAPKController _pluginCtrl = GetController(_chaCtrl);
			if (_pluginCtrl == null) return;

			_pluginCtrl._duringLoadChange = false;
		}

		internal static void OnDataApply(JetPack.MaterialEditor.ControllerEventArgs _args)
		{
			if (_args.State != "Coroutine") return;

			AAAPKController _pluginCtrl = GetController((_args.Controller as CharaCustomFunctionController).ChaControl);
			if (_pluginCtrl == null) return;

			//_pluginCtrl._duringLoadChange = false;
			_pluginCtrl.ApplyParentRuleList("OnDataApply");
		}
	}
}
