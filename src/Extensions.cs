using ParadoxNotion.Serialization;

namespace AAAPK
{
	public static partial class Extensions
	{
		public static T JsonClone<T>(this object _self)
		{
			if (_self == null)
				return default(T);
			string _json = JSONSerializer.Serialize(_self.GetType(), _self);
			return (T) JSONSerializer.Deserialize(_self.GetType(), _json);
		}

		public static int SlotIndex(this ChaCustom.CvsAccessory _self)
        {
			if (_self == null) return -1;
			return (int) _self.slotNo;
        }
	}
}
