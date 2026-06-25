# TensorFlow Lite (tflite_flutter) — usado no reconhecimento facial on-device.
# O R8 reclama de classes do GPU delegate que não estão no AAR; o app usa CPU,
# então basta manter as classes do TFLite e silenciar o aviso do delegate GPU.
-keep class org.tensorflow.lite.** { *; }
-dontwarn org.tensorflow.lite.gpu.GpuDelegateFactory$Options
-dontwarn org.tensorflow.lite.gpu.**
