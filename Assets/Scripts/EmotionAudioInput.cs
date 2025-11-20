using System.Collections;
using UnityEngine;
using Unity.Barracuda;
using System.IO;
using MathNet.Numerics.Providers.LinearAlgebra;
using Unity.VisualScripting;
using System;
using Google.Protobuf.WellKnownTypes;

/// <summary>
/// Records real-time audio, converts to mel spectrogram (crude version),
/// feeds ONNX emotion model via Barracuda, and maps valence to passthrough color.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class EmotionAudioInput : MonoBehaviour
{
    public OVRPassthroughLayer passthrough;
    public NNModel emotionModelAsset;

    private Model runtimeModel;
    private IWorker worker;
    private AudioSource audioSource;

    private const int sampleSize = 4096; // read 4096 samples per frame
    private float[] samples = new float[sampleSize];
    private float sampleRate;

    private Color currentColor = Color.white;
    private float smoothColorSpeed = 4f;

    private const int N_MELS = 64;
    private const int TARGET_FRAMES = 216; // matches model

    private string logPath = @"C:\Users\GRACE\Documents\Adjust Lens\Assets\Log\emotion_log.txt";
    private float timer = 0f;
    private float logInterval = 5f; // write log every 5 seconds

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.clip = Microphone.Start(null, true, 5, 44100);
        audioSource.loop = true;
        while (!(Microphone.GetPosition(null) > 0)) { }
        audioSource.Play();
        sampleRate = AudioSettings.outputSampleRate;

        runtimeModel = ModelLoader.Load(emotionModelAsset);
        worker = WorkerFactory.CreateWorker(WorkerFactory.Type.Auto, runtimeModel);

        passthrough.textureOpacity = 1.0f;
        passthrough.colorMapEditorGradient = MakeNeutralGradient();
        passthrough.colorMapEditorBrightness = 0.0f;
        passthrough.colorMapEditorContrast = 0.0f;
        passthrough.colorMapEditorPosterize = 0.0f;

        string folderPath = Path.GetDirectoryName(logPath);
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }
    }

    void Update()
    {
        int micPos = Microphone.GetPosition(null);
        if (micPos < sampleSize) return;
        audioSource.GetOutputData(samples, 0);

        // Crude mel spectrogram placeholder
        float[] mel = new float[N_MELS * TARGET_FRAMES];
        for (int m = 0; m < N_MELS; m++)
        {
            for (int t = 0; t < TARGET_FRAMES; t++)
            {
                int src = Mathf.Clamp(t * sampleSize / TARGET_FRAMES, 0, sampleSize - 1);
                mel[m * TARGET_FRAMES + t] = Mathf.Abs(samples[src]) * 10f;
            }
        }

        // Feed into Barracuda
        float[] mel5 = new float[5 * N_MELS * TARGET_FRAMES];

        for (int i = 0; i < 5; i++)
        {
            Array.Copy(mel, 0, mel5, i * (N_MELS * TARGET_FRAMES), N_MELS * TARGET_FRAMES);
        }

        // Barracuda tensor dims = (N, H, W, C)
        using (var input = new Tensor(5, 1, N_MELS, TARGET_FRAMES, mel5))
        {
            worker.Execute(input);
            using (var output = worker.PeekOutput())
            {
                float valence = 0f;
                float arousal = 0f;

                if (output.length >= 2)
                {
                    valence = output[0];
                    arousal = output[1];
                }

                // Normalize valence and arousal to 0–1
                valence = (float)Mathf.Clamp01((valence - 1.5578f)); //(float)((float)1.0 / (1.0 + Math.Exp(-2.5 * (valence - 1.8))));
                arousal = (float)Mathf.Clamp01((arousal - 1.61766f)); //(float)Math.Log10(1.0 + 9.0 * (arousal - 0.8928) / 1.2576);

                timer += Time.deltaTime;
                if (timer >= logInterval)
                {
                    timer = 0f;
                    if (valence > 0f && arousal > 0f)
                    {
                        string line = $"Valence: {valence} | Arousal: {arousal}\n";
                        File.AppendAllText(logPath, line);
                        Debug.Log(line);
                    }
                }

                

                
                Color targetColor = ValenceToGradientColor(valence);
                currentColor = Color.Lerp(currentColor, targetColor, Time.deltaTime * smoothColorSpeed);
                passthrough.colorMapEditorGradient = MakeTintedGradient(currentColor);
                passthrough.colorMapEditorBrightness = ArousalToBrightness(arousal);
                passthrough.colorMapEditorContrast = 0f;
                passthrough.colorMapEditorPosterize = 0f;
                passthrough.textureOpacity = 1.0f;
            }
        }
    }

    float ArousalToBrightness (float arousal)
    {
        if (arousal < 0.2f)
            return -0.2f;
        else if (arousal < 0.4f)
            return -0.1f;
        else if (arousal < 0.6f)
            return 0f;
        else if (arousal < 0.8f)
            return 0.1f;
        else
            return 0.2f;
    }

    Color ValenceToGradientColor(float valence)
    {
        Color result;
        // f(x) = 0.12 * (Math.Pow(1.35, x) - 1) + 0.01 * x
        if (valence < 0.12f)
            result = new Color(0f, 0f, 0.8f); // Deep Blue
        else if (valence < 0.132f)
            result = new Color(0f, 0.8f, 1f); // Cyan
        else if (valence < 0.167f)
            result = new Color(0.7f, 1f, 0.3f); // Lime
        else if (valence < 0.23f)
            result = new Color(1f, 1f, 0.2f); // Yellow
        else if (valence < 0.327f)
            result = new Color(1f, 0.6f, 0f); // Orange
        else if (valence < 0.465f)
            result = new Color(1f, 0.3f, 0.1f); // Red-Orange
        else
            result = new Color(1f, 0f, 0f); // Red
        return result;
    }

    Gradient MakeNeutralGradient()
    {
        Gradient g = new Gradient();
        g.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(Color.black, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 1f)
            }
        );
        return g;
    }

    Gradient MakeTintedGradient(Color c)
    {
        Gradient g = new Gradient();
        g.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(Color.black, 0f),
                new GradientColorKey(c * 0.9f, 0.5f),
                new GradientColorKey(Color.white, 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 0.5f),
                new GradientAlphaKey(1f, 1f)
            }
        );
        return g;
    }

    void OnDestroy()
    {
        worker?.Dispose();
    }
}
