using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using UnityEngine.Serialization;
using Unity.Networking.Transport.Relay;

public class MainMenu : MonoBehaviour {

    [Header("Direct Connect")]
    [SerializeField]
    [FormerlySerializedAs("directConnectAnchor")]
    private GameObject _directConnectAnchor;

    [SerializeField]
    [FormerlySerializedAs("directIpInput")]
    private InputField _directIpInput;

    [Header("Net Play")]
    [SerializeField]
    [FormerlySerializedAs("netPlayAnchor")]
    private GameObject _netPlayAnchor;

    [SerializeField]
    [FormerlySerializedAs("netListParent")]
    private GameObject _netListParent;

    [SerializeField]
    [FormerlySerializedAs("matchPrefab")]
    private GameObject _matchPrefab;

    [SerializeField]
    [FormerlySerializedAs("netMatchName")]
    private InputField _netMatchName;

    [Header("Options")]
    [SerializeField]
    [FormerlySerializedAs("optionsAnchor")]
    private GameObject _optionsAnchor;

    [SerializeField]
    [FormerlySerializedAs("namePref")]
    private StringPref _namePref;

    public NetworkManager Manager {
        get {
            return FindObjectOfType<NetworkManager>();
        }
    }

    private MenuState _menuState = MenuState.Main;
    private List<GameObject> _spawnedButtons = new List<GameObject>();

    private void Update() {
        bool directConnectActive = false;
        bool netPlayActive = false;
        bool optionsActive = false;

        //new RelayServerData(0)

        switch (_menuState) {
            case MenuState.Options:
                optionsActive = true;
                break;
            case MenuState.DirectConnect:
                directConnectActive = true;
                //Manager.networkAddress = _directIpInput.text;
                break;
            case MenuState.MatchMaker:
                netPlayActive = true;
                //Manager.matchName = _netMatchName.text;
                break;
        }

        _directConnectAnchor.SetActive(directConnectActive);
        _netPlayAnchor.SetActive(netPlayActive);
        _optionsAnchor.SetActive(optionsActive);
    }

    public void OnSelectOptions() {
        transitionState(MenuState.Options);
    }

    public void OnSelectDirectConnect() {
        transitionState(MenuState.DirectConnect);
    }

    public void OnSelectMatchMaker() {
        transitionState(MenuState.MatchMaker);
    }

    public void OnSelectRefreshMatchList() {
        //Manager.matchMaker.ListMatches(0, 20, "", true, 0, 0, onRecieveMatchList);
    }

    public void OnSelectHostDirect() {
        Manager.StartHost();
    }

    public void OnSelectClientDirect() {
        Manager.StartClient();
    }

    public void OnSelectCreateMatch() {
        //if (Manager.matchMaker != null) {
        //  Manager.matchName = Manager.matchName.Trim();
        //  if (string.IsNullOrEmpty(Manager.matchName) || Manager.matchName.Length > 100) {
        //    Manager.matchName = "New Game";
        //  }

        //  Manager.matchMaker.CreateMatch(Manager.matchName, Manager.matchSize, true, "", "", "", 0, 0, Manager.OnMatchCreate);
        //}
    }

    public enum MenuState {
        Main,
        Options,
        DirectConnect,
        MatchMaker
    }

    private void transitionState(MenuState toState) {
        if (toState == _menuState) {
            return;
        }

        //switch (_menuState) {
        //case MenuState.MatchMaker:
        //Manager.StopMatchMaker();
        //break;
        //}

        _menuState = toState;

        switch (_menuState) {
            case MenuState.MatchMaker:
                _netMatchName.text = _namePref.Value + "'s Game";
                //Manager.StartMatchMaker();
                OnSelectRefreshMatchList();
                break;
        }
    }

    //private void onRecieveMatchList(bool success, string extendedInfo, List<MatchInfoSnapshot> matches) {
    //  foreach (var button in _spawnedButtons) {
    //    DestroyImmediate(button);
    //  }

    //  for (int i = 0; i < matches.Count; i++) {
    //    var match = matches[i];
    //    var matchButton = Instantiate(_matchPrefab);
    //    matchButton.transform.SetParent(_netListParent.transform);
    //    matchButton.GetComponentInChildren<Text>().text = match.name;
    //    matchButton.GetComponent<Button>().onClick.AddListener(() => {
    //      Manager.matchName = match.name;
    //      Manager.matchSize = (uint)match.currentSize;
    //      Manager.matchMaker.JoinMatch(match.networkId, "", "", "", 0, 0, Manager.OnMatchJoined);
    //    });
    //    matchButton.SetActive(true);

    //    _spawnedButtons.Add(matchButton);
    //  }
    //}
}
