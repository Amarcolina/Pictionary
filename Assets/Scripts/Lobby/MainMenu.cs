using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using UnityEngine.Networking.Match;

public class MainMenu : MonoBehaviour {

  [Header("Direct Connect")]
  public GameObject directConnectAnchor;
  public InputField directIpInput;

  [Header("Net Play")]
  public GameObject netPlayAnchor;
  public GameObject netListParent;
  public GameObject matchPrefab;
  public InputField netMatchName;

  [Header("Options")]
  public GameObject optionsAnchor;
  public StringPref namePref;

  private NetworkManager manager {
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

    switch (_menuState) {
      case MenuState.Options:
        optionsActive = true;
        break;
      case MenuState.DirectConnect:
        directConnectActive = true;
        manager.networkAddress = directIpInput.text;
        break;
      case MenuState.MatchMaker:
        netPlayActive = true;
        manager.matchName = netMatchName.text;
        break;
    }

    directConnectAnchor.SetActive(directConnectActive);
    netPlayAnchor.SetActive(netPlayActive);
    optionsAnchor.SetActive(optionsActive);
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
    manager.matchMaker.ListMatches(0, 20, "", true, 0, 0, onRecieveMatchList);
  }

  public void OnSelectHostDirect() {
    manager.StartHost();
  }

  public void OnSelectClientDirect() {
    manager.StartClient();
  }

  public void OnSelectCreateMatch() {
    if (manager.matchMaker != null) {
      manager.matchName = manager.matchName.Trim();
      if (string.IsNullOrEmpty(manager.matchName) || manager.matchName.Length > 100) {
        manager.matchName = "New Game";
      }

      manager.matchMaker.CreateMatch(manager.matchName, manager.matchSize, true, "", "", "", 0, 0, manager.OnMatchCreate);
    }
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

    switch (_menuState) {
      case MenuState.MatchMaker:
        manager.StopMatchMaker();
        break;
    }

    _menuState = toState;

    switch (_menuState) {
      case MenuState.MatchMaker:
        netMatchName.text = namePref.value + "'s Game";
        manager.StartMatchMaker();
        OnSelectRefreshMatchList();
        break;
    }
  }

  private void onRecieveMatchList(bool success, string extendedInfo, List<MatchInfoSnapshot> matches) {
    foreach (var button in _spawnedButtons) {
      DestroyImmediate(button);
    }

    for (int i = 0; i < matches.Count; i++) {
      var match = matches[i];
      var matchButton = Instantiate(matchPrefab);
      matchButton.transform.SetParent(netListParent.transform);
      matchButton.GetComponentInChildren<Text>().text = match.name;
      matchButton.GetComponent<Button>().onClick.AddListener(() => {
        manager.matchName = match.name;
        manager.matchSize = (uint)match.currentSize;
        manager.matchMaker.JoinMatch(match.networkId, "", "", "", 0, 0, manager.OnMatchJoined);
      });
      matchButton.SetActive(true);

      _spawnedButtons.Add(matchButton);
    }
  }

}
