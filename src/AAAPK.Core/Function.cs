using System.Linq;

using UnityEngine;
using MessagePack;

namespace AAAPK
{
	public partial class AAAPK
	{
		internal static void ChangeParent(ChaControl _chaCtrl, int _slotIndex, GameObject _ca_slot, Transform _parentNode, bool _keepPos = true, bool _keepRot = true, bool _reset = false)
		{
			ChaFileAccessory.PartsInfo _part = new ChaFileAccessory.PartsInfo();
			if (_slotIndex < 20)
				_part = _chaCtrl.nowCoordinate.accessory.parts[_slotIndex];
			else
				_part = JetPack.MoreAccessories.ListNowAccessories(_chaCtrl).ElementAtOrDefault(_slotIndex - 20);

			if (_part == null || _part.type == 120 || _ca_slot == null || _parentNode == null) return;

			Vector3 _parentScale = _ca_slot.transform.parent.localScale;

			Transform _n_move = _ca_slot.GetComponentsInChildren<Transform>().Where(x => x.name == "N_move").FirstOrDefault();
			if (_n_move == null)
			{
				_logger.LogMessage($"Skip Slot{_slotIndex + 1:00} because it doesn't have a valid N_move Transform");
				return;
			}
			Vector3 _position = _n_move.position;
			Quaternion _rotation = _n_move.rotation;

			Transform _n_move2 = _ca_slot.GetComponentsInChildren<Transform>().Where(x => x.name == "N_move2").FirstOrDefault();
			Vector3 _position2 = _n_move2 == null ? Vector3.zero : _n_move2.position;
			Quaternion _rotation2 = _n_move2 == null ? Quaternion.identity : _n_move2.rotation;
			Vector3 _scale2 = _n_move2 == null ? Vector3.one : _n_move2.localScale;

			_ca_slot.transform.SetParent(_parentNode, true);
			//Vector3 _referenceScale = _parentNode.localScale;
			//Vector3 _parentScaleRate = new Vector3(_parentScale.x / _referenceScale.x, _parentScale.y / _referenceScale.y, _parentScale.z / _referenceScale.z);
			Vector3 _parentScaleRate = Vector3.one;

			_n_move.localScale = new Vector3(_n_move.localScale.x * _ca_slot.transform.localScale.x * _parentScaleRate.x, _n_move.localScale.y * _ca_slot.transform.localScale.y * _parentScaleRate.y, _n_move.localScale.z * _ca_slot.transform.localScale.z * _parentScaleRate.z);

			bool _underN = false;

			if (_n_move2 != null)
			{
				_underN = _n_move.GetComponentsInChildren<Transform>().Any(x => x.name == "N_move2"); // N_move2 under N_move case
				if (_underN)
					_n_move2.localScale = _scale2;
				else
					_n_move2.localScale = new Vector3(_n_move2.localScale.x * _ca_slot.transform.localScale.x * _parentScaleRate.x, _n_move2.localScale.y * _ca_slot.transform.localScale.y * _parentScaleRate.y, _n_move2.localScale.z * _ca_slot.transform.localScale.z * _parentScaleRate.z);
			}

			_ca_slot.transform.localPosition = Vector3.zero;
			_ca_slot.transform.localEulerAngles = Vector3.zero;
			_ca_slot.transform.localScale = Vector3.one;

			if (_reset)
				_part.parentKey = _parentNode.name;

			if (_keepPos)
			{
				_n_move.position = _position;
				_part.addMove[0, 0] = new Vector3(float.Parse((_n_move.localPosition.x * 100f).ToString("f2")), float.Parse((_n_move.localPosition.y * 100f).ToString("f2")), float.Parse((_n_move.localPosition.z * 100f).ToString("f2")));
			}
			if (_keepRot)
			{
				_n_move.rotation = _rotation;
				_part.addMove[0, 1] = new Vector3((float.Parse(_n_move.localEulerAngles.x.ToString("f2")) + 360f) % 360f, (float.Parse(_n_move.localEulerAngles.y.ToString("f2")) + 360f) % 360f, (float.Parse(_n_move.localEulerAngles.z.ToString("f2")) + 360f) % 360f);
			}
			_part.addMove[0, 2] = new Vector3(float.Parse(_n_move.localScale.x.ToString("f2")), float.Parse(_n_move.localScale.y.ToString("f2")), float.Parse(_n_move.localScale.z.ToString("f2")));

			if (_n_move2 != null && !_underN)
			{
				if (_keepPos)
				{
					_n_move2.position = _position2;
					_part.addMove[1, 0] = new Vector3(float.Parse((_n_move2.localPosition.x * 100f).ToString("f2")), float.Parse((_n_move2.localPosition.y * 100f).ToString("f2")), float.Parse((_n_move2.localPosition.z * 100f).ToString("f2")));
				}
				if (_keepRot)
				{
					_n_move2.rotation = _rotation2;
					_part.addMove[1, 1] = new Vector3((float.Parse(_n_move2.localEulerAngles.x.ToString("f2")) + 360f) % 360f, (float.Parse(_n_move2.localEulerAngles.y.ToString("f2")) + 360f) % 360f, (float.Parse(_n_move2.localEulerAngles.z.ToString("f2")) + 360f) % 360f);
				}
				_part.addMove[1, 2] = new Vector3(float.Parse(_n_move2.localScale.x.ToString("f2")), float.Parse(_n_move2.localScale.y.ToString("f2")), float.Parse(_n_move2.localScale.z.ToString("f2")));
			}

			if (_slotIndex < 20 && _slotIndex > -1)
			{
				byte[] _bytes = MessagePackSerializer.Serialize(_part);
				_chaCtrl.chaFile.coordinate[_chaCtrl.fileStatus.coordinateType].accessory.parts[_slotIndex] = MessagePackSerializer.Deserialize<ChaFileAccessory.PartsInfo>(_bytes);
			}
		}
	}
}
