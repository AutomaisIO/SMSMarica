import java.util.Properties

plugins {
    id("com.android.application")
    id("kotlin-android")
    // The Flutter Gradle Plugin must be applied after the Android and Kotlin Gradle plugins.
    id("dev.flutter.flutter-gradle-plugin")
}

// Chave do Google Navigation SDK lida de android/local.properties (gitignored),
// injetada no manifesto via manifestPlaceholders. NÃO commitar a chave.
val localProperties = Properties().apply {
    val file = rootProject.file("local.properties")
    if (file.exists()) {
        file.inputStream().use { load(it) }
    }
}
val mapsApiKey: String =
    (localProperties.getProperty("MAPS_API_KEY") ?: System.getenv("MAPS_API_KEY") ?: "")

// Assinatura de release: android/key.properties (gitignored, keystore em android/keystore/) ou
// variáveis AGENTE_* (CI). Chave ESTÁVEL é obrigatória: o MDM atualiza o app por cima e o Android
// recusa APK assinado com outra chave — por isso não há fallback para a chave de debug.
val keyProperties = Properties().apply {
    val file = rootProject.file("key.properties")
    if (file.exists()) {
        file.inputStream().use { load(it) }
    }
}
val releaseStoreFile: String? =
    keyProperties.getProperty("storeFile") ?: System.getenv("AGENTE_KEYSTORE_FILE")
val releaseStorePassword: String? =
    keyProperties.getProperty("storePassword") ?: System.getenv("AGENTE_KEYSTORE_PASSWORD")
val releaseKeyAlias: String? =
    keyProperties.getProperty("keyAlias") ?: System.getenv("AGENTE_KEY_ALIAS")
val releaseKeyPassword: String? =
    keyProperties.getProperty("keyPassword") ?: System.getenv("AGENTE_KEY_PASSWORD")
val hasReleaseSigning = listOf(releaseStoreFile, releaseStorePassword, releaseKeyAlias, releaseKeyPassword)
    .all { !it.isNullOrBlank() }

android {
    namespace = "io.automais.smsmais.agente"
    compileSdk = flutter.compileSdkVersion
    ndkVersion = flutter.ndkVersion

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
        // Navigation SDK exige desugaring de java.nio quando minSdk < 34.
        isCoreLibraryDesugaringEnabled = true
    }

    kotlinOptions {
        jvmTarget = JavaVersion.VERSION_17.toString()
    }

    defaultConfig {
        applicationId = "io.automais.smsmais.agente"
        // minSdk 26 (Android 8.0) garante suporte a foreground services robustos
        // e background location (requer 29, mas o app degrada com fallback no handler).
        // Navigation SDK exige minSdk >= 24.
        minSdk = 26
        targetSdk = flutter.targetSdkVersion
        versionCode = flutter.versionCode
        versionName = flutter.versionName
        // Disponibiliza a chave para o ${MAPS_API_KEY} do AndroidManifest.
        manifestPlaceholders["MAPS_API_KEY"] = mapsApiKey
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
            proguardFiles(
                getDefaultProguardFile("proguard-android-optimize.txt"),
                "proguard-rules.pro",
            )
        }
    }
}

flutter {
    source = "../.."
}

// Release sem chave falha cedo, com a causa clara (em vez de gerar APK com chave de debug).
gradle.taskGraph.whenReady {
    val buildsRelease = allTasks.any { it.project == project && it.name.contains("Release") && it.name.startsWith("assemble") }
    if (buildsRelease && !hasReleaseSigning) {
        throw GradleException(
            "Assinatura de release ausente: crie android/key.properties (storeFile, storePassword, " +
                "keyAlias, keyPassword) ou defina AGENTE_KEYSTORE_FILE/AGENTE_KEYSTORE_PASSWORD/" +
                "AGENTE_KEY_ALIAS/AGENTE_KEY_PASSWORD. Ver README, seção \"Distribuição pelo MDM\"."
        )
    }
}

dependencies {
    // Desugaring com suporte a java.nio — exigido pelo Google Navigation SDK.
    coreLibraryDesugaring("com.android.tools:desugar_jdk_libs_nio:2.0.4")
}
