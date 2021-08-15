using System;
using System.Collections;
using System.Linq;

using UnityEngine;
using ChaCustom;

using BepInEx.Logging;
using HarmonyLib;

namespace AAAPK
{
	public partial class AAAPK
	{
		private static GameObject GetParentNodeGameObjectByRule(ChaControl _chaCtrl, ParentRule _rule)
		{
			if (_rule == null)
				return null;

			Transform _parentNode = null;
			if (_rule.ParentType == ParentType.Character)
				_parentNode = _chaCtrl.transform.Find(_rule.ParentPath);
			else if (_rule.ParentType == ParentType.Accessory)
				_parentNode = GetObjAccessory(_chaCtrl, _rule.ParentSlot)?.transform?.Find(_rule.ParentPath);
			else if (_rule.ParentType == ParentType.Clothing)
				_parentNode = _chaCtrl.objClothes.ElementAtOrDefault(_rule.ParentSlot)?.transform?.Find(_rule.ParentPath);
			else if (_rule.ParentType == ParentType.Hair)
				_parentNode = _chaCtrl.objHair.ElementAtOrDefault(_rule.ParentSlot)?.transform?.Find(_rule.ParentPath);

			if (_parentNode == null)
				return null;

			return _parentNode.gameObject;
		}

		private static void RefreshCoordinate() => CustomBase.Instance?.chaCtrl.ChangeCoordinateTypeAndReload(false);

		public partial class AAAPKUI
		{
			internal void RefreshSlotInfo()
			{
				_currentSlotRule = _pluginCtrl.GetSlotRule(_currentCoordinateIndex, _currentSlotIndex);
				GetParentNodeGameObject();
				SetSelectedBone(_selectedParentGameObject);
				//_openedNodes.Clear();
				_needRefreshSlotInfo = false;
			}

			private IEnumerator ScrollToParentRect()
			{
				yield return JetPack.Toolbox.WaitForEndOfFrame;
				yield return JetPack.Toolbox.WaitForEndOfFrame;

				_logger.LogWarning($"[Warp][position]{_selectedParentRect.position.x}, {_selectedParentRect.position.y}");
				float _posX = _selectedParentRect.position.x < 120 ? 0 : _selectedParentRect.position.x - 120;
				float _posY = _selectedParentRect.position.y < 200 ? 0 : _selectedParentRect.position.y - 200;
				_boneScrollPosition = new Vector2(_posX, _posY);
			}

			private void DrawMakerWindow(int id)
			{
				Event _windowEvent = Event.current;
				if (EventType.MouseDown == _windowEvent.type || EventType.MouseUp == _windowEvent.type || EventType.MouseDrag == _windowEvent.type || EventType.MouseMove == _windowEvent.type)
					_hasFocus = true;

				GUI.Box(new Rect(0, 0, _windowSize.x, _windowSize.y), _windowBGtex);
				GUI.Box(new Rect(0, 0, _windowSize.x, 30), $"Advanced Parent - Slot{_currentSlotIndex + 1:00}", new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter });

				if (GUI.Button(new Rect(_windowSize.x - 27, 4, 23, 23), new GUIContent("X", "Close this window")))
				{
					CloseWindow();
				}

				if (GUI.Button(new Rect(4, 4, 23, 23), new GUIContent("<", "Reset window position")))
				{
					ChangeRes();
				}

				if (GUI.Button(new Rect(27, 4, 23, 23), new GUIContent("T", "Use current window position when reset")))
				{
					if (_cfgMakerWinResScale.Value)
					{
						_windowPos.x = _windowRect.x * _cfgScaleFactor;
						_windowPos.y = _windowRect.y * _cfgScaleFactor;
					}
					else
					{
						_windowPos.x = _windowRect.x / _resScaleFactor.x * _cfgScaleFactor;
						_windowPos.y = _windowRect.y / _resScaleFactor.y * _cfgScaleFactor;
					}
					_cfgMakerWinX.Value = _windowPos.x;
					_cfgMakerWinY.Value = _windowPos.y;
				}

				GUILayout.BeginVertical();
				{
					GUILayout.Space(10);

					string _lastSearchTerm = _searchTerm;

					GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
					{
						if (_currentSlotRule == null)
							GUI.enabled = false;
						if (GUILayout.Button("Warp", _gloButtonS))
						{
							_searchTerm = "";

							GetParentNodeGameObject();
							if (_selectedParentGameObject != null)
							{
								OpenParentsOf(_selectedParentGameObject);
								DebugMsg(LogLevel.Info, $"[Warp][{_selectedParentPath}]");

								StartCoroutine(ScrollToParentRect());
							}
						}
						GUI.enabled = true;

						if (_currentSlotRule == null)
							GUI.enabled = false;
						if (GUILayout.Button("Clear", _gloButtonS))
						{
							_pluginCtrl.RemoveRule(_currentSlotIndex);
							//RefreshCoordinate();

							SetSelectedParent(null);
							SetSelectedBone(null);
							_currentSlotRule = null;
							_pluginCtrl.RefreshCache();

							if (_currentSlotGameObject == null) return;

							ChaFileAccessory.PartsInfo _part = _pluginCtrl._listPartsInfo.ElementAtOrDefault(_currentSlotIndex);
							if (_part == null) return;

							string _parentKey = _part.parentKey;
							GameObject _parentNode = _chaCtrl.GetReferenceInfo((ChaReference.RefObjKey) Enum.Parse(typeof(ChaReference.RefObjKey), _parentKey));
							//_currentSlotGameObject.transform.SetParent(_parentNode.transform, false);
							ResetParentWithScale(_parentNode);
						}
						GUI.enabled = true;
						if (_currentSlotRule != null)
						{
							if (_selectedParentGameObject == null)
								GUILayout.Label($"Current: (Missing)");
							else
								GUILayout.Label($"Current: {_selectedParentGameObject?.name}");
						}
						else
							GUILayout.Label($"Current: (None)");
						GUILayout.FlexibleSpace();
					}
					GUILayout.EndHorizontal();

					GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
					{
						if (_openedNodes.Count == 0)
							GUI.enabled = false;
						if (GUILayout.Button("Collapse", _gloButtonM))
							_openedNodes.Clear();
						GUI.enabled = true;

						_searchTerm = GUILayout.TextField(_searchTerm);

						if (GUILayout.Button("X", GUILayout.Width(20)))
							_searchTerm = "";
					}
					GUILayout.EndHorizontal();

					GUILayout.BeginVertical(GUI.skin.box);
					{
						GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
						{
							GUILayout.Label("Hair");
							GUILayout.FlexibleSpace();
						}
						GUILayout.EndHorizontal();

						GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
						{
							if (GUILayout.Button("Back", _gloButtonS))
								_searchTerm = "ct_hairB";
							if (GUILayout.Button("Front", _gloButtonS))
								_searchTerm = "ct_hairF";
							if (GUILayout.Button("Side", _gloButtonS))
								_searchTerm = "ct_hairS";
							if (GUILayout.Button("Ext", _gloButtonS))
								_searchTerm = "ct_hairO";
						}
						GUILayout.EndHorizontal();

						GUILayout.BeginVertical();
						{
							GUILayout.Label("Accessories");

							_accScrollPosition = GUILayout.BeginScrollView(_accScrollPosition, GUILayout.Height(50));
							{
								if (_pluginCtrl._usedSlots.Count > 0)
								{
									GUILayout.BeginHorizontal(GUILayout.ExpandWidth(false));

									foreach (int _slotIndex in _pluginCtrl._usedSlots)
									{
										if (_slotIndex == _currentSlotIndex)
											continue;

										if (GUILayout.Button(new GUIContent($"{_slotIndex + 1:00}", $"ca_slot{_slotIndex:00}"), _gloButtonS))
											_searchTerm = $"ca_slot{_slotIndex:00}";
									}
									GUILayout.EndHorizontal();

								}
							}
							GUILayout.EndScrollView();
						}
						GUILayout.EndVertical();
					}
					GUILayout.EndVertical();

					if (_lastSearchTerm != _searchTerm)
						_openedNodes.Clear();

					_boneScrollPosition = GUILayout.BeginScrollView(_boneScrollPosition, false, false, GUI.skin.horizontalScrollbar, GUI.skin.verticalScrollbar, GUI.skin.box);
					{
						BuildObjectTree(Root, 0);
					}
					GUILayout.EndScrollView();

					GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
					{
						GUILayout.FlexibleSpace();
						if (_selectedBoneGameObject != null)
						{
							if (GUILayout.Button($"Attach to Selected Parent: {_selectedBoneGameObject.name}", GUILayout.ExpandWidth(false)))
							{
								_pluginCtrl.RemoveRule(_currentSlotIndex);
								_currentSlotRule = null;

								ParentRule _rule = new ParentRule
								{
									Coordinate = _currentCoordinateIndex,
									Slot = _currentSlotIndex
								};

								string _fullPath = _selectedBonePath;

								DebugMsg(LogLevel.Info, $"[Attach][_fullPath: {_fullPath}]");

								if ($"{_fullPath}/".Contains($"/ca_slot{_currentSlotIndex:00}/"))
								{
									_logger.LogMessage($"Cannot attach to this node because current accessory is in parent hierarchy of this slot");
									return;
								}

								GameObject _ca_slot = GetObjAccessory(_chaCtrl, _currentSlotIndex);

								if (_ca_slot.GetComponent(ChaAccessoryClothes) != null || _selectedBoneGameObject.GetComponentsInParent(ChaAccessoryClothes, true)?.Length > 0)
								{
									_logger.LogMessage($"Cannot use this function on Accessory Clothes");
									return;
								}

								if (_fullPath.Contains("/ca_slot"))
								{
									string _parentName = _fullPath.Substring(_fullPath.LastIndexOf("/ca_slot") + 1);
									_parentName = _parentName.Substring(0, _parentName.IndexOf("/"));

									_rule.ParentType = ParentType.Accessory;
									_rule.ParentSlot = int.Parse(_parentName.Replace("ca_slot", ""));

									string _name = "/" + _parentName + "/";
									int _index = _fullPath.LastIndexOf(_name) + _name.Length;
									_rule.ParentPath = _fullPath.Substring(_index);

									_pluginCtrl.ParentRuleList.Add(_rule);
									_pluginCtrl.RefreshCache();

									_currentSlotRule = _rule;
									SetSelectedParent(_selectedBoneGameObject);
									//MoveObjectToPlace();
									SetParentWithScale();
									return;
								}

								for (int i = 0; i < _chaCtrl.objHair.Length; i++)
								{
									string _name = "/" + _chaCtrl.objHair[i].name + "/";
									if (_fullPath.LastIndexOf(_name) < 0) continue;
									_rule.ParentType = ParentType.Hair;
									_rule.ParentSlot = i;
									int _index = _fullPath.LastIndexOf(_name) + _name.Length;
									_rule.ParentPath = _fullPath.Substring(_index);

									_pluginCtrl.ParentRuleList.Add(_rule);
									_pluginCtrl.RefreshCache();

									_currentSlotRule = _rule;
									SetSelectedParent(_selectedBoneGameObject);
									//MoveObjectToPlace();
									SetParentWithScale();
									return;
								}

								{
									_rule.ParentType = ParentType.Character;
									_rule.ParentSlot = -1;
									_rule.ParentPath = _fullPath;

									_pluginCtrl.ParentRuleList.Add(_rule);
									_pluginCtrl.RefreshCache();

									_currentSlotRule = _rule;
									SetSelectedParent(_selectedBoneGameObject);
									//MoveObjectToPlace();
									SetParentWithScale();
								}
							}
						}
						else
						{
							GUILayout.Box($"Select a Bone Above...", GUILayout.ExpandWidth(false));
						}
						GUILayout.FlexibleSpace();
					}
					GUILayout.EndHorizontal();

					GUILayout.BeginHorizontal(GUI.skin.box);
					{
						_cfgKeepPos = GUILayout.Toggle(_cfgKeepPos, new GUIContent(" keep position", "Preserve position on changing parent"));
						_cfgKeepRot = GUILayout.Toggle(_cfgKeepRot, new GUIContent(" keep rotation", "Preserve rotation on changing parent"));
						GUILayout.FlexibleSpace();
					}
					GUILayout.EndHorizontal();

					GUILayout.BeginHorizontal(GUI.skin.box);
					{
#if DEBUG
						if (GUILayout.Button("Panic", _gloButtonM))
						{
							foreach (ListInfoComponent _cmp in _chaCtrl.GetComponentsInChildren<ListInfoComponent>(true))
								Destroy(_cmp?.gameObject);
						}
						if (GUILayout.Button(new GUIContent("Refresh", "Refresh Dynamic Bone"), _gloButtonM))
						{
							Traverse.Create(GetBoneController(_chaCtrl)).Property("NeedsBaselineUpdate").SetValue(true);
						}
#endif
						if (GUILayout.Button(new GUIContent("Refresh", "Refresh coordinate"), _gloButtonM))
						{
							RefreshCoordinate();
						}

						if (GUILayout.Button(new GUIContent("List DB", "List hair and clothing (exclude acc) DB to console"), _gloButtonM))
						{
							foreach (DynamicBone _cmp in _chaCtrl.GetComponentsInChildren<DynamicBone>().Where(x => x.m_Root != null && (bool)!x.gameObject?.name.StartsWith("ca_slot")).ToList())
								_logger.LogInfo(_cmp.m_Root.name);
						}

						GUILayout.FlexibleSpace();
					}
					GUILayout.EndHorizontal();

					GUILayout.BeginHorizontal(GUI.skin.box);
					GUILayout.Label(GUI.tooltip);
					GUILayout.EndHorizontal();
				}
				GUILayout.EndVertical();

				GUI.DragWindow();
			}

			internal void SetParentWithScale()
			{
				AccGotHighRemoveEffect();

				if (_selectedParentGameObject == null)
					return;

				GameObject _ca_slot = GetObjAccessory(_chaCtrl, _currentSlotIndex);
				ChangeParent(_chaCtrl, _currentSlotIndex, _ca_slot, _selectedParentGameObject.transform, _keepPos: _cfgKeepPos, _keepRot: _cfgKeepRot);
				CustomBase.Instance.updateCustomUI = true;
				Traverse.Create(GetBoneController(_chaCtrl)).Property("NeedsBaselineUpdate").SetValue(true);
			}

			internal void ResetParentWithScale(GameObject _parent)
			{
				AccGotHighRemoveEffect();

				if (_parent == null)
					return;

				GameObject _ca_slot = GetObjAccessory(_chaCtrl, _currentSlotIndex);
				ChangeParent(_chaCtrl, _currentSlotIndex, _ca_slot, _parent.transform, _keepPos: _cfgKeepPos, _keepRot: _cfgKeepRot, _reset: true);
				CustomBase.Instance.updateCustomUI = true;
				Traverse.Create(GetBoneController(_chaCtrl)).Property("NeedsBaselineUpdate").SetValue(true);
			}

			internal void MoveObjectToPlace()
			{
				AccGotHighRemoveEffect();

				if (_selectedParentGameObject == null)
					return;

				Transform _ca_slot = GetObjAccessory(_chaCtrl, _currentSlotIndex).transform;
				_ca_slot.SetParent(_selectedParentGameObject.transform, false);
				_ca_slot.localPosition = Vector3.zero;
				_ca_slot.localEulerAngles = Vector3.zero;
				_ca_slot.localScale = Vector3.one;

				Traverse.Create(GetBoneController(_chaCtrl)).Property("NeedsBaselineUpdate").SetValue(true);
			}

			private void BuildObjectTree(GameObject _gameObject, int indentLevel)
			{
				if (_searchTerm.Length == 0 || _gameObject.name.IndexOf(_searchTerm, StringComparison.OrdinalIgnoreCase) > -1 || _openedNodes.Contains(_gameObject.transform.parent.gameObject))
				{
					Color _color = GUI.color;

					if (_selectedParentGameObject == _gameObject)
						GUI.color = Color.yellow;
					else if (_selectedBoneGameObject == _gameObject)
						GUI.color = Color.cyan;

					GUILayout.BeginHorizontal();

					if (_openedNodes.Contains(_gameObject.transform.parent.gameObject))
						GUILayout.Space(indentLevel * 25f);
					else
						indentLevel = 0;

					if (_gameObject.transform.childCount > 0)
					{
						if (GUILayout.Toggle(_openedNodes.Contains(_gameObject), "", GUILayout.ExpandWidth(false)))
							_openedNodes.Add(_gameObject);
						else
							_openedNodes.Remove(_gameObject);
					}
					else
						GUILayout.Space(19);

					if (_gameObject.GetComponent<ListInfoComponent>() != null && _gameObject.name.StartsWith("ca_slot"))
					{
						GUI.enabled = false;
						GUILayout.Button(new GUIContent(_gameObject.name, "Cannot directly attach to another slot root"), GUILayout.ExpandWidth(false));
						GUI.enabled = true;
					}
					else
					{
						if (GUILayout.Button(_gameObject.name, GUILayout.ExpandWidth(false)))
							SetSelectedBone(_gameObject);
					}

					if (_selectedParentGameObject == _gameObject && Event.current.type == EventType.Repaint)
						_selectedParentRect = GUILayoutUtility.GetLastRect();

					GUILayout.EndHorizontal();
					GUI.color = _color;
				}

				if (_searchTerm.Length > 0 || _openedNodes.Contains(_gameObject))
				{
					foreach (Transform child in _gameObject.transform)
						BuildObjectTree(child.gameObject, indentLevel + 1);
				}
			}

			private void OpenParentsOf(GameObject _gameObject)
			{
				_openedNodes.Add(_gameObject);
				if (_gameObject != Root)
					OpenParentsOf(_gameObject.transform.parent.gameObject);
			}

			private GameObject Root
			{
				get
				{
					return CustomBase.Instance?.chaCtrl.objAnim.transform.Find("cf_j_root").gameObject;
					//return CustomBase.Instance?.chaCtrl?.transform.Find("BodyTop").gameObject;
				}
			}

			internal void SetSelectedBone(GameObject _gameObject)
			{
				_selectedBoneGameObject = _gameObject;
				_selectedBonePath = BuildParentString(_gameObject);
			}

			internal void SetSelectedParent(GameObject _gameObject)
			{
				_selectedParentGameObject = _gameObject;
				_selectedParentPath = BuildParentString(_gameObject);
			}

			private void GetParentNodeGameObject()
			{
				if (_currentSlotRule == null)
				{
					SetSelectedParent(null);
					return;
				}

				Transform _parentNode = null;
				if (_currentSlotRule.ParentType == ParentType.Character)
					_parentNode = _chaCtrl.transform.Find(_currentSlotRule.ParentPath);
				else if (_currentSlotRule.ParentType == ParentType.Accessory)
					_parentNode = GetObjAccessory(_chaCtrl, _currentSlotRule.ParentSlot)?.transform?.Find(_currentSlotRule.ParentPath);
				else if (_currentSlotRule.ParentType == ParentType.Clothing)
					_parentNode = _chaCtrl.objClothes.ElementAtOrDefault(_currentSlotRule.ParentSlot)?.transform?.Find(_currentSlotRule.ParentPath);
				else if (_currentSlotRule.ParentType == ParentType.Hair)
					_parentNode = _chaCtrl.objHair.ElementAtOrDefault(_currentSlotRule.ParentSlot)?.transform?.Find(_currentSlotRule.ParentPath);

				if (_parentNode == null)
				{
					SetSelectedParent(null);
					return;
				}

				SetSelectedParent(_parentNode.gameObject);
			}

			private string BuildParentString(GameObject _gameObject)
			{
				if (_gameObject == null)
					return "";

				string _fullPath = _gameObject.name;
				GameObject _current = _gameObject.transform.parent.gameObject;
				while (_current != _chaCtrl.gameObject)
				{
					_fullPath = _current.name + "/" + _fullPath;
					_current = _current.transform.parent.gameObject;
				};
				return _fullPath;
			}
		}
	}
}
