using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;

public class RelayConnector : MonoBehaviour {

    public GameObject MainMenuRoot;
    public GameObject ErrorRoot;
    public Text ErrorLabel;
    public InputField JoinCodeField;
    public Text JoinCodeLabel;
    public Text ConnectingLabel;

    public bool IsConnecting;

    public async void OnHostPressed() {
        if (ErrorRoot != null) {
            ErrorRoot.SetActive(false);
        }

        string code;
        try {
            IsConnecting = true;
            code = await StartHostWithRelay();
        } catch (Exception e) {
            Debug.LogException(e);
            if (ErrorRoot != null) {
                ErrorRoot.SetActive(true);
                ErrorLabel.text = e.ToString();
            }
            return;
        } finally {
            IsConnecting = false;
        }

        JoinCodeLabel.text = code;
        JoinCodeField.text = "";
    }

    public async void OnJoinPressed() {
        if (ErrorRoot != null) {
            ErrorRoot.SetActive(false);
        }

        try {
            IsConnecting = true;
            await StartClientWithRelay(JoinCodeField.text);
        } catch (Exception e) {
            Debug.LogException(e);
            if (ErrorRoot != null) {
                ErrorRoot.SetActive(true);
                ErrorLabel.text = e.ToString();
            }
        } finally {
            IsConnecting = false;
        }

        JoinCodeLabel.text = JoinCodeField.text;
        JoinCodeField.text = "";
    }

    public void OnCopyJoinCode() {
        GUIUtility.systemCopyBuffer = JoinCodeLabel.text;
    }

    private void Update() {
        MainMenuRoot.SetActive(Player.Local == null);

        if (IsConnecting) {
            ConnectingLabel.gameObject.SetActive(true);
            ConnectingLabel.text = "Connecting" + "".PadRight(Mathf.RoundToInt(Time.time * 4f) % 3 + 1, '.');
        } else {
            ConnectingLabel.gameObject.SetActive(false);
        }
    }

    public async Task<string> StartHostWithRelay(int maxConnections = 8) {
        await UnityServices.InitializeAsync();
        if (!AuthenticationService.Instance.IsSignedIn) {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
        Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);
        NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(new RelayServerData(allocation, "udp"));
        var joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

        if (!NetworkManager.Singleton.StartHost()) {
            throw new Exception("Was unable to start host for unknown reason.");
        }

        return joinCode;
    }

    public async Task StartClientWithRelay(string joinCode) {
        if (string.IsNullOrWhiteSpace(joinCode)) {
            throw new Exception("Must provide a join code!");
        }

        await UnityServices.InitializeAsync();
        if (!AuthenticationService.Instance.IsSignedIn) {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        var joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode: joinCode);
        NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(new RelayServerData(joinAllocation, "udp"));

        if (!NetworkManager.Singleton.StartClient()) {
            throw new Exception("Was unable to join game for unknown reason!");
        }
    }
}
