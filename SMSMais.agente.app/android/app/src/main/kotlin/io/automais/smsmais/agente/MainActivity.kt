package io.automais.smsmais.agente

import android.annotation.SuppressLint
import android.os.Build
import android.provider.Settings
import android.view.WindowManager
import io.flutter.embedding.android.FlutterActivity
import io.flutter.embedding.engine.FlutterEngine
import io.flutter.plugin.common.MethodChannel

class MainActivity : FlutterActivity() {

    // Canal pequeno para o que não justifica dependência nova: identificação do tablet
    // (vínculo com o veículo) e manter a tela ligada na tela de Deslocamento.
    @SuppressLint("HardwareIds")
    override fun configureFlutterEngine(flutterEngine: FlutterEngine) {
        super.configureFlutterEngine(flutterEngine)
        MethodChannel(flutterEngine.dartExecutor.binaryMessenger, CANAL).setMethodCallHandler { call, result ->
            when (call.method) {
                "info" -> result.success(
                    mapOf(
                        "modelo" to "${Build.MANUFACTURER} ${Build.MODEL}".trim(),
                        "identificador" to Settings.Secure.getString(contentResolver, Settings.Secure.ANDROID_ID),
                    )
                )
                "manterTelaLigada" -> {
                    if (call.arguments == true) window.addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON)
                    else window.clearFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON)
                    result.success(null)
                }
                else -> result.notImplemented()
            }
        }
    }

    companion object {
        private const val CANAL = "io.automais.smsmais.agente/dispositivo"
    }
}
