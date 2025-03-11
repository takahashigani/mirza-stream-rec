using UnityEngine;
using System;
using UnityEngine.UI;
using UnityEngine.Android;

public class AudioManager : MonoBehaviour
{
    //　音声収録用のAudioSource
    [SerializeField] AudioSource m_AudioSource;

    //　デバッグ用のステータス表示用Text
    [SerializeField] Text m_StatusText;

    //　Android側の音声収録処理を行うクラス
    private AndroidJavaObject _audioRecorderHandler;

    //　音声データを格納するAudioClip
    private AudioClip _audioClip;

    ///　Androidマイク収録時のサンプリングレート数
    private const int _sampleRate = 44100;

    //　base64->float[]へ変換した際のLengh数
    private const int _sampleLength = 44800;

    private void Start()
    {
        Permission.RequestUserPermission(Permission.Microphone);
    }

    /// <summary>
    /// 音声収録開始
    /// </summary>
    public void StartRecording()
    {
        m_StatusText.text = "Recording Status: Start recording";

        // Java側のAudioRecorderHandlerクラスをインスタンス化
        _audioRecorderHandler = new AndroidJavaObject("recordlibrary.AudioRecorderHandler");

        using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        {
            AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            AndroidJavaObject context = activity.Call<AndroidJavaObject>("getApplicationContext");
            AndroidJavaObject audioManager = context.Call<AndroidJavaObject>("getSystemService", "audio");

            // AudioDeviceInfoの配列を取得
            AndroidJavaObject[] devices = audioManager.Call<AndroidJavaObject[]>("getDevices", 1);

            AndroidJavaObject proxyDevice = null;

            foreach (var device in devices)
            {
                int deviceType = device.Call<int>("getType");
                if (deviceType == 20) // TYPE_IP
                {
                    proxyDevice = device;
                    break;
                }
            }

            if (proxyDevice != null)
            {
                m_StatusText.text = $"Recording Status: Start recording";
                _audioRecorderHandler.Call("startStreamingRecording", proxyDevice);
                _audioClip = AudioClip.Create("MicrophoneAudio", _sampleLength, 1, _sampleRate, false);
                m_AudioSource.clip = _audioClip;
            }
            else
            {
                Debug.LogError("in-proxy deviceが見つかりません");
            }
        }
    }

    /// <summary>
    /// 音声収録停止
    /// </summary>
    public void StopRecording()
    {
        m_StatusText.text = "Recording Status: Stop recording";

        if (_audioRecorderHandler != null)
        {
            _audioRecorderHandler.Call("stopStreamingRecording");
        }
    }

    /// <summary>
    /// Android側から音声を文字列(base64)受け取り
    /// </summary>
    public void OnAudioDataReceived(string base64AudioData)
    {
        if (!string.IsNullOrEmpty(base64AudioData))
        {
            m_StatusText.text = $"Received audio base64AudioData: {base64AudioData}";
            byte[] audioData = Convert.FromBase64String(base64AudioData);

            float[] audioSamples = ConvertByteArrayToFloatArray(audioData);
            _audioClip.SetData(audioSamples, 0);
            m_AudioSource.clip = _audioClip;
            m_AudioSource.Play();
        }
    }

    /// <summary>
    /// 音声データの型変更 byte[] → Float[]
    /// </summary>
    private float[] ConvertByteArrayToFloatArray(byte[] byteArray)
    {
        int floatCount = byteArray.Length / 2; // 16ビットPCMの場合
        float[] floatArray = new float[floatCount];

        for (int i = 0; i < floatCount; i++)
        {
            short sample = (short)(byteArray[i * 2] | (byteArray[i * 2 + 1] << 8));
            floatArray[i] = sample / 32768.0f; // 16ビットPCMをfloatに正規化
        }
        return floatArray;
    }
}

