using ChaCustom;

namespace AAAPK
{
	public static partial class Extensions
	{
		public static int SlotIndex(this CvsAccessory _self)
		{
			if (_self == null)
				return -1;
			return _self.nSlotNo;
		}
	}
}
