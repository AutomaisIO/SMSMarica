package io.automais.smsmais.agente

import android.annotation.SuppressLint
import android.content.Context
import android.content.pm.PackageManager
import android.content.res.Configuration
import android.os.Build
import android.provider.Settings
import android.view.WindowManager
import io.flutter.embedding.android.FlutterActivity
import io.flutter.embedding.engine.FlutterEngine
import io.flutter.plugin.common.MethodChannel

class MainActivity : FlutterActivity() {

    // pt-BR também no contexto da Activity (textos do Navigation SDK na tela). Ver Idioma.
    override fun attachBaseContext(base: Context) {
        super.attachBaseContext(Idioma.emPortugues(base))
    }

    override fun onConfigurationChanged(newConfig: Configuration) {
        Idioma.fixarPadrao()
        super.onConfigurationChanged(newConfig)
    }

    // Canal pequeno para o que não justifica dependência nova: identificação do tablet
    // (vínculo com o veículo), manter a tela ligada na tela de Deslocamento e saber se o
    // build trouxe a chave do Google Maps (a navegação não funciona sem ela).
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
                // Se o build trouxe a chave do Google (MAPS_API_KEY → meta-data do manifesto).
                "temChaveMaps" -> {
                    @Suppress("DEPRECATION")
                    val meta = packageManager.getApplicationInfo(packageName, PackageManager.GET_META_DATA).metaData
                    result.success(!meta?.getString("com.google.android.geo.API_KEY").isNullOrBlank())
                }
                else -> result.notImplemented()
            }
        }
    }

    companion object {
        private const val CANAL = "io.automais.smsmais.agente/dispositivo"
    }
}
