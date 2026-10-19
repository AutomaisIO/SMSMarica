package io.automais.smsmais.agente

import android.app.Application
import android.content.Context
import android.content.res.Configuration
import java.util.Locale

/**
 * pt-BR no processo inteiro, desde a partida.
 *
 * O Navigation SDK escolhe o idioma da voz (e baixa o pacote de frases) cedo, no início do
 * processo — fixar o idioma só na MainActivity chegava tarde: a narração saía com a voz e as
 * frases em inglês (`Synthesis request for locale eng-USA`, pacote `voice/en_US…`).
 */
class AgenteApplication : Application() {
    override fun attachBaseContext(base: Context) {
        super.attachBaseContext(Idioma.emPortugues(base))
    }

    override fun onConfigurationChanged(newConfig: Configuration) {
        super.onConfigurationChanged(newConfig)
        Idioma.fixarPadrao()
    }
}

/**
 * O produto é só para o Brasil, mas os tablets da frota vêm com o sistema em inglês (en-US):
 * o app fixa pt-BR em vez de depender da configuração de cada tablet. O Android repõe o Locale
 * padrão do sistema em mudança de configuração, por isso [fixarPadrao] é reaplicado nela.
 */
object Idioma {
    private val PT_BR: Locale = Locale.forLanguageTag("pt-BR")

    fun fixarPadrao() {
        Locale.setDefault(PT_BR)
    }

    fun emPortugues(base: Context): Context {
        fixarPadrao()
        val config = Configuration(base.resources.configuration).apply { setLocale(PT_BR) }
        return base.createConfigurationContext(config)
    }
}
