using System.Collections.Generic;

using UnityEngine;
using ChaCustom;

using KKAPI.Maker;

namespace AAAPK
{
	public partial class AAAPK
	{
		public partial class AAAPKUI : MonoBehaviour
		{
			private void Awake()
			{
				DontDestroyOnLoad(this);
				enabled = false;

				_windowRectID = GUIUtility.GetControlID(FocusType.Passive);
				_windowPos.x = _cfgMakerWinX.Value;
				_windowPos.y = _cfgMakerWinY.Value;
				_windowBGtex = MakeTex((int) _windowSize.x, (int) _windowSize.y + 10, _windowBG);
				_passThrough = _cfgDragPass.Value;
				_windowRect = new Rect(_windowPos.x, _windowPos.y, _windowSize.x, _windowSize.y);
				ChangeRes();
			}

			private readonly GUILayoutOption _gloButtonS = GUILayout.Width(50);
			private readonly GUILayoutOption _gloButtonM = GUILayout.Width(75);

			private int _windowRectID;
			private Rect _windowRect, _dragWindowRect;

			private Vector2 _windowSize = new Vector2(400, 695);
			internal Vector2 _windowPos = new Vector2(525, 80);
			private Texture2D _windowBGtex = null;
			internal bool _onAccTab = false;
			private bool _hasFocus = false;
			private bool _passThrough = false;

			private Vector2 _ScreenRes = Vector2.zero;
			internal float _cfgScaleFactor = 1f;
			private Vector2 _resScaleFactor = Vector2.one;
			private Matrix4x4 _resScaleMatrix;

			private readonly Color _windowBG = new Color(0.5f, 0.5f, 0.5f, 1f);

			private Vector2 _boneScrollPosition = Vector2.zero;
			private Vector2 _accScrollPosition = Vector2.zero;

			internal int _currentCoordinateIndex = -1;
			internal int _currentSlotIndex = -1;

			internal ListInfoComponent _currentSlotGameObject = null;
			internal bool _needRefreshSlotInfo = false;

			private GameObject _selectedBoneGameObject = null;
			private GameObject _selectedParentGameObject = null;

			private string _selectedBonePath = "";
			private string _selectedParentPath = "";

			internal ChaControl _chaCtrl => CustomBase.Instance?.chaCtrl;
			internal AAAPKController _pluginCtrl => _chaCtrl?.GetComponent<AAAPKController>();
			internal ParentRule _currentSlotRule = null;

			internal string _searchTerm = "";
			internal HashSet<GameObject> _openedNodes = new HashSet<GameObject>();

			private bool _initStyle = true;

			private GUIStyle _windowSolid;

			private void InitStyle()
			{
				_windowSolid = new GUIStyle(GUI.skin.window);
				_windowSolid.normal.background = _windowSolid.onNormal.background;
				_initStyle = false;
			}

			private void OnGUI()
			{
				if (!_onAccTab) return;
				if (CustomBase.Instance?.chaCtrl == null) return;
				if (CustomBase.Instance.customCtrl.hideFrontUI) return;
				if (!Manager.Scene.Instance.AddSceneName.IsNullOrEmpty() && Manager.Scene.Instance.AddSceneName != "CustomScene") return;

				if (_currentCoordinateIndex != CustomBase.Instance.chaCtrl.fileStatus.coordinateType)
				{
					_currentCoordinateIndex = CustomBase.Instance.chaCtrl.fileStatus.coordinateType;
					//RefreshSlotInfo();
					_needRefreshSlotInfo = true;
				}

				if (_currentSlotIndex != AccessoriesApi.SelectedMakerAccSlot)
				{
					_currentSlotIndex = AccessoriesApi.SelectedMakerAccSlot;
					//RefreshSlotInfo();
					_needRefreshSlotInfo = true;
				}

				GameObject _go = GetObjAccessory(_chaCtrl, _currentSlotIndex);
				ListInfoComponent _cmp = _go == null ? null : _go.GetComponent<ListInfoComponent>();
				if (_currentSlotGameObject != _cmp)
				{
					_currentSlotGameObject = _cmp;
					//RefreshSlotInfo();
					_needRefreshSlotInfo = true;
				}

				if (_needRefreshSlotInfo)
					RefreshSlotInfo();

				if (_currentSlotGameObject == null) return;

				if (_ScreenRes.x != Screen.width || _ScreenRes.y != Screen.height)
					ChangeRes();

				if (_initStyle)
				{
					ChangeRes();
					InitStyle();
				}
				GUI.matrix = _resScaleMatrix;
				_dragWindowRect = GUILayout.Window(_windowRectID, _windowRect, DrawMakerWindow, "", _windowSolid);
				_windowRect.x = _dragWindowRect.x;
				_windowRect.y = _dragWindowRect.y;

				Event _windowEvent = Event.current;
				if (EventType.MouseDown == _windowEvent.type || EventType.MouseUp == _windowEvent.type || EventType.MouseDrag == _windowEvent.type || EventType.MouseMove == _windowEvent.type)
					_hasFocus = false;

				//if (_hasFocus && GetResizedRect(_windowRect).Contains(new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y)))
				if ((!_passThrough || _hasFocus) && GetResizedRect(_windowRect).Contains(new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y)))
					Input.ResetInputAxes();
			}

			// https://bensilvis.com/unity3d-auto-scale-gui/
			private Rect GetResizedRect(Rect _rect)
			{
				Vector2 _position = GUI.matrix.MultiplyVector(new Vector2(_rect.x, _rect.y));
				Vector2 _size = GUI.matrix.MultiplyVector(new Vector2(_rect.width, _rect.height));

				return new Rect(_position.x, _position.y, _size.x, _size.y);
			}

			private void OnEnable()
			{
				_hasFocus = true;
				_needRefreshSlotInfo = true;
			}

			private void OnDisable()
			{
				_initStyle = true;
				_hasFocus = false;

				SetSelectedParent(null);
				SetSelectedBone(null);
				_currentSlotRule = null;
				_currentCoordinateIndex = -1;
				_currentSlotIndex = -1;
			}

			// https://answers.unity.com/questions/840756/how-to-scale-unity-gui-to-fit-different-screen-siz.html
			internal void ChangeRes()
			{
				//_cfgScaleFactor = _cfgMakerWinScale.Value;
				_ScreenRes.x = Screen.width;
				_ScreenRes.y = Screen.height;
				_resScaleFactor.x = _ScreenRes.x / 1600;
				_resScaleFactor.y = _ScreenRes.y / 900;

				if (_cfgMakerWinResScale.Value)
					_resScaleMatrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(_resScaleFactor.x * _cfgScaleFactor, _resScaleFactor.y * _cfgScaleFactor, 1));
				else
					_resScaleMatrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(_cfgScaleFactor, _cfgScaleFactor, 1));
				ResetPos();
			}

			internal void ResetPos()
			{
				_windowPos.x = _cfgMakerWinX.Value;
				_windowPos.y = _cfgMakerWinY.Value;

				if (_cfgMakerWinResScale.Value)
				{
					_windowRect.x = _windowPos.x / _cfgScaleFactor;
					_windowRect.y = _windowPos.y / _cfgScaleFactor;
				}
				else
				{
					_windowRect.x = _windowPos.x * _resScaleFactor.x / _cfgScaleFactor;
					_windowRect.y = _windowPos.y * _resScaleFactor.y / _cfgScaleFactor;
				}
			}

			private void CloseWindow()
			{
				enabled = false;
			}

			private Texture2D MakeTex(int _width, int _height, Color _color)
			{
				Color[] pix = new Color[_width * _height];

				for (int i = 0; i < pix.Length; i++)
					pix[i] = _color;

				Texture2D result = new Texture2D(_width, _height);
				result.SetPixels(pix);
				result.Apply();

				return result;
			}
		}
	}
}