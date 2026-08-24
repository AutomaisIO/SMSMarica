# SMSMarica.agente.app

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
cd SMSMarica.agente.app
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
