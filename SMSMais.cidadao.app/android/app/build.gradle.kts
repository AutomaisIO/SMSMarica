import java.util.Properties

plugins {
    id("com.android.application")
    id("kotlin-android")
    // The Flutter Gradle Plugin must be applied after the Android and Kotlin Gradle plugins.
    id("dev.flutter.flutter-gradle-plugin")
}

// Assinatura de release = a CHAVE DE UPLOAD da Play Store: android/key.properties (gitignored; o
// .jks fica FORA do repositório) ou variáveis CIDADAO_* (CI). A Play reassina com a chave dela
// (Assinatura de apps do Google Play); perder a de upload se resolve pelo Play Console, mas sem ela
// não sai versão nova. Sem fallback para a chave de debug: a loja recusa, e o erro aqui é mais claro.
val keyProperties = Properties().apply {
    val file = rootProject.file("key.properties")
    if (file.exists()) {
        file.inputStream().use { load(it) }
    }
}
val releaseStoreFile: String? =
    keyProperties.getProperty("storeFile") ?: System.getenv("CIDADAO_KEYSTORE_FILE")
val releaseStorePassword: String? =
    keyProperties.getProperty("storePassword") ?: System.getenv("CIDADAO_KEYSTORE_PASSWORD")
val releaseKeyAlias: String? =
    keyProperties.getProperty("keyAlias") ?: System.getenv("CIDADAO_KEY_ALIAS")
val releaseKeyPassword: String? =
    keyProperties.getProperty("keyPassword") ?: System.getenv("CIDADAO_KEY_PASSWORD")
val hasReleaseSigning = listOf(releaseStoreFile, releaseStorePassword, releaseKeyAlias, releaseKeyPassword)
    .all { !it.isNullOrBlank() }

android {
    namespace = "io.automais.smsmais.cidadao"
    compileSdk = flutter.compileSdkVersion
    ndkVersion = flutter.ndkVersion

    compileOptions {
        // Exigido pelo flutter_local_notifications (notificação do push com o app aberto).
        isCoreLibraryDesugaringEnabled = true
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }

    kotlinOptions {
        jvmTarget = JavaVersion.VERSION_17.toString()
    }

    defaultConfig {
        // Não muda nunca: é a identidade do app na Play Store e no Firebase.
        applicationId = "io.automais.smsmais.cidadao"
        minSdk = flutter.minSdkVersion
        targetSdk = flutter.targetSdkVersion
        versionCode = flutter.versionCode
        versionName = flutter.versionName
    }

    signingConfigs {
        if (hasReleaseSigning) {
            create("release") {
                storeFile = rootProject.file(releaseStoreFile!!)
                storePassword = releaseStorePassword
                keyAlias = releaseKeyAlias
                keyPassword = releaseKeyPassword
            }
        }
    }

    buildTypes {
        release {
            if (hasReleaseSigning) {
                signingConfig = signingConfigs.getByName("release")
            }
        }
    }
}

flutter {
    source = "../.."
}

dependencies {
    coreLibraryDesugaring("com.android.tools:desugar_jdk_libs:2.1.4")
}

// Release sem chave falha cedo, com a causa clara (em vez de gerar pacote com chave de debug, que
// a Play Store recusa). Vale para o APK (assemble) e para o pacote da loja (bundle).
gradle.taskGraph.whenReady {
    val buildsRelease = allTasks.any {
        it.project == project && it.name.contains("Release") &&
            (it.name.startsWith("assemble") || it.name.startsWith("bundle"))
    }
    if (buildsRelease && !hasReleaseSigning) {
        throw GradleException(
            "Assinatura de release ausente: crie android/key.properties (storeFile, storePassword, " +
                "keyAlias, keyPassword) ou defina CIDADAO_KEYSTORE_FILE/CIDADAO_KEYSTORE_PASSWORD/" +
                "CIDADAO_KEY_ALIAS/CIDADAO_KEY_PASSWORD. Ver README, seção \"Publicar na Play Store\"."
        )
    }
}
