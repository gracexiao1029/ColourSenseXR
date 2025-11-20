# ColourSenseXR - Audio-Reactive VR Passthrough

ColourSenseXR is a Unity VR project that dynamically modifies Oculus passthrough filters based on real-time audio input using two distinct strategies: pitch-to-color mapping and emotion-based color analysis.

## Overview

This project implements two audio-reactive passthrough strategies for VR headsets:

1. **Pitch-to-Color Mapping** (`Assets/Scripts/AudioInput.cs`) - Maps musical notes and frequencies to colors using HSL color space
2. **Emotion-Based Analysis** (`Assets/Scripts/EmotionAudioInput.cs`) - Uses a Barracuda neural network model to analyze audio emotion (valence/arousal) and map it to colors

## Features

### Strategy 1: Pitch-to-Color Mapping

The `AudioInput` class captures microphone input and performs real-time frequency analysis to determine the dominant pitch, then maps it to colors:

- **Frequency Detection**: Uses FFT (Fast Fourier Transform) to identify dominant frequencies
- **Musical Mapping**: Converts frequencies to musical notes (C, D, E, F, G, A, B) with custom color assignments (A → 1 = Brown, B → 2 = Red, C → 3 = Orange, D → 4 = Yellow, E → 5 = Green, F → 6 = Blue, G → 7 = Violet)
- **Dynamic Brightness**: Adjusts lightness based on octave and pitch position within the octave
- **Smooth Transitions**: Color changes are smoothly interpolated for visual comfort

### Strategy 2: Emotion-Based Analysis

Trained Model: https://github.com/gracexiao1029/Music2EmotionModel

The `EmotionAudioInput` class uses machine learning to analyze emotional content in audio:

- **Neural Network**: Employs Unity Barracuda to run an ONNX emotion recognition model
- **Mel Spectrogram**: Converts audio to mel spectrogram format (64 mel bands × 1292 frames) for model input
- **Valence Mapping**: Maps emotional valence to a color gradient (Blue → Cyan → Lime → Yellow → Orange → Red-Orange → Red)
- **Arousal to Brightness**: Converts arousal levels to passthrough brightness adjustments
- **Logging**: Records valence and arousal values every 5 seconds for analysis

## Technical Requirements

- Unity with Oculus Integration SDK
- Unity Barracuda package (for emotion analysis)
- MathNet.Numerics (for emotion analysis)
- Oculus VR headset with passthrough support
- Microphone access

## Implementation Details

Both strategies manipulate the `OVRPassthroughLayer` component to modify the visual appearance of passthrough video:

- **Color Gradients**: Applied via `colorMapEditorGradient`
- **Brightness/Contrast**: Controlled through `colorMapEditorBrightness` and `colorMapEditorContrast`
- **Opacity**: Set via `textureOpacity`

## Usage

1. Attach either `AudioInput` or `EmotionAudioInput` component to a GameObject with an AudioSource
2. Assign the `OVRPassthroughLayer` reference in the inspector
3. For emotion analysis, assign the ONNX model asset to `emotionModelAsset`
4. Run the scene - the passthrough will react to audio input in real-time

## Notes

The project includes a `PassthroughManager` class that provides basic passthrough controls and toggle functionality, though the main audio-reactive features are implemented in the two strategy classes mentioned above. The emotion analysis strategy logs output to `Adjust Lens\Assets\Log\emotion_log.txt` for debugging purposes.
