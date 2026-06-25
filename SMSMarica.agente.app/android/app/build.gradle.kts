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

android {
    namespace = "online.smsmarica.agente"
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
        applicationId = "online.smsmarica.agente"
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

    buildTypes {
        release {
            // TODO: Add your own signing config for the release build.
            // Signing with the debug keys for now, so `flutter run --release` works.
            signingConfig = signingConfigs.getByName("debug")
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

dependencies {
    // Desugaring com suporte a java.nio — exigido pelo Google Navigation SDK.
    coreLibraryDesugaring("com.android.tools:desugar_jdk_libs_nio:2.0.4")
}
