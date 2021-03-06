﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using BepInEx;
using Manager;

namespace ObjectTreeDebugKK
{
    [BepInPlugin("ObjectTreeDebugKKPort", "ObjectTreeDebugKK", "1.0.1")]
    public class ObjectTreeDebug : BaseUnityPlugin
    {
        private Transform _target;
        private readonly HashSet<GameObject> _openedObjects = new HashSet<GameObject>();
        private Vector2 _scroll;
        private Vector2 _scroll2;
        private Vector2 _scroll3;
        private static readonly LinkedList<KeyValuePair<LogType, string>> _lastlogs = new LinkedList<KeyValuePair<LogType, string>>();
        private static bool _debug;
        private Rect _rect = new Rect(Screen.width / 4f, Screen.height / 4f, Screen.width / 2f, Screen.height / 2f);
        private int _randomId;
        private SavedKeyboardShortcut ShowConsole { get; }
        private bool _isStudio;
        private bool noCtrlConditionDone = false;

        ObjectTreeDebug()
        {
            ShowConsole = new SavedKeyboardShortcut("Show Debug Console", this, new KeyboardShortcut(KeyCode.RightControl));
        }

        void Awake()
        {
            Application.logMessageReceived += HandleLog;
            _randomId = (int)(UnityEngine.Random.value * UInt32.MaxValue);
            for (int i = 0; i < 32; i++)
            {
                string n = LayerMask.LayerToName(i);
                //UnityEngine.Debug.Log("Layer " + i + " " + n);
            }

            _isStudio = Application.productName == "CharaStudio";
        }

        void Update()
        {
            if(ShowConsole.IsDown())
            {
                _debug = !_debug;

                if(!noCtrlConditionDone && _isStudio && !Scene.Instance.IsNowLoadingFade && Singleton<StudioScene>.Instance)
                {
                    var oldCondition = Studio.Studio.Instance.cameraCtrl.noCtrlCondition;
                    Studio.Studio.Instance.cameraCtrl.noCtrlCondition = () => (_debug && _rect.Contains(Event.current.mousePosition)) || oldCondition();
                    noCtrlConditionDone = true;
                }
            }
        }

        void OnDestroy()
        {
            Application.logMessageReceived -= HandleLog;
        }

        private void HandleLog(string condition, string stackTrace, LogType type)
        {
            _lastlogs.AddLast(new KeyValuePair<LogType, string>(type, type + " " + condition));
            if (_lastlogs.Count == 101)
                _lastlogs.RemoveFirst();
            _scroll3.y += 999999;
        }

        private void DisplayObjectTree(GameObject go, int indent)
        {
            Color c = GUI.color;
            if (_target == go.transform)
                GUI.color = Color.cyan;
            GUILayout.BeginHorizontal();
            GUILayout.Space(indent * 20f);
            if (go.transform.childCount != 0)
            {
                if (GUILayout.Toggle(_openedObjects.Contains(go), "", GUILayout.ExpandWidth(false)))
                {
                    if (_openedObjects.Contains(go) == false)
                        _openedObjects.Add(go);
                }
                else
                {
                    if (_openedObjects.Contains(go))
                        _openedObjects.Remove(go);

                }
            }
            else
                GUILayout.Space(20f);
            if (GUILayout.Button(go.name, GUILayout.ExpandWidth(false)))
            {
                _target = go.transform;
            }
            GUI.color = c;
            go.SetActive(GUILayout.Toggle(go.activeSelf, "", GUILayout.ExpandWidth(false)));
            GUILayout.EndHorizontal();
            if (_openedObjects.Contains(go))
                for (int i = 0; i < go.transform.childCount; ++i)
                    DisplayObjectTree(go.transform.GetChild(i).gameObject, indent + 1);
        }

        void OnGUI()
        {
            if (_debug == false)
                return;
            _rect = GUILayout.Window(_randomId, _rect, WindowFunc, "Debug Console");
        }

        private void WindowFunc(int id)
        {
            GUILayout.BeginHorizontal();
            _scroll = GUILayout.BeginScrollView(_scroll, GUI.skin.box, GUILayout.ExpandHeight(true), GUILayout.MinWidth(300));
            foreach (Transform t in Resources.FindObjectsOfTypeAll<Transform>())
                if (t.parent == null)
                    DisplayObjectTree(t.gameObject, 0);
            GUILayout.EndScrollView();
            GUILayout.BeginVertical();
            _scroll2 = GUILayout.BeginScrollView(_scroll2, GUI.skin.box);
            if (_target != null)
            {
                Transform t = _target.parent;
                string n = _target.name;
                while (t != null)
                {
                    n = t.name + "/" + n;
                    t = t.parent;
                }
                GUILayout.BeginHorizontal();
                GUILayout.Label(n);
                if (GUILayout.Button("Copy to clipboard", GUILayout.ExpandWidth(false)))
                    GUIUtility.systemCopyBuffer = n;
                GUILayout.EndHorizontal();
                GUILayout.Label("Layer: " + LayerMask.LayerToName(_target.gameObject.layer) + " " + _target.gameObject.layer);
                foreach (Component c in _target.GetComponents<Component>())
                {
                    if (c == null)
                        continue;
                    GUILayout.BeginHorizontal();
                    MonoBehaviour m = c as MonoBehaviour;
                    if (m != null)
                        m.enabled = GUILayout.Toggle(m.enabled, c.GetType().FullName, GUILayout.ExpandWidth(false));
                    else if (c is Animator)
                    {
                        Animator an = (Animator)c;
                        an.enabled = GUILayout.Toggle(an.enabled, c.GetType().FullName, GUILayout.ExpandWidth(false));
                    }
                    else
                        GUILayout.Label(c.GetType().FullName);

                    if (c is Image)
                    {
                        Image img = c as Image;
                        if (img.sprite != null && img.sprite.texture != null)
                        {
                            try
                            {
                                GUILayout.Label(img.sprite.name);
                                Color[] newImg = img.sprite.texture.GetPixels((int)img.sprite.textureRect.x, (int)img.sprite.textureRect.y, (int)img.sprite.textureRect.width, (int)img.sprite.textureRect.height);
                                Texture2D tex = new Texture2D((int)img.sprite.textureRect.width, (int)img.sprite.textureRect.height);
                                tex.SetPixels(newImg);
                                tex.Apply();
                                GUILayout.Label(tex);

                            }
                            catch (Exception)
                            {
                            }
                        }
                    }
                    else if (c is Slider)
                    {
                        Slider b = c as Slider;
                        for (int i = 0; i < b.onValueChanged.GetPersistentEventCount(); ++i)
                            GUILayout.Label(b.onValueChanged.GetPersistentTarget(i).GetType().FullName + "." + b.onValueChanged.GetPersistentMethodName(i));
                    }
                    else if (c is Text)
                    {
                        Text text = c as Text;
                        GUILayout.Label(text.text + " " + text.font + " " + text.fontStyle + " " + text.fontSize + " " + text.alignment + " " + text.alignByGeometry + " " + text.resizeTextForBestFit + " " + text.color);

                    }
                    else if (c is RawImage)
                        GUILayout.Label(((RawImage)c).mainTexture);
                    else if (c is Renderer)
                        GUILayout.Label(((Renderer)c).material != null ? ((Renderer)c).material.shader.name : "");
                    else if (c is Button)
                    {
                        Button b = c as Button;
                        for (int i = 0; i < b.onClick.GetPersistentEventCount(); ++i)
                            GUILayout.Label(b.onClick.GetPersistentTarget(i).GetType().FullName + "." + b.onClick.GetPersistentMethodName(i));
                        IList calls = b.onClick.GetPrivateExplicit<UnityEventBase>("m_Calls").GetPrivate("m_RuntimeCalls") as IList;
                        for (int i = 0; i < calls.Count; ++i)
                        {
                            UnityAction unityAction = ((UnityAction)calls[i].GetPrivate("Delegate"));
                            GUILayout.Label(unityAction.Target.GetType().FullName + "." + unityAction.Method.Name);
                        }
                    }
                    else if (c is Toggle)
                    {
                        Toggle b = c as Toggle;
                        for (int i = 0; i < b.onValueChanged.GetPersistentEventCount(); ++i)
                            GUILayout.Label(b.onValueChanged.GetPersistentTarget(i).GetType().FullName + "." + b.onValueChanged.GetPersistentMethodName(i));
                        IList calls = b.onValueChanged.GetPrivateExplicit<UnityEventBase>("m_Calls").GetPrivate("m_RuntimeCalls") as IList;
                        for (int i = 0; i < calls.Count; ++i)
                        {
                            UnityAction<bool> unityAction = ((UnityAction<bool>)calls[i].GetPrivate("Delegate"));
                            GUILayout.Label(unityAction.Target.GetType().FullName + "." + unityAction.Method.Name);
                        }
                    }
                    else if (c is RectTransform)
                    {
                        RectTransform rt = c as RectTransform;
                        GUILayout.Label("anchorMin " + rt.anchorMin);
                        GUILayout.Label("anchorMax " + rt.anchorMax);
                        GUILayout.Label("offsetMin " + rt.offsetMin);
                        GUILayout.Label("offsetMax " + rt.offsetMax);
                        GUILayout.Label("rect " + rt.rect);
                    }
                    GUILayout.EndHorizontal();
                }
            }
            GUILayout.EndScrollView();
            _scroll3 = GUILayout.BeginScrollView(_scroll3, GUI.skin.box, GUILayout.Height(Screen.height / 4f));
            foreach (KeyValuePair<LogType, string> lastlog in _lastlogs)
            {
                Color c = GUI.color;
                switch (lastlog.Key)
                {
                    case LogType.Error:
                    case LogType.Exception:
                        GUI.color = Color.red;
                        break;
                    case LogType.Warning:
                        GUI.color = Color.yellow;
                        break;
                }
                GUILayout.BeginHorizontal();
                GUILayout.Label(lastlog.Value);
                GUI.color = c;
                if (GUILayout.Button("Copy to clipboard", GUILayout.ExpandWidth(false)))
                    GUIUtility.systemCopyBuffer = lastlog.Value;
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Clear AssetBundle Cache"))
            {
                foreach (KeyValuePair<string, AssetBundleManager.BundlePack> pair in AssetBundleManager.ManifestBundlePack)
                {
                    foreach (KeyValuePair<string, LoadedAssetBundle> bundle in new Dictionary<string, LoadedAssetBundle>(pair.Value.LoadedAssetBundles))
                    {
                        AssetBundleManager.UnloadAssetBundle(bundle.Key, true, pair.Key);
                    }
                }
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Clear logs", GUILayout.ExpandWidth(false)))
                _lastlogs.Clear();
            if (GUILayout.Button("Open log file", GUILayout.ExpandWidth(false)))
                System.Diagnostics.Process.Start(System.IO.Path.Combine(Application.dataPath, "output_log.txt"));
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUI.DragWindow();
        }
    }
}

