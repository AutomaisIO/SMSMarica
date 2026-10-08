# SMSMais.agente.app

## O que é

Aplicativo **Flutter** destinado ao **motorista** (agente de transporte sanitário).

## Para que serve

- Receber e seguir **rotas** e paradas do dia (pontos de embarque/desembarque).
- Exibir o **próximo** destino de forma clara durante o trajeto.
- Abrir apps de navegação (**Waze**, **Google Maps** ou outro compatível) via integração nativa no **Android**.
- Enviar a posição **GPS em intervalos** para o backend, permitindo rastreamento e estimativas de chegada para o cidadão.
- Usar **geofencing** para identificar quando o veículo **chega** perto de residências e dos locais de tratamento/unidades.

## Escopo de plataforma

**Somente Android** nesta fase — ver [ADR-0003](../docs/adr/0003-flutter-android-only-agente.md). Qualquer PR que adicione pasta `ios/` é rejeitado.

## Relação com o restante do ecossistema

Consome a API do projeto **`SMSMais.server`**. O cadastro administrativo de motoristas, veículos e alocações é feito no **`SMSMais.front`** (ou futuras integrações).

## Stack

- Flutter (stable 3.41) — Dart 3.11
- Riverpod 2.x, go_router, dio
- `very_good_analysis` como base de lints
- `permission_handler` para permissões de localização e notificação

## Estrutura

```
lib/
  app/              # router, tema, entrypoint do MaterialApp
  features/
    auth/           # login (mockado na A1)
    rota/           # rota do dia (mockada na A1)
  shared/
    api/            # Dio base
    auth/           # estado de sessão
    permissoes/     # fluxo de permissões Android
```

## Como rodar

Pré-requisitos: Flutter stable + Android SDK (API 26+) + um dispositivo/emulador.

```bash
cd SMSMais.agente.app
flutter pub get
flutter run              # debug
flutter build apk --debug
```

Credenciais do login são **mockadas** em A1 — qualquer usuário/senha válidos entram.

## Estado do trilho A

- **A1 (esta entrega)**: scaffold + login mock + rota mock + permissões.
- A2.1 Postagem periódica de GPS (depende de S3.2).
- A2.2 Geofencing local (depende de S3.2).
- A2.3 Intent para Waze / Google Maps.
- A2.4 Marcação de embarque/desembarque (depende de S3.3).

Plano detalhado: `C:\Users\berna\.claude\plans\deep-gathering-kahn.md` (trilho A).

## Distribuição pelo MDM (quiosque)

Os tablets do TFD recebem o app pelo MDM da Automais.IO, que instala, atualiza e trava o tablet
no app (quiosque). Por isso o release **sempre** sai com a mesma chave: o Android recusa atualizar um
app assinado com outra chave.

1. **Build de release** (a chave do Google, `MAPS_API_KEY`, fica em `android/local.properties`):
   ```bash
   cd SMSMais.agente.app
   flutter build apk --release
   # saída: build/app/outputs/flutter-apk/app-release.apk
   ```
   Sem a chave de assinatura o build **falha de propósito** (não há mais fallback para a chave de debug).
2. **Chave de assinatura** (fora do git):
   - `android/keystore/smsmais-agente-release.jks` + `android/key.properties` (senha e alias).
   - CI futura: variáveis `AGENTE_KEYSTORE_FILE`, `AGENTE_KEYSTORE_PASSWORD`, `AGENTE_KEY_ALIAS`,
     `AGENTE_KEY_PASSWORD`.
   - **Faça backup dos dois arquivos.** Perder a chave = desinstalar e reinstalar o app em todos os
     tablets (e refazer a restrição da chave do Google).
   - SHA-1 do certificado (restrição da chave do GCP, junto com o package `io.automais.smsmais.agente`):
     `98:FB:AB:5C:14:E3:F8:FE:81:AA:74:B1:7C:F8:F9:37:96:EE:AA:BA`.
3. **Publicar**: Automais.IO › Managed Devices › aba **Mobile** › **Apps gerenciados** › enviar o
   `app-release.apk` (pacote e versão são lidos do APK). A cada versão nova, suba o APK de novo com o
   `version` do `pubspec.yaml` incrementado (o `+N` é o versionCode, precisa crescer).
4. **Instalar**: no app do catálogo, **Instalar em…** › escolher os tablets › marcar **Definir como
   quiosque**. O MDM concede as permissões do app (câmera, localização inclusive em segundo plano,
   notificações), bloqueia a desinstalação e abre o app travado; após reboot ele volta sozinho.
