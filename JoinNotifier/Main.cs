using MelonLoader;
using PyroMod;
using PyroMod.API.QuickMenu;
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnhollowerRuntimeLib;
using UnityEngine;
using UnityEngine.UI;
using VRC;
using VRC.Core;

[assembly: MelonInfo(typeof(JoinNotifier.Main), "Join Notifier", "1.0.0")]
[assembly: MelonGame("VRChat", "VRChat")]
[assembly: MelonAdditionalDependencies("PyroMod")]

namespace JoinNotifier
{
    public class Main : MelonMod
    {
        private static PyroModule Module;
        private static MelonPreferences_Category _category;
        private static MelonPreferences_Entry<bool> _enabled;
        private static MelonPreferences_Entry<bool> _friendsOnly;
        private static MelonPreferences_Entry<bool> _playSound;
        private static MelonPreferences_Entry<bool> _hudAlerts;
        private static AudioSource _audioSource;
        private static AudioClip _bell;
        public static Sprite _joinSprite;
        public static Sprite _leftSprite;
        private static QMToggleButton BtnEnabled;
        private static QMToggleButton BtnFriendsOnly;
        private static QMToggleButton BtnJoinAudio;
        private static QMToggleButton BtnHudAlerts;
        private static GameObject _hudObj;
        public static Image _hudImg;
        public static Text _hudTxt;
        public static HudHandler _hudHandler;
        public static bool _playerLoaded;

        public override void OnApplicationStart()
        {
            Module = PyroMod.Main.RegisterModule("Join Notifier", "1.0.0", "WTFBlaze", ConsoleColor.Blue, "https://github.com/WTFBlaze/PyroModules");
            Module.CreateCategory("Join Notifier");
            Module.AddHook_QMInitialized(nameof(QMInitialized));
            Module.AddHook_PlayerJoined(nameof(PlayerJoined));
            Module.AddHook_PlayerLeft(nameof(PlayerLeft));
            Module.AddHook_LocalPlayerLoaded(nameof(LocalPlayerLoaded));
            Module.AddHook_LeftRoom(nameof(LeftRoom));

            ClassInjector.RegisterTypeInIl2Cpp<HudHandler>();

            _category = MelonPreferences.CreateCategory("PyroMod - Join Notifier");
            _enabled = _category.CreateEntry("enabled", true, "Enabled");
            _friendsOnly = _category.CreateEntry("friends-only", false, "Friends Only");
            _playSound = _category.CreateEntry("play-sound", true, "Play Sound");
            _hudAlerts = _category.CreateEntry("hud-alerts", true, "Hud Alerts");

            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("JoinNotifier.joinnotifier"))
            {
                using (var tempStream = new MemoryStream((int)stream.Length))
                {
                    stream.CopyTo(tempStream);
                    var bundle = AssetBundle.LoadFromMemory_Internal(tempStream.ToArray(), 0);
                    bundle.hideFlags |= HideFlags.DontUnloadUnusedAsset;
                    _bell = bundle.LoadAsset("Assets/Join Notifier/bell.mp3", Il2CppType.Of<AudioClip>()).Cast<AudioClip>();
                    _bell.hideFlags |= HideFlags.DontUnloadUnusedAsset;
                    _joinSprite = bundle.LoadAsset("Assets/Join Notifier/join.png", Il2CppType.Of<Sprite>()).Cast<Sprite>();
                    _joinSprite.hideFlags |= HideFlags.DontUnloadUnusedAsset;
                    _leftSprite = bundle.LoadAsset("Assets/Join Notifier/leave.png", Il2CppType.Of<Sprite>()).Cast<Sprite>();
                    _leftSprite.hideFlags |= HideFlags.DontUnloadUnusedAsset;
                }
            }

            BtnEnabled = Module.CreateToggle(Module.GetCategory(), "Enabled", delegate
            {
                _enabled.Value = true;
                MelonPreferences.Save();
            }, delegate
            {
                _enabled.Value = false;
                MelonPreferences.Save();
            }, "Toggles the whole module on or off", true);

            BtnFriendsOnly = Module.CreateToggle(Module.GetCategory(), "Friends Only", delegate
            {
                _friendsOnly.Value = true;
                MelonPreferences.Save();
            }, delegate
            {
                _friendsOnly.Value = false;
                MelonPreferences.Save();
            }, "Toggles only notifying for friends", _friendsOnly.Value);

            BtnJoinAudio = Module.CreateToggle(Module.GetCategory(), "Join Audio", delegate
            {
                _playSound.Value = true;
                MelonPreferences.Save();
            }, delegate
            {
                _playSound.Value = false;
                MelonPreferences.Save();
            }, "Toggles playing a bell sound when a player joins or leaves", _playSound.Value);

            BtnHudAlerts = Module.CreateToggle(Module.GetCategory(), "Hud Alerts", delegate
            {
                _hudAlerts.Value = true;
                MelonPreferences.Save();
            }, delegate
            {
                _hudAlerts.Value = false;
                MelonPreferences.Save();
            }, "Show the name of who joins or leaves on the hud near the mic icon", _hudAlerts.Value);
        }

        public override void OnPreferencesLoaded()
        {
            BtnEnabled?.SetToggleState(_enabled.Value);
            BtnFriendsOnly?.SetToggleState(_friendsOnly.Value);
            BtnJoinAudio?.SetToggleState(_playSound.Value);
            BtnHudAlerts?.SetToggleState(_hudAlerts.Value);
        }

        public static void QMInitialized()
        {
            var obj = new GameObject("Join Notifier");
            UnityEngine.Object.DontDestroyOnLoad(obj);
            _audioSource = obj.AddComponent<AudioSource>();
            _audioSource.volume = 0.3f;
            _audioSource.clip = _bell;
            _audioSource.loop = false;
            _audioSource.playOnAwake = false;
            _audioSource.bypassEffects = true;
            _audioSource.bypassListenerEffects = true;
            _audioSource.bypassReverbZones = true;
            _audioSource.ignoreListenerVolume = true;
            _audioSource.mute = false;
            MelonCoroutines.Start(WaitForMixerGroup());

            var _parent = GameObject.Find("UserInterface/UnscaledUI/HudContent/Hud/VoiceDotParent");
            _hudObj = UnityEngine.Object.Instantiate(_parent, _parent.transform.GetParent(), false);
            _hudObj.name = "JoinNotifierParent";
            UnityEngine.Object.Destroy(_hudObj.transform.Find("VoiceDot").gameObject);
            UnityEngine.Object.Destroy(_hudObj.transform.Find("PushToTalkKeybd").gameObject);
            UnityEngine.Object.Destroy(_hudObj.transform.Find("PushToTalkXbox").gameObject);
            var _hudImgObj = _hudObj.transform.Find("VoiceDotDisabled").gameObject;
            _hudImgObj.name = "JoinNotifier-Icon";
            UnityEngine.Object.Destroy(_hudImgObj.GetComponent<FadeCycleEffect>());
            _hudImg = _hudImgObj.GetComponent<Image>();
            _hudHandler = obj.AddComponent<HudHandler>();
            _hudImg.sprite = _joinSprite;
            _hudImg.color = Color.white;
            _hudObj.GetComponent<RectTransform>().anchoredPosition = new Vector2(-365, -269.92f);
            _hudImgObj.SetActive(false);

            var _hudTxtObj = new GameObject("JoinNotifier-Text");
            _hudTxt = _hudTxtObj.AddComponent<Text>();
            _hudTxtObj.transform.SetParent(_hudObj.transform, false);
            _hudTxtObj.GetComponent<RectTransform>().anchoredPosition = new Vector2(85, -55);
            _hudTxtObj.GetComponent<RectTransform>().sizeDelta = new Vector2(300, 150);
            _hudTxt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            _hudTxt.horizontalOverflow = HorizontalWrapMode.Wrap;
            _hudTxt.horizontalOverflow = HorizontalWrapMode.Wrap;
            _hudTxt.verticalOverflow = VerticalWrapMode.Overflow;
            _hudTxt.alignment = TextAnchor.MiddleLeft;
            _hudTxt.fontStyle = FontStyle.Bold;
            _hudTxt.supportRichText = true;
            _hudTxt.fontSize = 20;
            _hudTxtObj.SetActive(false);
        }

        private static IEnumerator WaitForMixerGroup()
        {
            while (GameObject.Find("UserInterface/MenuContent/MenuAudio") is null) yield return null;
            _audioSource.outputAudioMixerGroup = GameObject.Find("UserInterface/MenuContent/MenuAudio").GetComponent<AudioSource>().outputAudioMixerGroup;
            yield break;
        }

        public static void PlayerJoined(Player player)
        {
            if (_enabled.Value)
            {
                if (!player.field_Private_APIUser_0.IsSelf)
                {
                    if (_friendsOnly.Value && !player.field_Private_APIUser_0.isFriend) return;
                    if (_playSound.Value)
                    {
                        if (!_audioSource.isPlaying) _audioSource.Play();
                    }
                    if (_hudAlerts.Value)
                    {
                        _hudHandler.queue.Enqueue(new HudQueueItem()
                        {
                            type = HudType.Join,
                            username = player.field_Private_APIUser_0.displayName
                        });
                    }
                    Module.Logger.Log($"{player.field_Private_APIUser_0.displayName} has joined!");
                }
            }
        }

        public static void PlayerLeft(Player player)
        {
            if (_enabled.Value)
            {
                if (!player.field_Private_APIUser_0.IsSelf)
                {
                    if (_playSound.Value)
                    {
                        if (!_audioSource.isPlaying) _audioSource.Play();
                    }
                    if (_hudAlerts.Value)
                    {
                        _hudHandler.queue.Enqueue(new HudQueueItem()
                        {
                            type = HudType.Leave,
                            username = player.field_Private_APIUser_0.displayName
                        });
                    }
                    Module.Logger.Log($"{player.field_Private_APIUser_0.displayName} has left!");
                }
            }
        }

        public static void LocalPlayerLoaded(VRCPlayer vrcPlayer)
        {
            _playerLoaded = true;
        }

        public static void LeftRoom()
        {
            _playerLoaded = false;
            _hudHandler.Clear();
        }
    }
}
