using UnityEngine;

internal static class LocalCoopBootstrap
{
    public static int CurrentPlayerCount { get; private set; } = 1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetPlayerCount()
    {
        CurrentPlayerCount = 1;
    }

    public static void ConfigurePlayerCount(int playerCount)
    {
        CurrentPlayerCount = Mathf.Clamp(playerCount, 1, 2);
        BonkRobotController playerOne = Object.FindAnyObjectByType<BonkRobotController>();
        Camera playerOneCamera = Camera.main;
        if (playerOne == null || playerOneCamera == null)
        {
            return;
        }

        playerOne.ConfigureLocalPlayer(1, playerOneCamera.transform);
        if (CurrentPlayerCount <= 1)
        {
            playerOneCamera.rect = new Rect(0f, 0f, 1f, 1f);
            return;
        }

        playerOne.gameObject.SetActive(false);
        GameObject playerTwoObject = Object.Instantiate(
            playerOne.gameObject, playerOne.transform.parent);
        playerTwoObject.name = "Player 2";

        Camera playerTwoCamera = Object.Instantiate(playerOneCamera);
        playerTwoCamera.name = "Player 2 Camera";
        playerTwoCamera.tag = "Untagged";
        playerOneCamera.rect = new Rect(0f, 0f, 0.5f, 1f);
        playerTwoCamera.rect = new Rect(0.5f, 0f, 0.5f, 1f);

        AudioListener secondListener = playerTwoCamera.GetComponent<AudioListener>();
        if (secondListener != null)
        {
            secondListener.enabled = false;
        }

        playerOne.transform.position += Vector3.left * 2f;
        playerTwoObject.transform.position += Vector3.right * 2f;
        BonkRobotController playerTwo =
            playerTwoObject.GetComponent<BonkRobotController>();
        playerTwo.ConfigureLocalPlayer(2, playerTwoCamera.transform);

        playerOne.gameObject.SetActive(true);
        playerTwoObject.SetActive(true);
    }
}
